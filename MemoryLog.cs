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

using System.IO;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace TaskMaster
{
	using System;

	public class LogEventArgs : EventArgs
	{
		public readonly string Message;
		public readonly LogEventLevel Level;

		public LogEventArgs(string logmessage, LogEventLevel level)
		{
			Message = logmessage;
			Level = level;
		}
	}

	static class MemoryLog
	{
		public static event EventHandler<LogEventArgs> onNewEvent;

		public static int Max = 50;
		static object LogLock = new object();
		public static System.Collections.Generic.List<LogEventArgs> Logs = new System.Collections.Generic.List<LogEventArgs>(Max);

		public static LoggingLevelSwitch LevelSwitch;

		public static void ExcludeDebug()
		{
			LevelSwitch.MinimumLevel = LogEventLevel.Information;
		}

		public static void ExcludeTrace()
		{
			LevelSwitch.MinimumLevel = LogEventLevel.Debug;
		}

		public static void IncludeTrace()
		{
			LevelSwitch.MinimumLevel = LogEventLevel.Verbose;
		}

		public static void Emit(object sender, LogEventArgs e)
		{
			lock (LogLock)
			{
				if (Logs.Count > Max)
					Logs.RemoveAt(0);
				Logs.Add(e);
			}
			onNewEvent?.Invoke(sender, e);
		}

		public static System.Collections.Generic.List<LogEventArgs> Copy()
		{
			System.Collections.Generic.List<LogEventArgs> logcopy;
			lock (LogLock)
			{
				logcopy = new System.Collections.Generic.List<LogEventArgs>(Logs);
			}
			return logcopy;
		}
	}

	namespace SerilogMemorySink
	{
		class MemorySink : Serilog.Core.ILogEventSink
		{
			readonly TextWriter p_output;
			object sinklock = new object();
			readonly IFormatProvider p_formatProvider;
			readonly ITextFormatter p_textFormatter;
			public LoggingLevelSwitch LevelSwitch;
			const string p_DefaultOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

			public MemorySink(IFormatProvider formatProvider, string outputTemplate = p_DefaultOutputTemplate, LoggingLevelSwitch levelSwitch = null)
			{
				p_formatProvider = formatProvider;
				p_textFormatter = new Serilog.Formatting.Display.MessageTemplateTextFormatter(
					outputTemplate ?? p_DefaultOutputTemplate,
					p_formatProvider
				);
				p_output = new System.IO.StringWriter();
				LevelSwitch = levelSwitch;
			}

			public void Emit(LogEvent e)
			{
				if (e.Level < LevelSwitch.MinimumLevel) return;
				string t;
				lock (sinklock)
				{
					p_textFormatter.Format(e, p_output);
					t = p_output.ToString();
					((System.IO.StringWriter)p_output).GetStringBuilder().Clear();
				}
				MemoryLog.Emit(this, new LogEventArgs(t, e.Level));
			}
		}

		public static class MemorySinkExtensions
		{
			public static Serilog.LoggerConfiguration MemorySink(
				this LoggerSinkConfiguration logConf,
				IFormatProvider formatProvider = null,
				string outputTemplate = null,
				LoggingLevelSwitch levelSwitch = null
			)
			{
				return logConf.Sink(new MemorySink(formatProvider, outputTemplate, levelSwitch));
			}
		}
	}
}

