﻿//
// MainWindow.cs
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

namespace TaskMaster
{
	using System;
	using System.Collections.Generic;
	using System.Windows.Forms;

	public class MainWindow : System.Windows.Forms.Form
	{
		static NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public void ShowConfigRequest(object sender, System.EventArgs e)
		{
			Log.Warn("User wanted to config TaskMaster!");
		}

		void Save()
		{
			TaskMaster.saveConfig("Core.ini", TaskMaster.cfg);
		}

		void FastCleanup()
		{
			//stop
			Save();
		}

		void ExitCleanup()
		{
			Log.Trace("Cleaning...");
			Save();

			Hide();

			//Enabled = false;// mono.nexe hangs if disabled, so...
			Application.Exit();
		}

		public void ExitRequest(object sender, System.EventArgs e)
		{
			ExitCleanup();
		}

		void WindowClose(object sender, FormClosingEventArgs e)
		{
			Log.Trace("Window close");
			switch (e.CloseReason)
			{
				case CloseReason.UserClosing:
					// X was pressed or similar, we're just hiding to tray.
					// TODO: Destroy the window and save memory
					if (!lowmemory)
					{
						Log.Debug("Hiding window, keeping in memory.");
						e.Cancel = true;
						Hide();
					}
					else
					{
						Log.Debug("Closing window, freeing memory.");
					}
					break;
				case CloseReason.WindowsShutDown:
					Log.Info("Exit: Windows shutting down.");
					goto Cleanup;
				case CloseReason.TaskManagerClosing:
					Log.Info("Exit: Task manager told us to close.");
					goto Cleanup;
				case CloseReason.ApplicationExitCall:
					Log.Info("Exit: User asked to close.");
					goto Cleanup;
				default:
					Log.Warn(System.String.Format("Exit: Unidentified close reason: {0}", e.CloseReason));
				Cleanup:
					ExitCleanup();
					Application.Exit();
					break;
			}
		}

		// this restores the main window to a place where it can be easily found if it's lost
		public void RestoreWindowRequest(object sender, System.EventArgs e)
		{
			CenterToScreen();
			SetTopLevel(true); // this doesn't Keep it topmost, does it?
			TopMost = true;
			// toggle because we don't want to keep it there
			TopMost = false;
		}

		public void ShowWindowRequest(object sender, EventArgs e)
		{
			Show(); // FIXME: Gets triggered when menuitem is clicked
		}

		#region Microphone control code
		MicMonitor micMonitor = null;
		public void setMicMonitor(MicMonitor micmonitor)
		{
			Log.Trace("Hooking microphone monitor.");
			micMonitor = micmonitor;
			micName.Text = micMonitor.DeviceName;
			corCountLabel.Text = micMonitor.getCorrections().ToString();
			micMonitor.VolumeChanged += volumeChangeDetected;
			micVol.Value = System.Convert.ToInt32(micMonitor.Volume);
			MicEnum();
		   // TODO: Hook device changes
		}

		public void ProcAdjust(object sender, ProcessEventArgs e)
		{
			Log.Trace("Process adjust received.");
			ListViewItem item;
			if (appc.TryGetValue(e.Control.Executable, out item))
			{
				item.SubItems[5].Text = e.Control.Adjusts.ToString();
				item.SubItems[6].Text = e.Control.lastSeen.ToString();
			}
			else
			{
				Log.Error(System.String.Format("{0} not found in app list.", e.Control.Executable));
			}
		}

		GameMonitor gamemon = null;
		public void setGameMonitor(GameMonitor gamemonitor)
		{
			gamemon = gamemonitor;
			gamemon.ActiveChanged += OnActiveWindowChanged;
		}

		public void OnActiveWindowChanged(object sender, WindowChangedArgs e)
		{
			activeLabel.Text = "Active window: " + e.title;
		}

