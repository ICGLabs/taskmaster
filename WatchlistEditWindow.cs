﻿//
// WatchlistEditWindow.cs
//
// Author:
//       M.A. (https://github.com/mkahvi)
//
// Copyright (c) 2018 M.A.
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
using System.Diagnostics;
using System.Windows.Forms;
using Serilog;

namespace Taskmaster
{
	sealed public class WatchlistEditWindow : UI.UniForm
	{
		public ProcessController Controller;

		bool newPrc = false;

		// Adding
		public WatchlistEditWindow()
		{
			DialogResult = DialogResult.Abort;

			Controller = new ProcessController("Unnamed")
			{
				Enabled = true,
				Valid = true,
			};

			newPrc = true;

			WindowState = FormWindowState.Normal;
			FormBorderStyle = FormBorderStyle.FixedDialog; // no min/max buttons as wanted

			BuildUI();
		}

		// Editingg
		public WatchlistEditWindow(ProcessController controller)
		{
			DialogResult = DialogResult.Abort;

			Controller = controller;

			StartPosition = FormStartPosition.CenterParent;

			if (Controller == null) throw new ArgumentException(string.Format("{0} not found in watchlist.", Controller.FriendlyName));

			WindowState = FormWindowState.Normal;
			FormBorderStyle = FormBorderStyle.FixedDialog; // no min/max buttons as wanted
			MinimizeBox = false;
			MaximizeBox = false;

			BuildUI();
		}

