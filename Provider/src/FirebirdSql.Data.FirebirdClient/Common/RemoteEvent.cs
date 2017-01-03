﻿/*
 *	Firebird ADO.NET Data provider for .NET and Mono
 *
 *	   The contents of this file are subject to the Initial
 *	   Developer's Public License Version 1.0 (the "License");
 *	   you may not use this file except in compliance with the
 *	   License. You may obtain a copy of the License at
 *	   http://www.firebirdsql.org/index.php?op=doc&id=idpl
 *
 *	   Software distributed under the License is distributed on
 *	   an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either
 *	   express or implied. See the License for the specific
 *	   language governing rights and limitations under the License.
 *
 *	Copyright (c) 2002, 2007 Carlos Guzman Alvarez
 *	All Rights Reserved.
 *
 *  Contributors:
 *      Jiri Cincura (jiri@cincura.net)
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FirebirdSql.Data.Common
{
	internal class RemoteEvent
	{
		internal const int MaxEvents = ushort.MaxValue;

		private List<string> _events;
		private Charset _charset;
		private IDatabase _db;
		private int[] _previousCounts;
		private int[] _currentCounts;

		public int LocalId { get; set; }
		public int RemoteId { get; set; }
		public Action<string, int> EventCountsCallback { get; set; }
		public Action<Exception> EventErrorCallback { get; set; }

		public List<string> Events
		{
			get { return _events; }
		}

		public RemoteEvent(IDatabase db)
		{
			LocalId = 0;
			RemoteId = 0;
			_events = new List<string>();
			_charset = db.Charset;
			_db = db;
		}

		public void QueueEvents()
		{
			_db.QueueEvents(this);
		}

		public void CancelEvents()
		{
			_db.CancelEvents(this);
			_currentCounts = null;
			_previousCounts = null;
		}

		internal void EventCounts(byte[] buffer)
		{
			var pos = 1;

			_previousCounts = _currentCounts;
			_currentCounts = new int[_events.Count];

			while (pos < buffer.Length)
			{
				var length = buffer[pos++];
				var eventName = _charset.GetString(buffer, pos, length);

				pos += length;

				var index = _events.IndexOf(eventName);
				_currentCounts[index] = BitConverter.ToInt32(buffer, pos) - 1;

				pos += 4;
			}

			for (var i = 0; i < _events.Count; i++)
			{
				var count = _currentCounts[i] - _previousCounts[i];
				if (count == 0)
					continue;
				EventCountsCallback(_events[i], count);
			}

			QueueEvents();
		}

		internal void EventError(Exception exception)
		{
			EventErrorCallback(exception);
		}

		internal EventParameterBuffer ToEpb()
		{
			_currentCounts = _currentCounts ?? new int[_events.Count];
			EventParameterBuffer epb = new EventParameterBuffer();
			epb.Append(IscCodes.EPB_version1);
			for (var i = 0; i < _events.Count; i++)
			{
				epb.Append(_events[i], _currentCounts[i] + 1);
			}
			return epb;
		}
	}
}