		ProcessManager procCntrl = null;
		public void setProcControl(ProcessManager control)
		{
			procCntrl = control;
			//control.onProcAdjust += ProcAdjust;
			foreach (ProcessControl item in control.images)
			{

				ListViewItem litem = new ListViewItem(new string[] {
					item.FriendlyName.ToString(),
					item.Executable,
					item.Priority.ToString(),
						(item.Affinity.ToInt32() == ProcessManager.allCPUsMask ? "OS controlled" : Convert.ToString(item.Affinity.ToInt32(), 2)),
					item.Boost.ToString(),
					item.Adjusts.ToString(),
					item.lastSeen != System.DateTime.MinValue ? item.lastSeen.ToString() : "Never"
				});
				appc.Add(item.Executable, litem);
				appList.Items.Add(litem);
			}

			foreach (PathControl path in procCntrl.ActivePaths())
			{
				ListViewItem ni = new ListViewItem(new string[] { path.FriendlyName, path.Path, path.Adjusts.ToString() });
				appw.Add(path, ni);
				pathList.Items.Add(ni);
			}

			procCntrl.onPathLocated += PathLocatedEvent;
			procCntrl.onPathAdjust += PathAdjustEvent;
		}

		public void PathAdjustEvent(object sender, PathControlEventArgs e)
		{
			ListViewItem ni;
			if (appw.TryGetValue(e.Control, out ni))
				ni.SubItems[2].Text = e.Control.Adjusts.ToString();
		}

		public void PathLocatedEvent(object sender, PathControlEventArgs e)
		{
			Log.Trace(e.Control.FriendlyName + " // " + e.Control.Path);
			ListViewItem ni = new ListViewItem(new string[] { e.Control.FriendlyName, e.Control.Path, "0" });
			try
			{
				appw.Add(e.Control, ni);
				pathList.Items.Add(ni);
			}
			catch (Exception)
			{
				// FIXME: This happens mostly because Application.Run() is triggered after we do ProcessEverything() and the events are processed only after
				Log.Warn("[Expected] Superfluous path watch update: " + e.Control.FriendlyName);
			}
		}

		Label micName;
		NumericUpDown micVol;
		ListView micList;
		ListView appList;
		ListView pathList;
		Dictionary<PathControl, ListViewItem> appw = new Dictionary<PathControl, ListViewItem>();
		Dictionary<string, ListViewItem> appc = new Dictionary<string, ListViewItem>();
		Label corCountLabel;

		void UserMicVol(object sender, System.EventArgs e)
		{
			// TODO: Handle volume changes. Not really needed. Give presets?
			//micMonitor.setVolume(micVol.Value);
		}

		void volumeChangeDetected(object sender, VolumeChangedEventArgs e)
		{
			micVol.Value = System.Convert.ToInt32(e.New);
			if(e.Corrected)
			{
				corCountLabel.Text = micMonitor.getCorrections().ToString();
				//corCountLabel.Refresh();
			}
		}

		void MicEnum()
		{
			// hopefully this creates temp variable instead of repeatedly calling the func...
			foreach (KeyValuePair<string, string> dev in micMonitor.enumerate())
				micList.Items.Add(new ListViewItem(new string[] { dev.Value, dev.Key }));
		}
		#endregion // Microphone control code

		#region Game Monitor
		Label activeLabel = null;
		#endregion

		#region Internet handling functionality
		/*
		bool netAvailable = false;
		Label inetLabel = null;
		void NetworkChanged(object sender, System.EventArgs e)
		{
			netAvailable = NetworkInterface.GetIsNetworkAvailable();
			inetLabel.Text = netAvailable ? "Network of tubes connected." : "Tubes broken.";
		}
		*/

		/*
		string netSpeed(long speed)
		{
			if (speed >= 1000000000d)
				return Math.Round(speed / 1000000000d, 2) + " Gb/s";
			else if (speed >= 1000000)
				return Math.Round(speed / 1000000d, 2) + " Mb/s";
			else if (speed >= 1000)
				return Math.Round(speed / 1000d, 2) + " kb/s";
			else
				return speed + " b/s";
		}
		*/