		void SaveInfo(object sender, System.EventArgs ev)
		{
			var enOrig = Controller.Enabled;
			Controller.Enabled = false;

			// TODO: VALIDATE FOR GRIMMY'S SAKE!
			// TODO: Foreground/Powermode need to be informed of any relevant changes.

			// -----------------------------------------------
			// VALIDATE

			var fnlen = (friendlyName.Text.Length > 0);
			var exnam = (execName.Text.Length > 0);
			var path = (pathName.Text.Length > 0);

			if (!fnlen || friendlyName.Text.Contains("]") || friendlyName.Text.Contains("["))
			{
				Controller.Valid = false;
				MessageBox.Show("Friendly name is missing or includes illegal characters (such as square brackets).", "Malconfigured friendly name", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
			}

			if (!path && !exnam)
			{
				Controller.Valid = false;
				MessageBox.Show("No path nor executable defined.", "Configuration error", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
			}

			if ((rescanFreq.Value > 0) && !exnam)
			{
				Controller.Valid = false;
				MessageBox.Show("Rescan requires executable to be defined.", "Configuration error", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
			}

			var dprc = Taskmaster.processmanager.getWatchedController(friendlyName.Text);
			if (dprc != null && dprc != Controller)
			{
				Controller.Valid = false;
				MessageBox.Show("Friendly Name conflict.", "Configuration error", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
			}

			if (!Controller.Valid)
			{
				Log.Warning("[{FriendlyName}] Can't save, configuration invalid.", friendlyName.Text);
				return;
			}

			// -----------------------------------------------

			// TODO: Warn about conflicting section name
			string newfriendlyname = friendlyName.Text.Trim();
			if (!newPrc && !newfriendlyname.Equals(Controller.FriendlyName))
				Controller.DeleteConfig(); // SharpConfig doesn't seem to support renaming sections, so we delete the old one instead

			Controller.FriendlyName = newfriendlyname;
			Controller.Executable = execName.Text.Length > 0 ? execName.Text.Trim() : null;
			Controller.Path = pathName.Text.Length > 0 ? pathName.Text.Trim() : null;
			if (priorityClass.SelectedIndex == 5)
			{
				Controller.Priority = null;
				Controller.PriorityStrategy = ProcessPriorityStrategy.None;
			}
			else
			{
				Controller.Priority = ProcessHelpers.IntToPriority(priorityClass.SelectedIndex); // is this right?
				Controller.PriorityStrategy = ProcessPriorityStrategy.None;
				if (increasePrio.Checked && decreasePrio.Checked)
					Controller.PriorityStrategy = ProcessPriorityStrategy.Force;
				else if (increasePrio.Checked && !decreasePrio.Checked)
					Controller.PriorityStrategy = ProcessPriorityStrategy.Increase;
				else if (decreasePrio.Checked && !increasePrio.Checked)
					Controller.PriorityStrategy = ProcessPriorityStrategy.Decrease;
			}

			if (affstrategy.SelectedIndex != 0)
			{
				if (cpumask == -1)
					Controller.Affinity = null;
				else
				{
					Controller.Affinity = new IntPtr(cpumask);
					Controller.AffinityStrategy = affstrategy.SelectedIndex == 1 ? ProcessAffinityStrategy.Limit : ProcessAffinityStrategy.Force;
				}
			}
			else
			{
				// strategy = ignore
				Controller.Affinity = null;
				Controller.AffinityStrategy = ProcessAffinityStrategy.None;
			}

			Controller.ModifyDelay = (int)(modifyDelay.Value * 1000);
			Controller.PowerPlan = PowerManager.GetModeByName(powerPlan.Text);
			if (Controller.PowerPlan == PowerInfo.PowerMode.Custom) Controller.PowerPlan = PowerInfo.PowerMode.Undefined;
			Controller.Rescan = Convert.ToInt32(rescanFreq.Value);
			Controller.AllowPaging = allowPaging.Checked;
			Controller.ForegroundOnly = foregroundOnly.Checked;

			if (ignorelist.Items.Count > 0)
			{
				List<string> ignlist = new List<string>();
				foreach (ListViewItem item in ignorelist.Items)
					ignlist.Add(item.Text);

				Controller.IgnoreList = ignlist.ToArray();
			}
			else
				Controller.IgnoreList = null;

			Controller.Enabled = enOrig;
			Controller.SaveConfig();

			DialogResult = DialogResult.OK;

			Close();
		}

		TextBox friendlyName = new TextBox();
		TextBox execName = new TextBox();
		TextBox pathName = new TextBox();
		ComboBox priorityClass = null;
		CheckBox increasePrio = new CheckBox();
		CheckBox decreasePrio = new CheckBox();
		ComboBox affstrategy = new ComboBox();
		NumericUpDown affinityMask = new NumericUpDown();
		Button allbutton = new Button();
		Button clearbutton = new Button();
		NumericUpDown rescanFreq = new NumericUpDown();
		NumericUpDown modifyDelay = new NumericUpDown();
		CheckBox allowPaging = new CheckBox();
		ComboBox powerPlan = new ComboBox();
		CheckBox foregroundOnly = new CheckBox();
		CheckBox bacgroundPowerdown = new CheckBox();
		ListView ignorelist = new ListView();
		int cpumask = 0;

		void BuildUI()
		{
			// Size = new System.Drawing.Size(340, 480); // width, height
			AutoSizeMode = AutoSizeMode.GrowOnly;
			AutoSize = true;

			Text = string.Format("{0} ({1}) – {2}",
								 Controller.FriendlyName,
								 (Controller.Executable ?? Controller.Path),
								 System.Windows.Forms.Application.ProductName);
			Padding = new Padding(12);

			var tooltip = new ToolTip();

			var lt = new TableLayoutPanel
			{
				Parent = this,
				ColumnCount = 3,
				//lrows.RowCount = 10;
				Dock = DockStyle.Fill,
				AutoSize = true,
			};

			lt.Controls.Add(new Label { Text = "Friendly name", TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
			friendlyName.Text = Controller.FriendlyName;
			friendlyName.Width = 180;
			friendlyName.CausesValidation = true;
			friendlyName.Validating += (sender, e) =>
			{
				if (friendlyName.Text.Contains("]") || friendlyName.Text.Length == 0)
				{
					e.Cancel = true;
					friendlyName.Select(0, friendlyName.Text.Length);
				}
			};
			tooltip.SetToolTip(friendlyName, "Human readable name, for user convenience.");
			lt.Controls.Add(friendlyName);

			lt.Controls.Add(new Label()); // empty

			// EXECUTABLE
			lt.Controls.Add(new Label { Text = "Executable", TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Dock = DockStyle.Left });
			execName.Text = Controller.Executable;
			execName.Width = 180;
			tooltip.SetToolTip(execName, "Executable name, used to recognize these applications.\nFull filename, including extension if any.");
			var findexecbutton = new Button()
			{
				Text = "Running",
				AutoSize = true,
				//Dock = DockStyle.Left,
				//Width = 46,
				//Height = 20,
			};
			findexecbutton.Click += (sender, e) =>
			{
				using (var exselectdialog = new ProcessSelectDialog())
				{
					try
					{
						if (exselectdialog.ShowDialog(this) == DialogResult.OK)
						{
							// SANITY CHECK: exselectdialog.Selection;
							execName.Text = exselectdialog.Selection;
						}
					}
					catch (Exception ex)
					{
						Log.Fatal("{Type} : {Message}", ex.GetType().Name, ex.Message);
					}
				}
			};
			lt.Controls.Add(execName);
			lt.Controls.Add(findexecbutton);

			// PATH
			lt.Controls.Add(new Label { Text = "Path", TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
			pathName.Text = Controller.Path;
			pathName.Width = 180;
			tooltip.SetToolTip(pathName, "Path name; rule will match only paths that include this, subfolders included.\nPartial matching is allowed.");
			var findpathbutton = new Button()
			{
				Text = "Locate",
				AutoSize = true,
				//Dock = DockStyle.Left,
				//Width = 46,
				//Height = 20,
			};
			findpathbutton.Click += (sender, e) =>
			{
				try
				{
					using (var folderdialog = new FolderBrowserDialog())
					{
						folderdialog.ShowNewFolderButton = false;
						folderdialog.RootFolder = Environment.SpecialFolder.MyComputer;
						var result = folderdialog.ShowDialog();
						if (result == DialogResult.OK && !string.IsNullOrEmpty(folderdialog.SelectedPath))
						{
							pathName.Text = folderdialog.SelectedPath;
						}
					}
				}
				catch (Exception ex)
				{
					Logging.Stacktrace(ex);
				}
			};
			lt.Controls.Add(pathName);
			lt.Controls.Add(findpathbutton);

			// IGNORE

			ignorelist.View = View.Details;
			ignorelist.HeaderStyle = ColumnHeaderStyle.None;
			ignorelist.Width = 180;
			ignorelist.Columns.Add("Executable", -2);

			if (Controller.IgnoreList != null)
			{
				foreach (string item in Controller.IgnoreList)
					ignorelist.Items.Add(item);

			}

			var ignorelistmenu = new ContextMenuStrip();
			ignorelist.ContextMenuStrip = ignorelistmenu;
			ignorelistmenu.Items.Add(new ToolStripMenuItem("Add", null, (s, ev) =>
			{
				try
				{
					using (var rs = new TextInputBox("Filename:", "Ignore executable"))
					{
						rs.ShowDialog();
						if (rs.DialogResult == DialogResult.OK)
						{
							ignorelist.Items.Add(rs.Value);
						}
					}
				}
				catch (Exception ex)
				{
					Logging.Stacktrace(ex);
				}
			}));
			ignorelistmenu.Items.Add(new ToolStripMenuItem("Remove", null, (s, ev) =>
			{
				if (ignorelist.SelectedItems.Count == 1)
					ignorelist.Items.Remove(ignorelist.SelectedItems[0]);
			}));

			lt.Controls.Add(new Label() { Text = "Ignore", TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
			lt.Controls.Add(ignorelist);
			lt.Controls.Add(new Label()); // empty

			// PRIORITY
			lt.Controls.Add(new Label { Text = "Priority class", TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
			var priopanel = new TableLayoutPanel()
			{
				ColumnCount = 1,
				AutoSize = true
			};
			priorityClass = new ComboBox
			{
				Dock = DockStyle.Left,
				DropDownStyle = ComboBoxStyle.DropDownList,
				Items = { "Idle", "Below Normal", "Normal", "Above Normal", "High", "Ignored" }, // System.Enum.GetNames(typeof(ProcessPriorityClass)), 
				SelectedIndex = 2
			};
			priorityClass.Width = 180;
			priorityClass.SelectedIndex = Controller.Priority?.ToInt32() ?? 5;
			tooltip.SetToolTip(priorityClass, "CPU priority for the application.\nIf both increase and decrease are disabled, this has no effect.");
			var incdecpanel = new TableLayoutPanel()
			{
				ColumnCount = 4,
				AutoSize = true,
			};
			incdecpanel.Controls.Add(new Label() { Text = "Increase:", TextAlign = System.Drawing.ContentAlignment.MiddleLeft, AutoSize = true });
			increasePrio.Checked = Controller.PriorityStrategy == ProcessPriorityStrategy.Increase || Controller.PriorityStrategy == ProcessPriorityStrategy.Force;
			increasePrio.AutoSize = true;
			incdecpanel.Controls.Add(increasePrio);
			incdecpanel.Controls.Add(new Label() { Text = "Decrease:", TextAlign = System.Drawing.ContentAlignment.MiddleLeft, AutoSize = true });
			decreasePrio.Checked = Controller.PriorityStrategy == ProcessPriorityStrategy.Decrease || Controller.PriorityStrategy == ProcessPriorityStrategy.Force;
			decreasePrio.AutoSize = true;
			incdecpanel.Controls.Add(decreasePrio);
			priopanel.Controls.Add(priorityClass);
			priopanel.Controls.Add(incdecpanel);
			lt.Controls.Add(priopanel);
			lt.Controls.Add(new Label()); // empty

			priorityClass.SelectedIndexChanged += (s, e) => {
				if (priorityClass.SelectedIndex == 5)
				{
					increasePrio.Enabled = false;
					decreasePrio.Enabled = false;
				}
				else
				{
					increasePrio.Enabled = true;
					decreasePrio.Enabled = true;
				}
			};

			// lt.Controls.Add(priorityClass);

			// AFFINITY
			var corelist = new List<CheckBox>();

			lt.Controls.Add(new Label { Text = "Affinity", TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
			affstrategy.DropDownStyle = ComboBoxStyle.DropDownList;
			affstrategy.Items.AddRange(new string[] { "Ignored", "Limit (Default)", "Force" });
			tooltip.SetToolTip(affstrategy, "Limit constrains cores to the defined range but does not increase used cores beyond what the app is already using.\nForce sets the affinity mask to the defined regardless of anything.");
			affstrategy.SelectedIndexChanged += (s, e) =>
			{
				bool enabled = affstrategy.SelectedIndex != 0;
				affinityMask.Enabled = enabled;
				foreach (var box in corelist)
					box.Enabled = enabled;
				allbutton.Enabled = enabled;
				clearbutton.Enabled = enabled;
			};

			lt.Controls.Add(affstrategy);
			lt.Controls.Add(new Label()); // empty, right

			lt.Controls.Add(new Label() { Text = "Affinity mask\n& cores", TextAlign = System.Drawing.ContentAlignment.MiddleLeft }); // left
			affinityMask.Width = 80;
			affinityMask.Maximum = ProcessManager.allCPUsMask;
			affinityMask.Minimum = -1;
			affinityMask.Value = (Controller.Affinity?.ToInt32() ?? 0);

			tooltip.SetToolTip(affinityMask, "CPU core afffinity as integer mask.\nEnter 0 to let OS manage this as normal.\nFull affinity is same as 0, there's no difference.\nExamples:\n14 = all but first core on quadcore.\n254 = all but first core on octocore.\n-1 = Ignored");

			// lt.Controls.Add(affinityMask);

			// ---------------------------------------------------------------------------------------------------------

			var afflayout = new TableLayoutPanel()
			{
				ColumnCount = 1,
				AutoSize = true,
			};

			afflayout.Controls.Add(affinityMask);

			var corelayout = new TableLayoutPanel()
			{
				ColumnCount = 8,
				AutoSize = true,
			};

			cpumask = Controller.Affinity?.ToInt32() ?? -1;
			for (int bit = 0; bit < ProcessManager.CPUCount; bit++)
			{
				var box = new CheckBox();
				var bitoff = bit;
				box.AutoSize = true;
				box.Checked = ((Math.Max(0,cpumask) & (1 << bitoff)) != 0);
				box.CheckedChanged += (sender, e) =>
				{
					if (cpumask < 0) cpumask = 0;

					if (box.Checked)
					{
						cpumask |= (1 << bitoff);
						affinityMask.Value = cpumask;
					}
					else
					{
						cpumask &= ~(1 << bitoff);
						affinityMask.Value = cpumask;
					}
				};
				corelist.Add(box);
				corelayout.Controls.Add(new Label
				{
					Text = (bit + 1) + ":",
					AutoSize = true,
					//BackColor = System.Drawing.Color.LightPink,
					TextAlign = System.Drawing.ContentAlignment.MiddleLeft
				});
				corelayout.Controls.Add(box);
			}

			afflayout.Controls.Add(corelayout);

			var buttonpanel = new TableLayoutPanel()
			{
				ColumnCount = 1,
				AutoSize = true,
			};
			clearbutton.Text = "None";
			clearbutton.Click += (sender, e) =>
			{
				foreach (var litem in corelist) litem.Checked = false;
			};
			allbutton.Text = "All";
			allbutton.Click += (sender, e) =>
			{
				foreach (var litem in corelist) litem.Checked = true;
			};
			buttonpanel.Controls.Add(allbutton);
			buttonpanel.Controls.Add(clearbutton);

			affinityMask.ValueChanged += (sender, e) =>
			{
				var bitoff = 0;
				try { cpumask = (int)affinityMask.Value; }
				catch { cpumask = 0; affinityMask.Value = 0; }
				foreach (var bu in corelist)
					bu.Checked = ((Math.Max(0,cpumask) & (1 << bitoff++)) != 0);
			};

			switch (Controller.AffinityStrategy)
			{
				case ProcessAffinityStrategy.Force:
					affstrategy.SelectedIndex = 2;
					break;
				default:
				case ProcessAffinityStrategy.Limit:
					affstrategy.SelectedIndex = 1;
					break;
				case ProcessAffinityStrategy.None:
					affstrategy.SelectedIndex = 0;
					break;
			}

			lt.Controls.Add(afflayout);
			lt.Controls.Add(buttonpanel);

			// ---------------------------------------------------------------------------------------------------------

			// RESCAN
			lt.Controls.Add(new Label { Text = "Rescan frequency", TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
			rescanFreq.Minimum = 0;
			rescanFreq.Maximum = 60 * 24;
			rescanFreq.Value = Controller.Rescan;
			rescanFreq.Width = 80;
			tooltip.SetToolTip(rescanFreq, "How often to rescan for this app, in minutes.\nSometimes instances slip by.");
			lt.Controls.Add(rescanFreq);
			lt.Controls.Add(new Label()); // empty

			// lt.Controls.Add(new Label { Text="Children"});
			// lt.Controls.Add(new Label { Text="Child priority"});

			// MODIFY DELAY

			lt.Controls.Add(new Label() { Text = "Modify delay", TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
			modifyDelay.Minimum = 0;
			modifyDelay.Maximum = 180;
			modifyDelay.DecimalPlaces = 1;
			modifyDelay.Value = ((decimal)Controller.ModifyDelay) / 1000;
			modifyDelay.Width = 80;
			tooltip.SetToolTip(modifyDelay, "Delay before the process is actually attempted modification.\nEither to keep original priority for a short while, or to counter early self-adjustment.\nThis is also applied to foreground only limited modifications.");
			lt.Controls.Add(modifyDelay);
			lt.Controls.Add(new Label()); // empty

			// POWER
			lt.Controls.Add(new Label { Text = "Power plan", TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
			foreach (string t in PowerManager.PowerModes)
				powerPlan.Items.Add(t);
			var ppi = System.Convert.ToInt32(Controller.PowerPlan);
			powerPlan.DropDownStyle = ComboBoxStyle.DropDownList;
			powerPlan.SelectedIndex = System.Math.Min(ppi, 3);
			powerPlan.Width = 180;
			tooltip.SetToolTip(powerPlan, "Power Mode to be used when this application is detected. Leaving this undefined disables it.");
			lt.Controls.Add(powerPlan);
			lt.Controls.Add(new Label()); // empty

			// FOREGROUND
			lt.Controls.Add(new Label { Text = "Foreground only", TextAlign = System.Drawing.ContentAlignment.MiddleLeft, AutoSize = true });
			foregroundOnly.Checked = Controller.ForegroundOnly;
			tooltip.SetToolTip(foregroundOnly, "Lower priority and power mode is restored when this app is not in focus.");
			lt.Controls.Add(foregroundOnly);
			lt.Controls.Add(new Label()); // empty

			// POWERDOWN in background
			lt.Controls.Add(new Label() { Text = "Background powerdown", TextAlign = System.Drawing.ContentAlignment.MiddleLeft, AutoSize = true });
			bacgroundPowerdown.Checked = Controller.BackgroundPowerdown;
			tooltip.SetToolTip(bacgroundPowerdown, "Power down any power mode when the app goes off focus.");
			lt.Controls.Add(bacgroundPowerdown);
			lt.Controls.Add(new Label()); // empty

			// TODO: Add modifying background priority

			// PAGING
			lt.Controls.Add(new Label { Text = "Allow paging", TextAlign = System.Drawing.ContentAlignment.MiddleLeft });
			allowPaging.Checked = Controller.AllowPaging;
			tooltip.SetToolTip(allowPaging, "Allow this application to be paged when it is requested.\nNOT FULLY IMPLEMENTED.");
			lt.Controls.Add(allowPaging);
			lt.Controls.Add(new Label()); // empty

			// lt.Controls.Add(new Label { Text=""})

			var finalizebuttons = new TableLayoutPanel() { ColumnCount = 2, AutoSize = true };
			var saveButton = new Button() { Text = "Save" }; // SAVE
			saveButton.Click += SaveInfo;
			finalizebuttons.Controls.Add(saveButton);
			// lt.Controls.Add(saveButton);
			var cancelButton = new Button() { Text = "Cancel" }; // CLOSE
			cancelButton.Click += (sender, e) =>
			{
				DialogResult = DialogResult.Cancel;
				Close();
			};
			finalizebuttons.Controls.Add(cancelButton);

			var validatebutton = new Button() { Text = "Validate" };
			validatebutton.Click += ValidateWatchedItem;
			validatebutton.Margin = CustomPadding;

			lt.Controls.Add(validatebutton);

			lt.Controls.Add(finalizebuttons);

			// ---
		}

		void ValidateWatchedItem(object sender, EventArgs ev)
		{
			var fnlen = (friendlyName.Text.Length > 0);
			var exnam = (execName.Text.Length > 0);
			var path = (pathName.Text.Length > 0);

			var exfound = false;
			if (exnam)
			{
				var friendlyexe = System.IO.Path.GetFileNameWithoutExtension(execName.Text);
				var procs = Process.GetProcessesByName(friendlyexe);
				if (procs.Length > 0) exfound = true;
			}

			string pfound = "No";
			if (path)
			{
				try
				{
					if (System.IO.Directory.Exists(pathName.Text))
					{
						pfound = "Exact";
					}
					else
					{
						string search = System.IO.Path.GetFileName(pathName.Text);
						var di = System.IO.Directory.GetParent(pathName.Text);
						if (di != null && !string.IsNullOrEmpty(search))
						{
							var dirs = System.IO.Directory.EnumerateDirectories(di.FullName, search+"*", System.IO.SearchOption.TopDirectoryOnly);
							foreach (var dir in dirs)
							{
								pfound = "Partial Match";
								break;
							}
						}
					}
				}
				catch
				{
					// NOP, don't caree
				}
			}

			var sbs = new System.Text.StringBuilder();
			sbs.Append("Name: ").Append(fnlen ? "OK" : "Fail").AppendLine();

			var samesection = Controller.FriendlyName.Equals(friendlyName.Text);
			if (!samesection)
			{
				var dprc = Taskmaster.processmanager.getWatchedController(friendlyName.Text);
				if (dprc != null)
				{
					sbs.Append("Friendly name conflict!");
				}
			}

			if (execName.Text.Length > 0)
				sbs.Append("Executable: ").Append(exnam ? "OK" : "Fail").Append(" – Found: ").Append(exfound).AppendLine();
			if (pathName.Text.Length > 0)
				sbs.Append("Path: ").Append(path ? "OK" : "Fail").Append(" - Found: ").Append(pfound).AppendLine();

			if (!exnam && !path)
				sbs.Append("Both path and executable are missing!").AppendLine();

			if ((rescanFreq.Value > 0) && !exnam)
				sbs.Append("Rescan frequency REQUIRES executable to be defined.").AppendLine();
			if (priorityClass.SelectedIndex == 5)
				sbs.Append("Priority class is to be ignored.").AppendLine();
			if (cpumask == -1 || affstrategy.SelectedIndex == 0)
				sbs.Append("Affinity is to be ignored.").AppendLine();
			if (ignorelist.Items.Count > 0 && execName.Text.Length > 0)
				sbs.Append("Ignore list is meaningless with executable defined.").AppendLine();

			MessageBox.Show(sbs.ToString(), "Validation results", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
		}
	}
}