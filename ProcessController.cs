//
// ProcessController.cs
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace Taskmaster
{
	/// <summary>
	/// Process controller.
	/// </summary>
	sealed public class ProcessController : IDisposable
	{
		/// <summary>
		/// Public identifier.
		/// </summary>
		public int Id { get; set; } = -1;

		/// <summary>
		/// Whether or not this rule is enabled.
		/// </summary>
		public bool Enabled { get; set; } = false;

		/// <summary>
		/// Whether this rule is valid.
		/// </summary>
		public bool Valid { get; set; } = false;

		/// <summary>
		/// Human-readable friendly name for the process.
		/// </summary>
		public string FriendlyName { get; set; } = null;

		string p_Executable = null;
		/// <summary>
		/// Executable filename related to this.
		/// </summary>
		public string Executable
		{
			get
			{
				return p_Executable;
			}
			set
			{
				ExecutableFriendlyName = System.IO.Path.GetFileNameWithoutExtension(p_Executable = value);
			}
		}

		public string Path { get; set; } = null;

		public string[] IgnoreList { get; set; } = null;

		/*
		/// <summary>
		/// Determines if the process I/O is to be set background.
		/// </summary>
		/// <value><c>true</c> if background process; otherwise, <c>false</c>.</value>
		public bool BackgroundIO { get; set; } = false;
		*/

		/// <summary>
		/// Determines if the values are only maintained when the app is in foreground.
		/// </summary>
		/// <value><c>true</c> if foreground; otherwise, <c>false</c>.</value>
		public bool ForegroundOnly { get; set; } = false;

		/// <summary>
		/// Target priority class for the process.
		/// </summary>
		public System.Diagnostics.ProcessPriorityClass Priority = System.Diagnostics.ProcessPriorityClass.Normal;

		/// <summary>
		/// CPU core affinity.
		/// </summary>
		public IntPtr Affinity = new IntPtr(ProcessManager.allCPUsMask);

		/// <summary>
		/// Affinity describes allowed cores more than actual affinity.
		/// </summary>
		public bool AllowedCores = false;

		/// <summary>
		/// The power plan.
		/// </summary>
		public PowerInfo.PowerMode PowerPlan = PowerInfo.PowerMode.Undefined;

		/// <summary>
		/// Allow priority decrease.
		/// </summary>
		public bool Decrease { get; set; } = true;
		/// <summary>
		/// Allow priority increase.
		/// </summary>
		public bool Increase { get; set; } = true;

		int p_Recheck = 0;
		public int Recheck
		{
			get
			{
				return p_Recheck;
			}
			set
			{
				p_Recheck = value.Constrain(0, 300);
			}
		}

		public bool AllowPaging = false;

		/// <summary>
		/// Delay in milliseconds before we attempt to alter the process.
		/// </summary>
		public int ModifyDelay { get; set; } = 0;

		int p_Rescan;
		/// <summary>
		/// Delay in minutes before we try to use Scan again.
		/// </summary>
		public int Rescan
		{
			get { return p_Rescan; }
			set { p_Rescan = value.Constrain(0, 300); }
		}

		/// <summary>
		/// Frienly executable name as required by various System.Process functions.
		/// Same as <see cref="T:Taskmaster.ProcessControl.Executable"/> but with the extension missing.
		/// </summary>
		public string ExecutableFriendlyName { get; set; } = null;

		public ProcessController(string name, ProcessPriorityClass priority, int affinity, string path = null)
		{
			FriendlyName = name;
			// Executable = executable;

			Priority = priority;
			if (affinity != ProcessManager.allCPUsMask)
				Affinity = new IntPtr(affinity);

			if (!string.IsNullOrEmpty(path))
			{
				Path = path;
				if (!System.IO.Directory.Exists(Path))
					Log.Warning("{FriendlyName} configured path {Path} does not exist!", FriendlyName, path);

				if (Path != null)
				{
					Log.Information("[{FriendlyName}] Watched in: {Path} [Priority: {Priority}, Mask: {Mask}]",
									FriendlyName, Path, Priority, Affinity.ToInt32());
				}
			}
		}

		const string watchlistfile = "Watchlist.ini";

		public void DeleteConfig(SharpConfig.Configuration cfg = null)
		{
			if (cfg == null)
				cfg = Taskmaster.LoadConfig(watchlistfile);

			cfg.Remove(FriendlyName); // remove the section, should remove items in the section

			Taskmaster.MarkDirtyINI(cfg);
		}

		public void SaveConfig(SharpConfig.Configuration cfg = null, SharpConfig.Section app=null)
		{
			if (cfg == null)
				cfg = Taskmaster.LoadConfig(watchlistfile);

			if (app == null)
				app = cfg[FriendlyName];

			if (!string.IsNullOrEmpty(Executable))
				app["Image"].StringValue = Executable;
			else
				app.Remove("Image");
			if (!string.IsNullOrEmpty(Path))
				app["Path"].StringValue = Path;
			else
				app.Remove("Path");
			app["Increase"].BoolValue = Increase;
			app["Decrease"].BoolValue = Decrease;
			app["Priority"].IntValue = ProcessHelpers.PriorityToInt(Priority);

			var affinity = Affinity.ToInt32();
			if (affinity == ProcessManager.allCPUsMask) affinity = 0;
			if (affinity > 0)
				app["Affinity"].IntValue = Affinity.ToInt32();
			else
				app.Remove("Affinity"); // windows defaults to all cores, pointless to set it

			var pmode = PowerManager.GetModeName(PowerPlan);
			if (PowerPlan != PowerInfo.PowerMode.Undefined)
				app["Power mode"].StringValue = PowerManager.GetModeName(PowerPlan);
			else
				app.Remove("Power mode");

			if (ForegroundOnly)
			{
				app["Foreground only"].BoolValue = ForegroundOnly;
				if (BackgroundPriority != ProcessPriorityClass.RealTime)
					app["Background priority"].IntValue = ProcessHelpers.PriorityToInt(BackgroundPriority);
				else
					app.Remove("Background priority");
				if (BackgroundPowerdown)
					app["Background powerdown"].BoolValue = BackgroundPowerdown;
				else
					app.Remove("Background powerdown");
			}
			else
			{
				app.Remove("Foreground only");
				app.Remove("Background priority");
				app.Remove("Background powerdown");
			}

			if (AllowPaging)
				app["Allow paging"].BoolValue = AllowPaging;
			else
				app.Remove("Allow paging");
			if (Rescan > 0) app["Rescan"].IntValue = Rescan;
			else app.Remove("Rescan");
			if (Recheck > 0)
				app["Recheck"].IntValue = Recheck;
			else
				app.Remove("Recheck");
			if (!Enabled) app["Enabled"].BoolValue = Enabled;
			else app.Remove("Enabled");

			if (IgnoreList != null && IgnoreList.Length > 0)
				app["Ignore"].StringValueArray = IgnoreList;
			else
				app.Remove("Ignore");

			Taskmaster.MarkDirtyINI(cfg);
		}

		const string statfile = "Watchlist.Statistics.ini";

		public void LoadStats()
		{
			var stats = Taskmaster.LoadConfig(statfile);

			string statkey = null;
			if (Executable != null) statkey = Executable;
			else if (Path != null) statkey = Path;

			if (statkey != null)
			{
				Adjusts = stats[statkey].TryGet("Adjusts")?.IntValue ?? 0;

				var ls = stats[statkey].TryGet("Last seen");
				if (null != ls && !ls.IsEmpty)
				{
					var stamp = long.MinValue;
					try
					{
						stamp = ls.GetValue<long>();
						LastSeen = stamp.Unixstamp();
					}
					catch { /* NOP */ }
				}
			}
		}

		public void SaveStats()
		{
			var stats = Taskmaster.LoadConfig(statfile);

			// BROKEN?
			string key = null;
			if (Executable != null) key = Executable;
			else if (Path != null) key = Path;
			else return;

			if (Adjusts > 0)
			{
				stats[key]["Adjusts"].IntValue = Adjusts;
				Taskmaster.MarkDirtyINI(stats);
			}

			if (LastSeen != DateTime.MinValue)
			{
				stats[key]["Last seen"].SetValue(LastSeen.Unixstamp());
				Taskmaster.MarkDirtyINI(stats);
			}
		}

		HashSet<int> PausedIds = new HashSet<int>();

		public bool BackgroundPowerdown { get; set; } = true;
		public ProcessPriorityClass BackgroundPriority { get; set; } = ProcessPriorityClass.Normal;
		/// <summary>
		/// Pause the specified foreground process.
		/// </summary>
		public void Pause(ProcessEx info)
		{
			if (PausedIds.Contains(info.Id)) return;
			// throw new InvalidOperationException(string.Format("{0} already paused", info.Name));

			if (Taskmaster.DebugForeground && Taskmaster.Trace)
				Log.Debug("[{Name}] Quelling {Exec} (#{Pid})", FriendlyName, info.Name, info.Id);

			// PausedState.Affinity = Affinity;
			// PausedState.Priority = Priority;
			// PausedState.PowerMode = PowerPlan;

			try
			{
				info.Process.PriorityClass = BackgroundPriority;
			}
			catch
			{
			}
			// info.Process.ProcessorAffinity = OriginalState.Affinity;

			if (Taskmaster.PowerManagerEnabled)
			{
				if (PowerPlan != PowerInfo.PowerMode.Undefined && BackgroundPowerdown)
				{
					if (Taskmaster.DebugPower)
						Log.Debug("<Process> [{Name}] {Exec} (#{Pid}) background power down",
							FriendlyName, info.Name, info.Id);

					UndoPower(info);
				}
			}

			if (Taskmaster.DebugForeground)
				Log.Debug("[{FriendlyName}] {Exec} (#{Pid}) priority reduced: {Current}→{Paused} [Background]",
					FriendlyName, info.Name, info.Id, Priority, BackgroundPriority);

			PausedIds.Add(info.Id);
		}

		public bool isPaused(ProcessEx info) => PausedIds.Contains(info.Id);

		public void Resume(ProcessEx info)
		{
			if (!PausedIds.Contains(info.Id)) return;
			// throw new InvalidOperationException(string.Format("{0} not paused", info.Name));

			if (info.Process.PriorityClass.ToInt32() != Priority.ToInt32())
			{
				try
				{
					info.Process.PriorityClass = Priority;
					if (Taskmaster.DebugForeground)
						Log.Debug("[{FriendlyName}] {Exec} (#{Pid}) priority restored: {Paused}→{Restored} [Foreground]",
										FriendlyName, info.Name, info.Id, BackgroundPriority, Priority);
				}
				catch
				{
					// should only happen if the process is already gone
				}
			}
			// PausedState.Priority = Priority;
			// PausedState.PowerMode = PowerPlan;

			if (Taskmaster.PowerManagerEnabled)
			{
				if (PowerPlan != PowerInfo.PowerMode.Undefined && BackgroundPowerdown)
				{
					if (Taskmaster.DebugPower)
						Log.Debug("<Process> [{Name}] {Exec} (#{Pid}) foreground power on",
							FriendlyName, info.Name, info.Id);

					SetPower(info);
				}
			}

			PausedIds.Remove(info.Id);
		}

		/// <summary>
		/// How many times we've touched associated processes.
		/// </summary>
		public int Adjusts { get; set; } = 0;
		/// <summary>
		/// Last seen any associated process.
		/// </summary>
		public DateTime LastSeen { get; set; } = DateTime.MinValue;
		/// <summary>
		/// Last modified any associated process.
		/// </summary>
		public DateTime LastTouch { get; set; } = DateTime.MinValue;

		/*
		public bool Children = false;
		public ProcessPriorityClass ChildPriority = ProcessPriorityClass.Normal;
		public bool ChildPriorityReduction = false;
		*/

		// -----------------------------------------------

		void ProcessEnd(object sender, EventArgs ev)
		{
		}

		// -----------------------------------------------

		bool SetPower(ProcessEx info)
		{
			if (!Taskmaster.PowerManagerEnabled) return false;
			if (PowerPlan == PowerInfo.PowerMode.Undefined) return false;
			Taskmaster.powermanager.SaveMode();

			Taskmaster.processmanager.WaitForExit(info); // need nicer way to signal this

			return Taskmaster.powermanager.Force(PowerPlan, info.Id);
		}

		void UndoPower(ProcessEx info) => Taskmaster.powermanager.Release(info.Id);

		/*
		// Windows doesn't allow setting this for other processes
		bool SetBackground(Process process)
		{
			return SetIOPriority(process, PriorityTypes.PROCESS_MODE_BACKGROUND_BEGIN);
		}
		*/

		public static bool SetIOPriority(Process process, NativeMethods.PriorityTypes priority)
		{
			try
			{
				var rv = NativeMethods.SetPriorityClass(process.Handle, (uint)priority);
				return rv;
			}
			catch { /* NOP, don't care */ }
			return false;
		}

		// TODO: Deal with combo path+exec
		public async void Touch(ProcessEx info, bool schedule_next = false, bool recheck = false)
		{
			Debug.Assert(info.Process != null, "ProcessController.Touch given null process.");
			Debug.Assert(info.Id > 4, "ProcessController.Touch given invalid process ID");
			Debug.Assert(!string.IsNullOrEmpty(info.Name), "ProcessController.Touch given empty process name.");

			bool foreground = Taskmaster.activeappmonitor?.Foreground.Equals(info.Id) ?? true;

			info.PowerWait = (PowerPlan != PowerInfo.PowerMode.Undefined);
			info.ActiveWait = ForegroundOnly;

			if (info.Process == null || info.Id <= 4 || string.IsNullOrEmpty(info.Name))
			{
				Log.Fatal("ProcessController.Touch({Name},#{Pid}) received invalid arguments.", info.Name, info.Id);
				throw new ArgumentNullException();
				// return ProcessState.Invalid;
			}

			if (!recheck || ModifyDelay > 0)
				await Task.Delay(recheck ? 0 : ModifyDelay).ConfigureAwait(false);

			if (recheck || ModifyDelay > 0)
			{
				try
				{
					info.Process.Refresh();
				}
				catch
				{
					Log.Warning("[{FriendlyName}] {Exec} (#{Pid}) failed to refresh.", FriendlyName, info.Name, info.Id);
				}
			}

			/*
			ProcessPriorityClass oldPriority = process.PriorityClass;

			bool rv = process.SetLimitedPriority(Priority, Increase, Decrease);
			LastSeen = DateTime.Now;
			Adjusts += 1;
			*/

			try
			{
				if (info.Process.HasExited)
				{
					if (Taskmaster.DebugProcesses)
						Log.Debug("[{FriendlyName}] {ProcessName} (#{ProcessID}) has already exited.", FriendlyName, info.Name, info.Id);
					return; // return ProcessState.Invalid;
				}
			}
			catch (Win32Exception ex)
			{
				if (ex.NativeErrorCode != 5)
					Log.Warning("[{FriendlyName}] {ProcessName} (#{ProcessID}) access failure determining if it's still running.", FriendlyName, info.Name, info.Id);
				return; // return ProcessState.Error; // we don't care what this error is exactly
			}

			if (Taskmaster.Trace) Log.Verbose("[{FriendlyName}] Touching: {ExecutableName} (#{ProcessID})", FriendlyName, info.Name, info.Id);

			var rv = ProcessState.Invalid;

			if (IgnoreList != null && IgnoreList.Contains(info.Name, StringComparer.InvariantCultureIgnoreCase))
			{
				if (Taskmaster.ShowInaction && Taskmaster.DebugProcesses)
					Log.Debug("[{FriendlyName}] {Exec} (#{ProcessID}) ignored due to user defined rule.", FriendlyName, info.Name, info.Id);
				return; // return ProcessState.Ignored;
			}

			var denyChange = ProcessManager.ProtectedProcessName(info.Name);
			if (denyChange && Taskmaster.ShowInaction && Taskmaster.DebugProcesses)
				Log.Debug("[{FriendlyName}] {ProcessName} (#{ProcessID}) in protected list, limiting tampering.", FriendlyName, info.Name, info.Id);

			// TODO: Validate path.
			if (Path != null)
			{
				if (info.Path == null)
				{
					if (ProcessManagerUtility.FindPath(info))
					{
						// Yay
					}
					else
						return; // return ProcessState.Error;
				}

				if (info.Match || info.Path.StartsWith(Path, Taskmaster.CaseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase)) // FIXME: this is done twice
				{
					// OK
					if (Taskmaster.DebugPaths && !info.Match)
						Log.Verbose("[{PathFriendlyName}] (Touch) Matched at: {Path}", FriendlyName, info.Path);

					info.Match = true;
				}
				else
				{
					if (Taskmaster.DebugPaths)
						Log.Verbose("[{PathFriendlyName}] {ExePath} NOT IN {Path} – IGNORING", FriendlyName, info.Path, Path);
					return; // return ProcessState.Ignored;
				}
			}

			bool mAffinity = false, mPriority = false, mPower = false, modified = false;
			LastSeen = DateTime.Now;

			var oldAffinity = IntPtr.Zero;
			var oldPriority = ProcessPriorityClass.RealTime;
			try
			{
				oldAffinity = info.Process.ProcessorAffinity;
				oldPriority = info.Process.PriorityClass;
			}
			catch { /* NOP, don't care */ }

			var newPriority = oldPriority;

			if (!denyChange)
			{
				if (!foreground && ForegroundOnly)
				{
					if (Taskmaster.DebugForeground)
						Log.Debug("{Exec} (#{Pid}) not in foreground, not prioritizing.", info.Name, info.Id);

					if (!PausedIds.Contains(info.Id))
						PausedIds.Add(info.Id);
					// NOP
				}
				else
				{
					try
					{
						if (info.Process.SetLimitedPriority(Priority, Increase, Decrease))
						{
							modified = mPriority = true;
							newPriority = Priority;
						}
					}
					catch
					{
						if (Taskmaster.ShowInaction)
							Log.Warning("[{FriendlyName}] {Exec} (#{Pid}) failed to set process priority.", FriendlyName, info.Name, info.Id);
						// NOP
					}
				}
			}
			else
			{
				if (Taskmaster.ShowInaction)
					Log.Verbose("{Exec} (#{Pid}) protected.", info.Name, info.Id);
			}

			try
			{
				var oldAffinityMask = info.Process.ProcessorAffinity.ToInt32();
				var newAffinityMask = Affinity.ToInt32();
				if (oldAffinityMask != newAffinityMask)
				{
					/*
					var taff = Affinity;
					if (AllowedCores || !Increase)
					{
						var minaff = Bit.Or(newAffinityMask, oldAffinityMask);
						var mincount = Bit.Count(minaff);
						var bitsold = Bit.Count(oldAffinityMask);
						var bitsnew = Bit.Count(newAffinityMask);
						var minaff1 = minaff;
						minaff = Bit.Fill(minaff, bitsnew, Math.Min(bitsold, bitsnew));
						if (minaff1 != minaff)
						{
							Console.WriteLine("--- Affinity | Core Shift ---");
							Console.WriteLine(Convert.ToString(minaff1, 2).PadLeft(ProcessManager.CPUCount));
							Console.WriteLine(Convert.ToString(minaff, 2).PadLeft(ProcessManager.CPUCount));
						}
						else
						{
							Console.WriteLine("--- Affinity | Meh ---");
							Console.WriteLine(Convert.ToString(Affinity.ToInt32(), 2).PadLeft(ProcessManager.CPUCount));
							Console.WriteLine(Convert.ToString(minaff, 2).PadLeft(ProcessManager.CPUCount));
						}

						// shuffle cores from old to new
						taff = new IntPtr(minaff);
					}
					*/
					// int bitsnew = Bit.Count(newAffinityMask);
					// TODO: Somehow shift bits old to new if there's free spots

					info.Process.ProcessorAffinity = Affinity;
					modified = mAffinity = true;
					// Log.Verbose("Affinity for '{ExecutableName}' (#{ProcessID}) set: {OldAffinity} → {NewAffinity}.",
					// execname, pid, process.ProcessorAffinity.ToInt32(), Affinity.ToInt32());
				}
				else
				{
					// Log.Verbose("Affinity for '{ExecutableName}' (#{ProcessID}) is ALREADY set: {OldAffinity} → {NewAffinity}.",
					// 			info.Name, info.Id, info.Process.ProcessorAffinity.ToInt32(), Affinity.ToInt32());
				}
			}
			catch
			{
				if (Taskmaster.ShowInaction)
					Log.Warning("[{FriendlyName}] {Exec} (#{Pid}) failed to set process affinity.", FriendlyName, info.Name, info.Id);
			}

			/*
			if (BackgroundIO)
			{
				try
				{
					//Process.EnterDebugMode(); // doesn't help

					//mBGIO = SetBackground(info.Process); // doesn't work, can only be done to current process

					//Process.LeaveDebugMode();
				}
				catch
				{
					// NOP, don't caree
				}
				if (mBGIO == false)
					Log.Warning("[{FriendlyName}] {Exec} (#{Pid}) Failed to set background I/O mode.", FriendlyName, info.Name, info.Id);
			}
			*/

			//var oldPP = PowerInfo.PowerMode.Undefined;
			if (Taskmaster.PowerManagerEnabled && PowerPlan != PowerInfo.PowerMode.Undefined)
			{
				if (!foreground && BackgroundPowerdown)
				{
					if (Taskmaster.DebugForeground)
						Log.Debug("{Exec} (#{Pid}) not in foreground, not powering up.", info.Name, info.Id);

					if (!PausedIds.Contains(info.Id))
						PausedIds.Add(info.Id);
				}
				else
				{
					//oldPP = Taskmaster.powermanager.CurrentMode;
					mPower = SetPower(info);
					//mPower = (oldPP != Taskmaster.powermanager.CurrentMode);
				}
			}

			var sbs = new System.Text.StringBuilder();
			sbs.Append("[").Append(FriendlyName).Append("] ").Append(info.Name).Append(" (#").Append(info.Id).Append(")");

			if (mPriority || mAffinity)
			{
				Statistics.TouchCount++;
				Adjusts += 1; // don't increment on power changes
			}

			if (modified)
			{
				if (mPriority)
				{
					try
					{
						info.Process.Refresh();
						newPriority = info.Process.PriorityClass;
						if (newPriority.ToInt32() != Priority.ToInt32())
						{
							Log.Warning("[{FriendlyName}] {Exe} (#{Pid}) Post-mortem of modification: FAILURE (Expected: {TgPrio}, Detected: {CurPrio}).",
										FriendlyName, info.Name, info.Id, Priority.ToString(), newPriority.ToString());
						}
					}
					catch { }// NOP, don't caree
				}

				LastTouch = DateTime.Now;

				rv = ProcessState.Modified;
				ScanModifyCount++;
			}

			sbs.Append("; Priority: ");
			if (mPriority)
				sbs.Append(oldPriority.ToString()).Append(" → ");
			sbs.Append(newPriority.ToString());
			if (denyChange) sbs.Append(" [Protected]");
			sbs.Append("; Affinity: ");
			if (mAffinity)
				sbs.Append(oldAffinity.ToInt32()).Append(" → ");
			sbs.Append(Affinity.ToInt32());
			if (mPower)
				sbs.Append(string.Format(" [Power Mode: {0}]", PowerPlan.ToString()));

			if (modified)
			{
				if (Taskmaster.DebugProcesses)
					Log.Information(sbs.ToString());
			}
			else
			{
				// if (DateTime.Now - LastSeen
				sbs.Append(" – looks OK, not touched.");
				if (Taskmaster.ShowInaction && Taskmaster.DebugProcesses)
					Log.Debug(sbs.ToString());
				// else
				// 	Log.Verbose(sbs.ToString());
				rv = ProcessState.OK;
			}

			TryResize(info);

			if (modified)
			{
				onTouch?.Invoke(this, new ProcessEventArgs { Control = this, Info = info });
			}

			if (schedule_next) TryScan();

			if (Recheck > 0 && recheck == false)
			{
				Task.Factory.StartNew(() => TouchReapply(info), TaskCreationOptions.PreferFairness);
			}

			return; // return rv;
		}

		NativeMethods.RECT rect = new NativeMethods.RECT();

		void TryResize(ProcessEx info)
		{
			if (Resize == null) return;

			try
			{
				lock (ResizeWaitList_lock)
				{
					if (ResizeWaitList.Contains(info.Id)) return;

					IntPtr hwnd = info.Process.MainWindowHandle;
					NativeMethods.GetWindowRect(hwnd, ref rect);

					var ro = new System.Drawing.Rectangle(
						rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

					var rn = new System.Drawing.Rectangle(
						Resize[0] != 0 ? Resize[0] : ro.Left,
						Resize[1] != 0 ? Resize[1] : ro.Top,
						Resize[2] != 0 ? Resize[2] : ro.Width,
						Resize[3] != 0 ? Resize[3] : ro.Height
						);

					if (!rn.Equals(ro))
					{
						Log.Debug("Resizing {Name} (#{Pid}) from {OldWidth}×{OldHeight} to {NewWidth}×{NewHeight}",
							info.Name, info.Id, ro.Width, ro.Height, rn.Width, rn.Height);

						// TODO: Add option to monitor the app and save the new size so relaunching the app keeps the size.

						NativeMethods.MoveWindow(hwnd, rn.Left, rn.Top, rn.Width, rn.Height, true);
					}

					if (!RememberSize && !RememberPos) return;

					ResizeWaitList.Add(info.Id);

					System.Threading.ManualResetEvent re = new System.Threading.ManualResetEvent(false);
					Task.Factory.StartNew(() =>
					{
						try
						{
							while (!re.WaitOne(1000 * 60))
							{
								NativeMethods.GetWindowRect(hwnd, ref rect);
								rn = new System.Drawing.Rectangle(
									RememberPos ? rect.Left : Resize[0], RememberPos ? rect.Top : Resize[1],
									RememberSize ? rect.Right - rect.Left : Resize[2], RememberSize ? rect.Bottom - rect.Top : Resize[3]
									);

								Resize = new int[] { rn.Left, rn.Top, rn.Width, rn.Height };
							}
						}
						catch (Exception ex)
						{
							Logging.Stacktrace(ex);
						}
					}, TaskCreationOptions.LongRunning);

					info.Process.EnableRaisingEvents = true;
					info.Process.Exited += (s, ev) =>
					{
						try
						{
							re.Set();
							re.Reset();

							lock (ResizeWaitList_lock)
							{
								ResizeWaitList.Remove(info.Id);
							}

							Log.Debug("Saving {Name} (#{Pid}) from {OldWidth}×{OldHeight} to {NewWidth}×{NewHeight}",
								info.Name, info.Id, ro.Width, ro.Height, Resize[0], Resize[1]);

							if (ro.Width != Resize[2] || ro.Height != Resize[3]
								|| ro.Left != Resize[0] || ro.Top != Resize[1])
							{
								var cfg = Taskmaster.LoadConfig(watchlistfile);
								var app = cfg[FriendlyName];
								int[] resizecopy = new int[] { 0, 0, 0, 0 };
								Resize.CopyTo(resizecopy, 0);
								if (!RememberPos)
								{
									resizecopy[0] = 0;
									resizecopy[1] = 0;
								}
								if (!RememberSize)
								{
									resizecopy[2] = 0;
									resizecopy[3] = 0;
								}
								app["Resize"].IntValueArray = resizecopy;
								SaveConfig(cfg, app);
							}
						}
						catch (Exception ex)
						{
							Logging.Stacktrace(ex);
						}
					};
				}
			}
			catch (Exception ex)
			{
				Logging.Stacktrace(ex);

				lock (ResizeWaitList_lock)
				{
					ResizeWaitList.Remove(info.Id);
				}
			}
		}

		public bool RememberSize = false;
		public bool RememberPos = false;
		public int[] Resize = null;
		List<int> ResizeWaitList = new List<int>();
		object ResizeWaitList_lock = new object();

		async Task TouchReapply(ProcessEx info)
		{
			await Task.Delay(Math.Max(Recheck, 5) * 1000).ConfigureAwait(false);

			if (Taskmaster.DebugProcesses)
				Log.Debug("[{FriendlyName}] {Process} (#{PID}) rechecking", FriendlyName, info.Name, info.Id);

			try
			{
				if (!info.Process.HasExited)
				{
					Touch(info, schedule_next: false, recheck: true);
				}
				else
				{
					if (Taskmaster.Trace) Log.Verbose("[{FriendlyName}] {Process} (#{PID}) is gone yo.", FriendlyName, info.Name, info.Id);
				}
			}
			catch (Exception ex)
			{
				Log.Warning("[{FriendlyName}] {Process} (#{PID}) – something bad happened.", FriendlyName, info.Name, info.Id);
				Logging.Stacktrace(ex);
				return; //throw; // would throw but this is async function
			}
		}

		/// <summary>
		/// Synchronous call to RescanWithSchedule()
		/// </summary>
		public int TryScan()
		{
			RescanWithSchedule();

			return Convert.ToInt32((LastScan.AddMinutes(Rescan) - DateTime.Now).TotalMinutes); // this will produce wrong numbers
		}

		DateTime LastScan = DateTime.MinValue;

		/// <summary>
		/// Atomic lock for RescanWithSchedule()
		/// </summary>
		int ScheduledScan = 0;

		async Task RescanWithSchedule()
		{
			try
			{
				var n = (DateTime.Now - LastScan).TotalMinutes;
				// Log.Trace(string.Format("[{0}] last scan {1:N1} minute(s) ago.", FriendlyName, n));
				if (Rescan > 0 && n >= Rescan)
				{
					if (!Atomic.Lock(ref ScheduledScan)) return;

					if (Taskmaster.DebugProcesses)
						Log.Debug("[{FriendlyName}] Rescan initiating.", FriendlyName);

					using (var m = SelfAwareness.Mind(DateTime.Now.AddSeconds(15)))
						await Scan().ConfigureAwait(false);


					ScheduledScan = 0;
				}
				// else
				// 	Log.Verbose("[{FriendlyName}] Scan too recent, ignoring.", FriendlyName); // this is too spammy.
			}
			catch (Exception ex)
			{
				Logging.Stacktrace(ex);
			}
		}

		int ScanModifyCount = 0;
		public async Task Scan()
		{
			if (ExecutableFriendlyName == null) return;

			//await Task.Delay(0);

			Process[] procs;
			try
			{
				procs = Process.GetProcessesByName(ExecutableFriendlyName); // should be case insensitive by default
			}
			catch // name not found
			{
				if (Taskmaster.Trace) Log.Verbose("{FriendlyName} is not running", ExecutableFriendlyName);
				return;
			}

			// LastSeen = LastScan;
			LastScan = DateTime.Now;

			if (procs.Length == 0) return;

			if (Taskmaster.DebugProcesses)
				Log.Debug("[{FriendlyName}] Scanning found {ProcessInstances} instance(s)", FriendlyName, procs.Length);

			ScanModifyCount = 0;
			foreach (Process process in procs)
			{
				string name;
				int pid;
				try
				{
					name = process.ProcessName;
					pid = process.Id;
				}
				catch
				{
					continue; // shouldn't happen
				}

				Touch(new ProcessEx { Name = name, Id = pid, Process = process, Path = null });
			}

			if (ScanModifyCount > 0)
			{
				if (Taskmaster.DebugProcesses)
					Log.Verbose("[{ProcessFriendlyName}] Scan modified {ModifiedInstances} out of {ProcessInstances} instance(s)",
								FriendlyName, ScanModifyCount, procs.Length);
			}
		}

		public bool Locate()
		{
			if (Path != null)
			{
				if (System.IO.Directory.Exists(Path)) return true;
				return false;
			}
			return false;
		}

		public static event EventHandler<ProcessEventArgs> onTouch;
		public static event EventHandler<PathControlEventArgs> onLocate;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		bool disposed; // = false;
		void Dispose(bool disposing)
		{
			if (disposed) return;

			if (disposing)
			{
				if (Taskmaster.Trace) Log.Verbose("Disposing process controller [{FriendlyName}]", FriendlyName);
			}

			disposed = true;
		}
	}

	sealed public class PathControlEventArgs : EventArgs
	{
	}

	sealed public class ProcessEventArgs : EventArgs
	{
		public ProcessController Control { get; set; }
		public ProcessEx Info;
		public enum ProcessState
		{
			Starting,
			Found,
			Reduced,
			Restored,
			Cancel,
			Exiting,
			Undefined
		}
		public ProcessState State;
	}
}