		/*
		ListView ifaceList;
		void NetEnum()
		{
			ifaceList.Items.Clear();
			NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface n in adapters)
			{
				//if (n.NetworkInterfaceType != NetworkInterfaceType.Ethernet) continue;

				ifaceList.Items.Add(new ListViewItem(new string[] { n.Name, n.NetworkInterfaceType.ToString(), n.OperationalStatus.ToString(), netSpeed(n.Speed) }));
			}
		}

		void NetAddrChanged(object sender, System.EventArgs e)
		{
			NetEnum();
		}
		*/

		/*
		void NetworkSetup()
		{
			NetworkChanged(null, null);
			//netAvailable = NetworkInterface.GetIsNetworkAvailable();
			//inetLabel.Text = netAvailable ? "Network of tubes connected." : "Tubes broken.";

			NetEnum();
			NetworkChange.NetworkAvailabilityChanged += NetworkChanged;
			NetworkChange.NetworkAddressChanged += NetAddrChanged;

			////---*
			// only recognizes changes related to Internet adapters
			if (NetworkInterface.GetIsNetworkAvailable()) {
				// however, this will include all adapters
				NetworkInterface[] interfaces =
					NetworkInterface.GetAllNetworkInterfaces();

				foreach (NetworkInterface face in interfaces) {
					// filter so we see only Internet adapters
					if (face.OperationalStatus == OperationalStatus.Up) {
						if ((face.NetworkInterfaceType != NetworkInterfaceType.Tunnel) &&
							(face.NetworkInterfaceType != NetworkInterfaceType.Loopback)) {
							IPv4InterfaceStatistics statistics =
								face.GetIPv4Statistics();

							// all testing seems to prove that once an interface
							// comes online it has already accrued statistics for
							// both received and sent...
						}
					}
				}
			}
			*---//
		}
		*/
		#endregion

		/*
		ProcessControl editproc;
		void saveAppData(Object sender, EventArgs e)
		{
			Log.Debug("Save button pressed!");
		}
		*/

		//void appEditEvent(Object sender, System.Windows.Input.MouseButtonEventArgs e)
		/*
	void appEditEvent(Object sender, EventArgs e)
	{
		if (appList.SelectedItems.Count == 1)
		{
			//ListView.SelectedListViewItemCollection items = appList.SelectedItems;
			//ListViewItem itm = items[0];
			ListViewItem.ListViewSubItemCollection sit = appList.SelectedItems[0].SubItems;

			// TODO: Find the actuall ProcessControl for this item
			editproc = procCntrl.getControl(sit[1].Text);
			if (editproc == null)
			{
				Log.Warn(sit[1].Text + " not found in process manager!");
				return;
			}

			Log.Trace(System.String.Format("[{0}] {1}", editproc.FriendlyName, editproc.Executable));

			Form f = new Form();
			f.Padding = new Padding(12);
			TableLayoutPanel lay = new TableLayoutPanel();
			lay.Parent = f;
			lay.ColumnCount = 2;
			lay.RowCount = 6;
			lay.Dock = DockStyle.Fill;
			lay.AutoSize = true;

			Label e_appname = new Label();
			e_appname.Text = "Application";
			e_appname.TextAlign = ContentAlignment.MiddleLeft;
			e_appname.Width = 120;
			lay.Controls.Add(e_appname, 0, 0);
			TextBox i_appname = new TextBox();
			i_appname.Text = editproc.FriendlyName;
			lay.Controls.Add(i_appname, 1, 0);

			Label e_exename = new Label();
			e_exename.Text = "Executable";
			e_exename.TextAlign = ContentAlignment.MiddleLeft;
			e_exename.Width = 120;
			lay.Controls.Add(e_exename, 0, 1);
			TextBox i_exename = new TextBox();
			i_exename.Text = editproc.Executable;
			lay.Controls.Add(i_exename, 1, 1);

			Label e_priority = new Label();
			e_priority.Text = "Priority";
			e_priority.TextAlign = ContentAlignment.MiddleLeft;
			lay.Controls.Add(e_priority, 0, 2);
			ComboBox i_priority = new ComboBox();
			i_priority.Items.Add(ProcessPriorityClass.AboveNormal);
			i_priority.Items.Add(ProcessPriorityClass.Normal);
			i_priority.Items.Add(ProcessPriorityClass.BelowNormal);
			i_priority.Items.Add(ProcessPriorityClass.Idle);
			i_priority.SelectedText = editproc.Priority.ToString();
			lay.Controls.Add(i_priority, 1, 2);

			Label e_affinity = new Label();
			e_affinity.Text = "Affinity";
			e_affinity.TextAlign = ContentAlignment.MiddleLeft;
			lay.Controls.Add(e_affinity, 0, 3);


			Label e_boost = new Label();
			e_boost.Text = "Boost";
			e_boost.TextAlign = ContentAlignment.MiddleLeft;
			lay.Controls.Add(e_boost, 0, 4);
			CheckBox i_boost = new CheckBox();
			i_boost.Checked = editproc.Boost;
			//Log.Debug("Boost:"+sit[4]);
			lay.Controls.Add(i_boost, 1, 4);

			Button savebut = new Button();
			savebut.Text = "Save";
			lay.Controls.Add(savebut, 1, 5);

			savebut.Click += saveAppData;

			f.MinimumSize = new Size(200,120);
			f.ShowDialog(this);
		}
	}
		*/

