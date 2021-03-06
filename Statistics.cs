﻿//
// Statistics.cs
//
// Author:
//       M.A. (https://github.com/mkahvi)
//
// Copyright (c) 2016-2018 M.A.
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

namespace Taskmaster
{
	public static class Statistics
	{
		public static int ParentSeeks { get; set; } = 0;
		public static double Parentseektime { get; set; } = 0;

		public static int WMIqueries { get; set; } = 0;
		public static double WMIquerytime { get; set; } = 0;

		public static int WMIPolling { get; set; } = 0;
		public static double WMIPollTime { get; set; } = 0;

		public static int MaintenanceCount { get; set; } = 0;
		public static double MaintenanceTime { get; set; } = 0;

		public static double PathCacheCurrent { get; set; } = 0;
		public static double PathCachePeak { get; set; } = 0;
		public static double PathCacheHits { get; set; } = 0;
		public static double PathCacheMisses { get; set; } = 0;

		public static long TouchCount { get; set; } = 0;

		public static int FatalErrors { get; set; } = 0;

		public static long PathFindAttempts { get; set; } = 0;
		public static long PathFindViaModule { get; set; } = 0;
		public static long PathFindViaC { get; set; } = 0;
		public static long PathFindViaWMI { get; set; } = 0;
	}
}