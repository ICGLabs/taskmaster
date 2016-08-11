﻿//
// LogWindow.cs
//
// Author:
//       M.A. (enmoku) <>
//
// Copyright (c) 2016 M.A. (enmoku)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Windows.Forms;

namespace TaskMaster
{

	public class LogEventArgs : EventArgs
	{
		public readonly NLog.LogEventInfo Info;
		public readonly string Message;

		public LogEventArgs(NLog.LogEventInfo loginfo, string logmessage=null)
		{
			Info = loginfo;
			Message = logmessage;
		}
	}

	[NLog.Targets.Target("MemLog")]
	public sealed class MemLog : NLog.Targets.TargetWithLayout
	{
		private static NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public int Max = 50;

		public System.Collections.Generic.List<string> Logs;
		public event System.EventHandler<LogEventArgs> OnNewLog;

		public MemLog()
		{
			Logs = new System.Collections.Generic.List<string>(25);
			Layout = "${callsite} :: ${message}";
		}

		int culling = 0;

		void onNewLogHandler(object sender, LogEventArgs e)
		{
			EventHandler<LogEventArgs> handler = OnNewLog;
			if (handler != null)
				handler(this, e);

			// hysterisis and such for log culling
			if ((Logs.Count > Max+10) && System.Threading.Interlocked.CompareExchange(ref culling, 1, 0) == 1)
			{
				System.Threading.Tasks.Task.Run(async () =>
				{
					await System.Threading.Tasks.Task.Delay(500);

					while (Logs.Count > Max)
						Logs.RemoveAt(0);

					System.Threading.Interlocked.Exchange(ref culling, 0);
				});
			}
		}

		protected override void Write(NLog.LogEventInfo logEvent)
		{
			string logMessage = this.Layout.Render(logEvent);
			Logs.Add(logMessage);
			onNewLogHandler(this, new LogEventArgs(logEvent, logMessage));
		}
	}

	public class LogWindow : Form
	{
		public LogWindow()
		{
			
		}
	}
}