		void BuildUI()
		{
			Text = Application.ProductName;
			AutoSize = true;
			Padding = new Padding(12);
			Size = new System.Drawing.Size(720, 580);
			//margin

			TableLayoutPanel lrows = new TableLayoutPanel();
			lrows.Parent = this;
			lrows.ColumnCount = 1;
			//lrows.RowCount = 10;
			lrows.Dock = DockStyle.Fill;

			#region Main Window Row 1, microphone device
			Label micDevLbl = new Label();
			micDevLbl.Text = "Default communications device:";
			micDevLbl.Dock = DockStyle.Left;
			micDevLbl.AutoSize = true;
			TableLayoutPanel micNameRow = new TableLayoutPanel();
			micNameRow.Dock = DockStyle.Top;
			micNameRow.RowCount = 1;
			micNameRow.ColumnCount = 2;
			micNameRow.BackColor = System.Drawing.Color.BlanchedAlmond; // DEBUG
			micNameRow.Controls.Add(micDevLbl, 0, 0);
			micName = new Label();
			micName.Dock = DockStyle.Left;
			micName.AutoSize = true;
			micNameRow.Controls.Add(micName, 1, 0);
			micNameRow.Dock = DockStyle.Fill;
			micNameRow.AutoSize = true;
			//lrows.Controls.Add(micNameRow, 0, 0);
			lrows.Controls.Add(micNameRow);
			#endregion

			// uhh???
			// Main Window Row 2, volume control
			TableLayoutPanel miccntrl = new TableLayoutPanel();
			miccntrl.ColumnCount = 5;
			miccntrl.RowCount = 1;
			miccntrl.BackColor = System.Drawing.Color.Azure; // DEBUG
			miccntrl.Dock = DockStyle.Top;
			miccntrl.AutoSize = true;
			//miccntrl.Location = new System.Drawing.Point(0, 0);
			miccntrl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			miccntrl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			//lrows.Controls.Add(miccntrl, 0, 1);
			lrows.Controls.Add(miccntrl);

			Label micVolLabel = new Label();
			micVolLabel.Text = "Mic volume";
			micVolLabel.Dock = DockStyle.Left;
			micVolLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

			Label micVolLabel2 = new Label();
			micVolLabel2.Text = "%";
			micVolLabel2.Dock = DockStyle.Left;
			micVolLabel2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

			micVol = new NumericUpDown();
			micVol.Maximum = 100;
			micVol.Minimum = 0;
			micVol.Width = 60;
			micVol.ReadOnly = true;
			micVol.Enabled = false;
			micVol.ValueChanged += UserMicVol;
			micVol.Dock = DockStyle.Left;

			miccntrl.Controls.Add(micVolLabel, 0, 0);
			miccntrl.Controls.Add(micVol, 1, 0);
			miccntrl.Controls.Add(micVolLabel2, 2, 0);

			Label corLbll = new Label();
			corLbll.Text = "Correction count:";
			corLbll.Dock = DockStyle.Fill;
			corLbll.AutoSize = true;
			corLbll.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			corCountLabel = new Label();
			corCountLabel.Dock = DockStyle.Left;
			corCountLabel.Text = "0";
			corCountLabel.AutoSize = true;
			corCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			miccntrl.Controls.Add(corLbll, 3, 0);
			miccntrl.Controls.Add(corCountLabel, 4, 0);
			// End: Volume control

			// Main Window row 3, microphone device enumeration
			micList = new ListView();
			micList.Dock = DockStyle.Top;
			micList.Width = lrows.Width - 3; // FIXME: 3 for the bevel, but how to do this "right"?
			micList.Height = 60;
			micList.View = View.Details;
			micList.FullRowSelect = true;
			micList.Columns.Add("Name", 200);
			micList.Columns.Add("GUID", 220);

			//lrows.Controls.Add(micList, 0, 2);
			lrows.Controls.Add(micList);
			// End: Microphone enumeration

			// Main Window row 4-5, internet status
			/*
			inetLabel = new Label();
			inetLabel.Dock = DockStyle.Top;
			inetLabel.Text = "Uninitialized";
			inetLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			inetLabel.AutoSize = true;
			inetLabel.BackColor = Color.LightGoldenrodYellow;
			lrows.Controls.Add(inetLabel, 0, 3);
			*/

			/*
			activeLabel = new Label();
			activeLabel.Dock = DockStyle.Top;
			activeLabel.Text = "no active window found";
			activeLabel.AutoSize = true;
			activeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			activeLabel.BackColor = Color.Aquamarine;
			lrows.Controls.Add(activeLabel, 0, 4);
			*/

			/*
			ifaceList = new ListView();
			ifaceList.Dock = DockStyle.Top;
			ifaceList.Width = lrows.Width - 3; // FIXME: why does 3 work? can't we do this automatically?
			ifaceList.View = View.Details;
			ifaceList.FullRowSelect = true;
			ifaceList.Columns.Add("Device", 220);
			ifaceList.Columns.Add("Type", 80);
			ifaceList.Columns.Add("Status", 60);
			ifaceList.Columns.Add("Link speed", 80);
			ifaceList.Scrollable = true;
			lrows.Controls.Add(ifaceList, 0, 5);
			*/
			// End: Inet status

			// Main Window row 6, settings
			/*
			ListView settingList = new ListView();
			settingList.Dock = DockStyle.Top;
			settingList.Width = lrows.Width - 3; // FIXME: why does 3 work? can't we do this automatically?
			settingList.View = View.Details;
			settingList.FullRowSelect = true;
			settingList.Columns.Add("Key", 80);
			settingList.Columns.Add("Value", 220);
			settingList.Scrollable = true;
			Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
			if (config.AppSettings.Settings.Count > 0)
			{
				foreach (var key in config.AppSettings.Settings.AllKeys)
				{
					settingList.Items.Add(new ListViewItem(new string[] { key, config.AppSettings.Settings[key].Value }));
				}
			}
			lrows.Controls.Add(settingList, 0, 6);
			*/
			// End: Settings

			// Main Window, Path list
			pathList = new ListView();
			pathList.View = View.Details;
			pathList.Dock = DockStyle.Top;
			pathList.Width = lrows.Width - 3;
			pathList.Height = 60;
			pathList.FullRowSelect = true;
			pathList.Columns.Add("Name", 120);
			pathList.Columns.Add("Path", 300);
			pathList.Columns.Add("Adjusts", 60);
			pathList.Scrollable = true;
			lrows.Controls.Add(pathList);
			// End: Path list

			// Main Window row 7, app list
			appList = new ListView();
			appList.View = View.Details;
			appList.Dock = DockStyle.Top;
			appList.Width = lrows.Width - 3; // FIXME: why does 3 work? can't we do this automatically?
			appList.Height = 140; // FIXME: Should use remaining space
			appList.FullRowSelect = true;
			appList.Columns.Add("Name", 100);
			appList.Columns.Add("Executable", 140);
			appList.Columns.Add("Priority", 80);
			appList.Columns.Add("Affinity", 80);
			appList.Columns.Add("Boost", 40);
			appList.Columns.Add("Adjusts", 60);
			appList.Columns.Add("Last seen", 120);
			appList.Scrollable = true;
			appList.Alignment = ListViewAlignment.Left;
			//appList.DoubleClick += appEditEvent; // for in-app editing, probably not going to actually do that
			//lrows.Controls.Add(appList, 0, 7);
			lrows.Controls.Add(appList);
			// End: App list

			// UI Log
			loglist = new ListView();
			loglist.Dock = DockStyle.Fill;
			loglist.View = View.Details;
			loglist.FullRowSelect = true;
			loglist.Columns.Add("Log content");
			loglist.Columns[0].Width = lrows.Width - 25;
			loglist.HeaderStyle = ColumnHeaderStyle.None;
			loglist.Scrollable = true;
			loglist.Height = 100;
			lrows.Controls.Add(loglist, 0, 11);
			// End: UI Log

			//layout.Visible = true;

			/*
			Label micVolLabel = new Label();
			micVolLabel.Parent = micPanel;
			micVolLabel.AutoSize = true;
			//micVolLabel.Location = Location.
			*/

			/*
			TrackBar tb = new TrackBar();
			tb.Parent = micPanel;
			tb.TickStyle = TickStyle.Both;
			//tb.Size = new Size(150, 25);
			//tb.Height 
			tb.Dock = DockStyle.Fill; // fills parent
									  //tb.Location = new Point(0, 0); // insert point
			*/
		}

		MemLog memlog;
		ListView loglist;
		public void setLog(MemLog log)
		{
			memlog = log;
			Log.Trace("Filling GUI log.");
			foreach (string msg in memlog.Logs.ToArray())
				loglist.Items.Add(msg);
			memlog.OnNewLog += onNewLog;
		}

		// DO NOT LOG INSIDE THIS FOR FUCKS SAKE
		// it creates an infinite log loop
		int MaxLogSize = 20;
		void onNewLog(object sender, LogEventArgs e)
		{
			try
			{
				while (loglist.Items.Count > MaxLogSize)
					loglist.Items.RemoveAt(0);
			}
			catch (System.NullReferenceException)
			{
				Log.Warn("Couldn't remove old log entries from GUI."); // POSSIBLY REALLY BAD IDEA
				System.Console.WriteLine("ERROR: Null reference");
			}

			loglist.Items.Add(e.Message).EnsureVisible();
		}

		static bool onetime = false;
		static bool lowmemory = false;

		// constructor
		public MainWindow()
		{
			if (!onetime)
			{
				SharpConfig.Configuration cfg = TaskMaster.loadConfig("Core.ini");
				if (cfg.Contains("Options") && cfg["Options"].Contains("Low memory"))
				{
					lowmemory = cfg["Options"]["Low memory"].BoolValue;
					Log.Info("Low memory mode enabled.");
				}
				onetime = true;
			}

			FormClosing += WindowClose;

			//MakeTrayIcon();

			BuildUI();

			//DISABLED
			//NetworkSetup();

			// TODO: Detect mic device changes
			// TODO: Delay fixing by 5 seconds to prevent fix diarrhea

			// the form itself
			WindowState = FormWindowState.Normal;
			FormBorderStyle = FormBorderStyle.FixedDialog; // no min/max buttons as wanted
			MinimizeBox = false;
			MaximizeBox = false;
			Hide();
			//CenterToScreen();

			ProcessControl.onTouch += ProcAdjust;
		}

		/*
		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
		}
		*/

		//string cfgfile = "GUI.ini";
		//SharpConfig.Configuration guicfg;

		~MainWindow()
		{
			//TaskMaster.saveConfig(cfgfile, guicfg);

			//base.Dispose();
		}
	}
}

