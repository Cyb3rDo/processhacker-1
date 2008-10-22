﻿/*
 * Process Hacker
 * 
 * Copyright (C) 2008 wj32
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ProcessHacker
{
    public partial class HackerWindow : Form
    {
        delegate void QueueUpdatedCallback();
        delegate void AddIconCallback(Icon icon);
        delegate void AddListViewItemCallback(ListView lv, string[] text);

        #region Properties

        public MenuItem WindowMenuItem
        {
            get { return windowMenuItem; }
        }

        public wyDay.Controls.VistaMenu VistaMenu
        {
            get { return vistaMenu; }
        }

        #endregion

        #region Variables

        public int RefreshInterval = 1000;

        HelpWindow helpForm = new HelpWindow();

        // Queue of list update tasks
        Queue<UpdateTask> processQueue = new Queue<UpdateTask>();
        Queue<UpdateTask> threadQueue = new Queue<UpdateTask>();
        List<int> pids = new List<int>();
        List<int> tids = new List<int>();
        Thread processUpdaterThread;
        Thread threadUpdaterThread;
        //Thread cpuTimeUpdaterThread;

        int processSelectedItems;
        int processSelectedPID;
        int lastSelectedPID = -1;
        Process processSelected;

        Process virtualProtectProcess;
        int virtualProtectAddress;
        int virtualProtectSize;

        Process memoryProcess;
        int memoryAddress;
        int memorySize;

        int imageIndex = 1;

        Point lastMenuLocation;

        List<ListView> listViews = new List<ListView>();

        string[] dangerousNames = { "csrss.exe", "dwm.exe", "lsass.exe", "lsm.exe", "services.exe",
                                      "smss.exe", "wininit.exe", "winlogon.exe" };

        string[] kernelNames = { "ntoskrnl.exe", "ntkrnlpa.exe", "ntkrnlmp.exe", "ntkrpamp.exe" };

        #endregion

        #region Events

        #region Buttons

        private void buttonCloseProc_Click(object sender, EventArgs e)
        {
            panelProc.Visible = false;
            this.AcceptButton = null;

            listProcesses.Enabled = true;
            listThreads.Enabled = true;
            tabControl.Enabled = true;
        }

        private void buttonCloseVirtualProtect_Click(object sender, EventArgs e)
        {
            CloseVirtualProtect();
        }  

        private void buttonGetProcAddress_Click(object sender, EventArgs e)
        {
            if (listModules.SelectedItems.Count != 1)
                return;

            bool loaded = Win32.GetModuleHandle(listModules.SelectedItems[0].ToolTipText) != 0;
            int module;
            int address = 0;
            int ordinal = 0;

            if (loaded)
                module = Win32.GetModuleHandle(listModules.SelectedItems[0].ToolTipText);
            else
                module = Win32.LoadLibraryEx(listModules.SelectedItems[0].ToolTipText, 0, Win32.DONT_RESOLVE_DLL_REFERENCES);

            if (module == 0)
            {
                textProcAddress.Text = "Could not load library!";
            }

            if ((textProcName.Text.Length > 0) &&
                (textProcName.Text[0] >= '0' && textProcName.Text[0] <= '9'))
                ordinal = (int)BaseConverter.ToNumberParse(textProcName.Text, false);

            if (ordinal != 0)
            {
                address = Win32.GetProcAddress(module, ordinal);
            }
            else
            {
                address = Win32.GetProcAddress(module, textProcName.Text);
            }

            if (address != 0)
            {
                textProcAddress.Text = String.Format("0x{0:x8}", address);
                textProcAddress.SelectAll();
                textProcAddress.Focus();
            }
            else if (Marshal.GetLastWin32Error() == 0x7f)
            {
                textProcAddress.Text = "Not found.";
            }
            else
            {
                textProcAddress.Text = "Error.";
            }

            // don't unload libraries we had before
            if (module != 0 && !loaded)
                Win32.FreeLibrary(module);
        }

        private void buttonSearch_Click(object sender, EventArgs e)
        {
            PerformSearch(buttonSearch.Text);
        }

        private void buttonVirtualProtect_Click(object sender, EventArgs e)
        {
            try
            {
                int old = 0;
                int newprotect;

                try
                {
                    newprotect = (int)BaseConverter.ToNumberParse(textNewProtection.Text);
                }
                catch
                {
                    return;
                }

                if (Win32.VirtualProtectEx(virtualProtectProcess.Handle.ToInt32(), virtualProtectAddress,
                    virtualProtectSize, newprotect, ref old) == 0)
                {
                    MessageBox.Show("There was an error setting memory protection.", "Process Hacker",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                CloseVirtualProtect();

                try
                {
                    listMemory.SelectedItems[0].SubItems[4].Text =
                        ((Win32.MEMORY_PROTECTION)newprotect).ToString().Replace("PAGE_", "");
                }
                catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show("There was an error setting memory protection:\n\n" + ex.Message, "Process Hacker",
                 MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Lists

        private void listMemory_DoubleClick(object sender, EventArgs e)
        {
            readWriteMemoryMemoryMenuItem_Click(null, null);
        }

        private void listModules_DoubleClick(object sender, EventArgs e)
        {
            goToInMemoryViewModuleMenuItem_Click(null, null);
        }

        private void listProcesses_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Delete)
            {
                terminateMenuItem_Click(null, null);
            }
        }

        private void listProcesses_SelectedIndexChanged(object sender, EventArgs e)
        {
            processSelectedItems = listProcesses.SelectedItems.Count;

            if (processSelectedItems == 1)
            {
                processSelectedPID = Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text);

                if (processSelectedPID != lastSelectedPID)
                {
                    listThreads.Items.Clear();
                }

                treeMisc.Enabled = true;
                buttonSearch.Enabled = true;

                try
                {
                    try
                    {
                        if (processSelected != null)
                            processSelected.Close();
                    }
                    catch
                    { }

                    processSelected = Process.GetProcessById(processSelectedPID);

                    UpdateProcessExtra();
                }
                catch
                {
                    processSelected = null;

                    listMemory.Enabled = false;
                    listModules.Enabled = false;
                    listThreads.Enabled = false;
                }
            }
            else
            {
                processSelectedPID = -1;
                lastSelectedPID = -1;

                try
                {
                    if (processSelected != null)
                        processSelected.Close();
                }
                catch
                { }

                processSelected = null;

                listThreads.Items.Clear();
                treeMisc.Enabled = false;
                buttonSearch.Enabled = false;

                UpdateProcessExtra();
            }
        }

        private void listThreads_DoubleClick(object sender, EventArgs e)
        {
            inspectThreadMenuItem_Click(null, null);
        }

        #endregion

        #region Main Menu

        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            AboutWindow about = new AboutWindow();

            about.ShowDialog();
        }

        private void optionsMenuItem_Click(object sender, EventArgs e)
        {
            OptionsWindow options = new OptionsWindow();

            options.ShowDialog();

            RefreshInterval = Properties.Settings.Default.RefreshInterval;
            timerFire.Interval = RefreshInterval;
        }

        private void helpMenuItem_Click(object sender, EventArgs e)
        {
            helpForm.Show();
        }

        private void exitMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void selectAllHackerMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListView c in listViews)
            {
                if (c.Focused)
                {
                    try
                    {
                        SelectAll(c);
                    }
                    catch
                    { }
                }
            }
        }

        private void refreshMenuItem_Click(object sender, EventArgs e)
        {
            ReloadProcessList();

            processSelectedPID = -1;
            lastSelectedPID = -1;

            if (processSelected != null)
                processSelected.Close();

            listThreads.Items.Clear();
            treeMisc.Nodes.Clear();
            InitMiscInfo();
        }

        #endregion

        #region Memory Context Menu

        private void menuMemory_Popup(object sender, EventArgs e)
        {
            if (listMemory.SelectedItems.Count == 1 && listProcesses.SelectedItems.Count == 1)
            {
                EnableAllMenuItems(menuMemory);
            }      
            else
            {
                DisableAllMenuItems(menuMemory);

                if (listProcesses.SelectedItems.Count == 1)
                    readWriteAddressMemoryMenuItem.Enabled = true;
            }
        }

        private void changeMemoryProtectionMemoryMenuItem_Click(object sender, EventArgs e)
        {
            virtualProtectProcess = Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text));

            virtualProtectAddress = Int32.Parse(listMemory.SelectedItems[0].SubItems[0].Text.Replace("0x", ""),
                System.Globalization.NumberStyles.HexNumber);
            virtualProtectSize = Int32.Parse(listMemory.SelectedItems[0].SubItems[1].Text.Replace("0x", ""),
                System.Globalization.NumberStyles.HexNumber);

            ShowVirtualProtect();
        }

        private void readWriteMemoryMemoryMenuItem_Click(object sender, EventArgs e)
        {
            memoryProcess = Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text));
            memoryAddress = Int32.Parse(listMemory.SelectedItems[0].SubItems[0].Text.Replace("0x", ""),
                System.Globalization.NumberStyles.HexNumber);
            memorySize = Int32.Parse(listMemory.SelectedItems[0].SubItems[1].Text.Replace("0x", ""),
                System.Globalization.NumberStyles.HexNumber);

            ReadWriteMemory();
        }

        private void readWriteAddressMemoryMenuItem_Click(object sender, EventArgs e)
        {
            PromptBox prompt = new PromptBox();

            if (prompt.ShowDialog() == DialogResult.OK)
            {
                int address = -1;
                bool found = false;

                try
                {
                    address = (int)BaseConverter.ToNumberParse(prompt.Value);
                }
                catch
                {
                    MessageBox.Show("Invalid address!", "Process Hacker", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                if (address < 0)
                    return;

                memoryProcess = Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text));

                foreach (ListViewItem item in listMemory.Items)
                {
                    int itemaddress = Int32.Parse(item.SubItems[0].Text.Replace("0x", ""),
                System.Globalization.NumberStyles.HexNumber);

                    if (itemaddress > address)
                    {
                        listMemory.Items[item.Index - 1].Selected = true;
                        listMemory.Items[item.Index - 1].EnsureVisible();
                        memoryAddress = Int32.Parse(listMemory.Items[item.Index - 1].SubItems[0].Text.Replace("0x", ""),
                System.Globalization.NumberStyles.HexNumber);
                        memorySize = Int32.Parse(listMemory.Items[item.Index - 1].SubItems[1].Text.Replace("0x", ""),
                System.Globalization.NumberStyles.HexNumber);
                        found = true;

                        break;
                    }
                }

                if (!found)
                {
                    MessageBox.Show("Memory address not found!", "Process Hacker", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MemoryEditor m_e = ReadWriteMemory(true);

                try
                {
                    m_e.Invoke(new MethodInvoker(delegate { m_e.Select(address - memoryAddress, 1); }));
                }
                catch
                { }
            }
        }

        #endregion

        #region Menu Fixes

        private void listHeap_MouseDown(object sender, MouseEventArgs e)
        {
            lastMenuLocation = e.Location;
        }

        private void listMemory_MouseDown(object sender, MouseEventArgs e)
        {
            lastMenuLocation = e.Location;
        }

        private void listModules_MouseDown(object sender, MouseEventArgs e)
        {
            lastMenuLocation = e.Location;
        }

        private void listProcesses_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                lastMenuLocation = e.Location;
        }

        private void listThreads_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                lastMenuLocation = e.Location;
        }

        private void menuMemory2_Opening(object sender, CancelEventArgs e)
        {
            menuMemory.Show(listMemory, lastMenuLocation);
            e.Cancel = true;
        }

        private void menuModule2_Opening(object sender, CancelEventArgs e)
        {
            menuModule.Show(listModules, lastMenuLocation);
            e.Cancel = true;
        }

        private void menuProcess2_Opening(object sender, CancelEventArgs e)
        {
            menuProcess.Show(listProcesses, lastMenuLocation);
            e.Cancel = true;
        }

        private void menuThread2_Opening(object sender, CancelEventArgs e)
        {
            menuThread.Show(listThreads, lastMenuLocation);
            e.Cancel = true;
        }

        #endregion

        #region Module Context Menu

        private void menuModule_Popup(object sender, EventArgs e)
        {
            if (listModules.SelectedItems.Count == 1 && listProcesses.SelectedItems.Count == 1)
            {
                if (Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text) == 4)
                {
                    DisableAllMenuItems(menuModule);

                    copyFileNameMenuItem.Enabled = true;
                    openContainingFolderMenuItem.Enabled = true;
                    propertiesMenuItem.Enabled = true;
                }
                else
                {
                    EnableAllMenuItems(menuModule);
                }
            }
            else
            {
                DisableAllMenuItems(menuModule);
            }
        }

        private void copyFileNameMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(listModules.SelectedItems[0].ToolTipText);
        }

        private void openContainingFolderMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", "/select," + listModules.SelectedItems[0].ToolTipText);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not start process:\n\n" + ex.Message, "Process Hacker", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void propertiesMenuItem_Click(object sender, EventArgs e)
        {
            Win32.SHELLEXECUTEINFO info = new Win32.SHELLEXECUTEINFO();

            info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.SHELLEXECUTEINFO));
            info.lpFile = listModules.SelectedItems[0].ToolTipText;
            info.nShow = Win32.SW_SHOW;
            info.fMask = Win32.SEE_MASK_INVOKEIDLIST;
            info.lpVerb = "properties";

            Win32.ShellExecuteEx(ref info);
        }

        private void goToInMemoryViewModuleMenuItem_Click(object sender, EventArgs e)
        {
            string address = listModules.SelectedItems[0].SubItems[1].Text;

            foreach (ListViewItem item in listMemory.Items)
            {
                if (item.SubItems[0].Text == address)
                {
                    item.Selected = true;
                    tabControl.SelectedTab = tabMemory;
                    listMemory.EnsureVisible(item.Index);
                    listMemory.Select();
                    listMemory.Focus();
                }
            }
        }

        private void getFuncAddressMenuItem_Click(object sender, EventArgs e)
        {
            listProcesses.Enabled = false;
            listThreads.Enabled = false;
            tabControl.Enabled = false;

            panelProc.Visible = true;
            panelProc.BringToFront();
            this.AcceptButton = buttonGetProcAddress;
            textProcName.SelectAll();
            textProcName.Focus();
            textProcAddress.Text = "";
        }

        private void changeMemoryProtectionModuleMenuItem_Click(object sender, EventArgs e)
        {
            virtualProtectProcess = Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text));
            ProcessModule module = null;

            foreach (ProcessModule m in virtualProtectProcess.Modules)
            {
                if (m.FileName == listModules.SelectedItems[0].ToolTipText)
                {
                    module = m;
                    break;
                }
            }

            if (module == null)
                return;

            virtualProtectAddress = module.BaseAddress.ToInt32();
            virtualProtectSize = module.ModuleMemorySize;

            ShowVirtualProtect();
        }

        private void readMemoryModuleMenuItem_Click(object sender, EventArgs e)
        {
            Process p = Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text));
            ProcessModule module = null;

            foreach (ProcessModule m in p.Modules)
            {
                if (m.FileName == listModules.SelectedItems[0].ToolTipText)
                {
                    module = m;
                    break;
                }
            }

            if (module == null)
                return;

            memoryProcess = p;
            memoryAddress = module.BaseAddress.ToInt32();
            memorySize = module.ModuleMemorySize;

            ReadWriteMemory(true);
        }

        #endregion

        #region Process Context Menu

        private void menuProcess_Popup(object sender, EventArgs e)
        {
            if (listProcesses.SelectedItems.Count == 0)
            {
                terminateMenuItem.Enabled = false;
                suspendMenuItem.Enabled = false;
                resumeMenuItem.Enabled = false;
                closeActiveWindowMenuItem.Enabled = false;
                priorityMenuItem.Enabled = false;
            }
            else
            {
                priorityMenuItem.Text = "&Priority";

                if (listProcesses.SelectedItems.Count == 1)
                {
                    priorityMenuItem.Enabled = true;
                    terminateMenuItem.Text = "&Terminate Process";
                    closeActiveWindowMenuItem.Text = "&Close Active Window";
                    suspendMenuItem.Text = "&Suspend Process";
                    resumeMenuItem.Text = "&Resume Process";

                    realTimeMenuItem.Checked = false;
                    highMenuItem.Checked = false;
                    aboveNormalMenuItem.Checked = false;
                    normalMenuItem.Checked = false;
                    belowNormalMenuItem.Checked = false;
                    idleMenuItem.Checked = false;

                    try
                    {
                        switch (Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text)).PriorityClass)
                        {
                            case ProcessPriorityClass.RealTime:
                                realTimeMenuItem.Checked = true;
                                break;

                            case ProcessPriorityClass.High:
                                highMenuItem.Checked = true;
                                break;

                            case ProcessPriorityClass.AboveNormal:
                                aboveNormalMenuItem.Checked = true;
                                break;

                            case ProcessPriorityClass.Normal:
                                normalMenuItem.Checked = true;
                                break;

                            case ProcessPriorityClass.BelowNormal:
                                belowNormalMenuItem.Checked = true;
                                break;

                            case ProcessPriorityClass.Idle:
                                idleMenuItem.Checked = true;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        priorityMenuItem.Text = "(" + ex.Message + ")";
                        priorityMenuItem.Enabled = false;
                    }
                }
                else
                {
                    priorityMenuItem.Enabled = false;
                    terminateMenuItem.Text = "&Terminate Processes";
                    closeActiveWindowMenuItem.Text = "&Close Active Windows";
                    suspendMenuItem.Text = "&Suspend Processes";
                    resumeMenuItem.Text = "&Resume Processes";
                }

                terminateMenuItem.Enabled = true;
                closeActiveWindowMenuItem.Enabled = true;
                suspendMenuItem.Enabled = true;
                resumeMenuItem.Enabled = true;
            }
        }

        private void terminateMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to terminate the selected process(es)?", "Process Hacker", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
            {
                foreach (ListViewItem item in listProcesses.SelectedItems)
                {
                    try
                    {
                        Process.GetProcessById(Int32.Parse(item.SubItems[1].Text)).Kill();
                    }
                    catch (Exception ex)
                    {
                        DialogResult result = MessageBox.Show("Could not terminate process \"" + item.SubItems[0].Text +
                            "\" with PID " + item.SubItems[1].Text + ":\n\n" +
                                ex.Message, "Process Hacker", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);

                        if (result == DialogResult.Cancel)
                            return;
                    }
                }
            }
        }

        private void suspendMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listProcesses.SelectedItems)
            {
                Process process;

                try
                {
                    process = Process.GetProcessById(Int32.Parse(item.SubItems[1].Text));
                }
                catch { return; }

                if (Properties.Settings.Default.WarnDangerous && IsDangerousPID(process.Id))
                {
                    DialogResult result = MessageBox.Show("The process with PID " + process.Id + " is a system process. Are you" +
                        " sure you want to suspend it?", "Process Hacker", MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2);

                    if (result == DialogResult.No)
                        continue;
                    else if (result == DialogResult.Cancel)
                        return;
                }

                try
                {
                    foreach (ProcessThread thread in process.Threads)
                    {
                        int handle = Win32.OpenThread(Win32.THREAD_SUSPEND_RESUME, 0, thread.Id);

                        if (handle == 0)
                        {
                            throw new Exception("Could not open process handle.");
                        }

                        if (Win32.SuspendThread(handle) == -1)
                        {
                            Win32.CloseHandle(handle);
                            throw new Exception("Could not suspend thread.");
                        }

                        Win32.CloseHandle(handle);
                    }
                }
                catch (Exception ex)
                {
                    DialogResult result = MessageBox.Show("Could not suspend process with PID " + item.SubItems[1].Text +
                        ".\n\n" + ex.Message, "Process Hacker", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);

                    if (result == DialogResult.Cancel)
                        return;
                }
            }
        }

        private void resumeMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listProcesses.SelectedItems)
            {
                Process process;

                try
                {
                    process = Process.GetProcessById(Int32.Parse(item.SubItems[1].Text));
                }
                catch { return; }

                try
                {
                    foreach (ProcessThread thread in process.Threads)
                    {
                        int handle = Win32.OpenThread(Win32.THREAD_SUSPEND_RESUME, 0, thread.Id);

                        if (handle == 0)
                        {
                            throw new Exception("Could not open process handle.");
                        }

                        if (Win32.ResumeThread(handle) == -1)
                        {
                            Win32.CloseHandle(handle);
                            throw new Exception("Could not resume thread.");
                        }

                        Win32.CloseHandle(handle);
                    }
                }
                catch (Exception ex)
                {
                    DialogResult result = MessageBox.Show("Could not resume process with PID " + item.SubItems[1].Text +
                        ".\n\n" + ex.Message, "Process Hacker", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);

                    if (result == DialogResult.Cancel)
                        return;
                }
            }
        }

        private void closeActiveWindowMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listProcesses.SelectedItems)
            {
                try
                {
                    Process.GetProcessById(Int32.Parse(item.SubItems[1].Text)).Kill();
                }
                catch (Exception ex)
                {
                    DialogResult result = MessageBox.Show("Could not close active window of process \"" + item.SubItems[0].Text +
                        "\" with PID " + item.SubItems[1].Text + ":\n\n" +
                            ex.Message, "Process Hacker", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);

                    if (result == DialogResult.Cancel)
                        return;
                }
            }
        }

        #region Priority

        private void realTimeMenuItem_Click(object sender, EventArgs e)
        {
            SetProcessPriority(ProcessPriorityClass.RealTime);
        }

        private void highMenuItem_Click(object sender, EventArgs e)
        {
            SetProcessPriority(ProcessPriorityClass.High);
        }

        private void aboveNormalMenuItem_Click(object sender, EventArgs e)
        {
            SetProcessPriority(ProcessPriorityClass.AboveNormal);
        }

        private void normalMenuItem_Click(object sender, EventArgs e)
        {
            SetProcessPriority(ProcessPriorityClass.Normal);
        }

        private void belowNormalMenuItem_Click(object sender, EventArgs e)
        {
            SetProcessPriority(ProcessPriorityClass.BelowNormal);
        }

        private void idleMenuItem_Click(object sender, EventArgs e)
        {
            SetProcessPriority(ProcessPriorityClass.Idle);
        }

        #endregion

        private void selectAllMenuItem_Click(object sender, EventArgs e)
        {
            SelectAll(listProcesses);
        }             

        #endregion  

        #region Thread Context Menu

        private void menuThread_Popup(object sender, EventArgs e)
        {
            inspectThreadMenuItem.Enabled = false;

            if (listProcesses.SelectedItems.Count == 0 || listThreads.SelectedItems.Count == 0)
            {
                terminateThreadMenuItem.Enabled = false;
                suspendThreadMenuItem.Enabled = false;
                resumeThreadMenuItem.Enabled = false;
                priorityThreadMenuItem.Enabled = false;
                return;
            }
            else if (listThreads.SelectedItems.Count > 0)
            {      
                if (listThreads.SelectedItems.Count == 1)
                {
                    inspectThreadMenuItem.Enabled = true;
                }

                terminateThreadMenuItem.Enabled = true;
                suspendThreadMenuItem.Enabled = true;
                resumeThreadMenuItem.Enabled = true;
                priorityThreadMenuItem.Enabled = true;
            }

            priorityThreadMenuItem.Text = "&Priority";

            if (listThreads.SelectedItems.Count == 1)
            {
                timeCriticalThreadMenuItem.Checked = false;
                highestThreadMenuItem.Checked = false;
                aboveNormalThreadMenuItem.Checked = false;
                normalThreadMenuItem.Checked = false;
                belowNormalThreadMenuItem.Checked = false;
                lowestThreadMenuItem.Checked = false;
                idleThreadMenuItem.Checked = false;
                terminateThreadMenuItem.Text = "&Terminate Thread";
                suspendThreadMenuItem.Text = "&Suspend Thread";
                resumeThreadMenuItem.Text = "&Resume Thread";

                try
                {
                    Process p = Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text));
                    ProcessThread thread = null;

                    foreach (ProcessThread t in p.Threads)
                    {
                        if (t.Id.ToString() == listThreads.SelectedItems[0].SubItems[0].Text)
                        {
                            thread = t;
                            break;
                        }
                    }

                    if (thread == null)
                        return;

                    switch (thread.PriorityLevel)
                    {
                        case ThreadPriorityLevel.TimeCritical:
                            timeCriticalThreadMenuItem.Checked = true;
                            break;

                        case ThreadPriorityLevel.Highest:
                            highestThreadMenuItem.Checked = true;
                            break;

                        case ThreadPriorityLevel.AboveNormal:
                            aboveNormalThreadMenuItem.Checked = true;
                            break;

                        case ThreadPriorityLevel.Normal:
                            normalThreadMenuItem.Checked = true;
                            break;

                        case ThreadPriorityLevel.BelowNormal:
                            belowNormalThreadMenuItem.Checked = true;
                            break;

                        case ThreadPriorityLevel.Lowest:
                            lowestThreadMenuItem.Checked = true;
                            break;

                        case ThreadPriorityLevel.Idle:
                            idleThreadMenuItem.Checked = true;
                            break;
                    }

                    priorityThreadMenuItem.Enabled = true;
                }
                catch (Exception ex)
                {
                    priorityThreadMenuItem.Text = "(" + ex.Message + ")";
                }
            }
            else
            {
                terminateThreadMenuItem.Text = "&Terminate Threads";
                suspendThreadMenuItem.Text = "&Suspend Threads";
                resumeThreadMenuItem.Text = "&Resume Threads";
                priorityThreadMenuItem.Enabled = false;
            }
        }

        private void inspectThreadMenuItem_Click(object sender, EventArgs e)
        {
            return;
            ThreadWindow window;

            try
            {
                window = Program.GetThreadWindow(processSelectedPID,
                    Int32.Parse(listThreads.SelectedItems[0].SubItems[0].Text),
                    new Program.ThreadWindowInvokeAction(delegate(ThreadWindow f)
                {
                    try
                    {
                        f.Show();
                        f.Activate();
                    }
                    catch
                    { }
                }));
            }
            catch
            { }
        }

        private void terminateThreadMenuItem_Click(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.WarnDangerous && IsDangerousPID(processSelectedPID))
            {
                DialogResult result = MessageBox.Show("The process with PID " + processSelectedPID + " is a system process. Are you" +
                    " sure you want to terminate the selected threads?", "Process Hacker", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2);

                if (result == DialogResult.No)
                    return;
            }

            foreach (ListViewItem item in listThreads.SelectedItems)
            {
                try
                {
                    int handle = Win32.OpenThread(Win32.THREAD_TERMINATE, 0, Int32.Parse(item.SubItems[0].Text));

                    if (handle == 0)
                    {
                        throw new Exception("Could not open thread");
                    }

                    if (Win32.TerminateThread(handle, 0) == 0)
                    {                  
                        Win32.CloseHandle(handle);
                        throw new Exception("Could not terminate thread");
                    }

                    Win32.CloseHandle(handle);

                }
                catch (Exception ex)
                {
                    DialogResult result = MessageBox.Show("Could not terminate thread with ID " + item.SubItems[0].Text + ":\n\n" +
                            ex.Message, "Process Hacker", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);

                    if (result == DialogResult.Cancel)
                        return;
                }
            }
        }

        private void suspendThreadMenuItem_Click(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.WarnDangerous && IsDangerousPID(processSelectedPID))
            {
                DialogResult result = MessageBox.Show("The process with PID " + processSelectedPID + " is a system process. Are you" +
                    " sure you want to suspend the selected threads?", "Process Hacker", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2);

                if (result == DialogResult.No)
                    return;
            }

            foreach (ListViewItem item in listThreads.SelectedItems)
            {
                try
                {
                    int handle = Win32.OpenThread(Win32.THREAD_SUSPEND_RESUME, 0, Int32.Parse(item.SubItems[0].Text));

                    if (handle == 0)
                    {
                        throw new Exception("Could not open thread");
                    }

                    if (Win32.SuspendThread(handle) == -1)
                    {
                        Win32.CloseHandle(handle);
                        throw new Exception("Could not suspend thread");
                    }

                    Win32.CloseHandle(handle);
                }
                catch (Exception ex)
                {
                    DialogResult result = MessageBox.Show("Could not suspend thread with ID " + item.SubItems[0].Text + ":\n\n" +
                            ex.Message, "Process Hacker", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);

                    if (result == DialogResult.Cancel)
                        return;
                }
            }
        }

        private void resumeThreadMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listThreads.SelectedItems)
            {
                try
                {
                    int handle = Win32.OpenThread(Win32.THREAD_SUSPEND_RESUME, 0, Int32.Parse(item.SubItems[0].Text));

                    if (handle == 0)
                    {
                        throw new Exception("Could not open thread");
                    }

                    if (Win32.ResumeThread(handle) == -1)
                    {
                        Win32.CloseHandle(handle);
                        throw new Exception("Could not resume thread");
                    }

                    Win32.CloseHandle(handle);
                }
                catch (Exception ex)
                {
                    DialogResult result = MessageBox.Show("Could not resume thread with ID " + item.SubItems[0].Text + ":\n\n" +
                            ex.Message, "Process Hacker", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);

                    if (result == DialogResult.Cancel)
                        return;
                }
            }
        }

        #region Priority

        private void timeCriticalThreadMenuItem_Click(object sender, EventArgs e)
        {
            SetThreadPriority(ThreadPriorityLevel.TimeCritical);
        }

        private void highestThreadMenuItem_Click(object sender, EventArgs e)
        {
            SetThreadPriority(ThreadPriorityLevel.Highest);
        }

        private void aboveNormalThreadMenuItem_Click(object sender, EventArgs e)
        {
            SetThreadPriority(ThreadPriorityLevel.AboveNormal);
        }

        private void normalThreadMenuItem_Click(object sender, EventArgs e)
        {
            SetThreadPriority(ThreadPriorityLevel.Normal);
        }

        private void belowNormalThreadMenuItem_Click(object sender, EventArgs e)
        {
            SetThreadPriority(ThreadPriorityLevel.BelowNormal);
        }

        private void lowestThreadMenuItem_Click(object sender, EventArgs e)
        {
            SetThreadPriority(ThreadPriorityLevel.Lowest);
        }

        private void idleThreadMenuItem_Click(object sender, EventArgs e)
        {
            SetThreadPriority(ThreadPriorityLevel.Idle);
        }

        #endregion

        private void selectAllThreadMenuItem_Click(object sender, EventArgs e)
        {
            SelectAll(listThreads);
        }

        #endregion

        #region Timers

        private void timerFire_Tick(object sender, EventArgs e)
        {
            ProcessQueueUpdated();
            ThreadQueueUpdated();
            UpdateMiscInfo();
        }

        #endregion

        #endregion

        #region Form-related Helper functions

        private void AddIcon(Icon icon)
        {
            imageList.Images.Add(icon);
        }

        private void AddListViewItem(ListView lv, string[] text)
        {
            ListViewItem item = new ListViewItem();

            item.Text = text[0];

            for (int i = 1; i < text.Length; i++)
            {
                item.SubItems.Add(new ListViewItem.ListViewSubItem(item, text[i]));
            }
        }

        private void CloseVirtualProtect()
        {
            this.AcceptButton = null;
            virtualProtectProcess = null;
            panelVirtualProtect.Visible = false;
            listProcesses.Enabled = true;
            tabControl.Enabled = true;
            listThreads.Enabled = true;
        }

        private void DeselectAll(ListView list)
        {
            foreach (ListViewItem item in list.SelectedItems)
                item.Selected = false;
        }

        private void DisableAllMenuItems(ContextMenu menu)
        {
            foreach (MenuItem item in menu.MenuItems)
                item.Enabled = false;
        }

        private void EnableAllMenuItems(ContextMenu menu)
        {
            foreach (MenuItem item in menu.MenuItems)
                item.Enabled = true;
        }

        private void LoadSettings()
        {
            RefreshInterval = Properties.Settings.Default.RefreshInterval;
            this.Location = Properties.Settings.Default.WindowLocation;
            this.Size = Properties.Settings.Default.WindowSize;
            this.WindowState = Properties.Settings.Default.WindowState;
            splitMain.SplitterDistance = Properties.Settings.Default.SplitterDistance;
            buttonSearch.Text = Properties.Settings.Default.SearchType;
        }

        private void PerformSearch(string text)
        {
            ResultsWindow rw = Program.GetResultsWindow(processSelectedPID, new Program.ResultsWindowInvokeAction(delegate(ResultsWindow f)
            {
                if (text == "&New Results Window...")
                {
                    f.Show();
                }
                else if (text == "&Literal Search...")
                {
                    if (f.EditSearch(SearchType.Literal) == DialogResult.OK)
                    {
                        f.Show();
                        f.StartSearch();
                    }
                    else
                    {
                        f.Close();
                    }
                }
                else if (text == "&Regex Search...")
                {
                    if (f.EditSearch(SearchType.Regex) == DialogResult.OK)
                    {
                        f.Show();
                        f.StartSearch();
                    }
                    else
                    {
                        f.Close();
                    }
                }
                else if (text == "&String Scan...")
                {
                    f.SearchOptions.Type = SearchType.String;
                    f.Show();
                    f.StartSearch();
                }
                else if (text == "&Heap Scan...")
                {
                    f.SearchOptions.Type = SearchType.Heap;
                    f.Show();
                    f.StartSearch();
                }
            }));

            buttonSearch.Text = text;
        }

        private void PerformSearch(object sender, EventArgs e)
        {
            PerformSearch(((MenuItem)sender).Text);
        }

        private MemoryEditor ReadWriteMemory()
        {
            return ReadWriteMemory(false);
        }

        private MemoryEditor ReadWriteMemory(bool RO)
        {
            try
            {
                MemoryEditor ed = null;

                this.Cursor = Cursors.WaitCursor;

                ed = Program.GetMemoryEditor(processSelectedPID, memoryAddress, memorySize,
                    new Program.MemoryEditorInvokeAction(delegate(MemoryEditor f)
                {
                    try
                    {
                        f.ReadOnly = RO;
                        f.Show();
                        f.Activate();
                    }
                    catch
                    { }
                }));

                this.Cursor = Cursors.Default;

                return ed;
            }
            catch
            {
                this.Cursor = Cursors.Default;

                return null;
            }
        }

        public void ReloadProcessList()
        {
            processUpdaterThread.Suspend();

            pids = new List<int>();
            processMemoryUsage = new System.Collections.Hashtable();
            processUsername = new System.Collections.Hashtable();
            processTotalMilliseconds = new System.Collections.Hashtable();

            this.Cursor = Cursors.WaitCursor;

            listProcesses.BeginUpdate();
            processListUpdatedOnce = false;
            listProcesses.Items.Clear();
            UpdateProcessExtra();

            processUpdaterThread.Resume();
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.RefreshInterval = RefreshInterval;

            if (this.WindowState != FormWindowState.Minimized)
            {
                Properties.Settings.Default.WindowLocation = this.Location;
                Properties.Settings.Default.WindowSize = this.Size;
            }

            Properties.Settings.Default.WindowState = this.WindowState == FormWindowState.Minimized ?
                FormWindowState.Normal : this.WindowState;
            Properties.Settings.Default.SplitterDistance = splitMain.SplitterDistance;

            Properties.Settings.Default.SearchType = buttonSearch.Text;

            try
            {
                Properties.Settings.Default.Save();
            }
            catch
            { }
        }

        private void SelectAll(ListView list)
        {
            foreach (ListViewItem item in list.Items)
                item.Selected = true;
        }

        private void ShowVirtualProtect()
        {
            panelVirtualProtect.Visible = true;
            panelVirtualProtect.BringToFront();
            textNewProtection.SelectAll();
            textNewProtection.Focus();
            this.AcceptButton = buttonVirtualProtect;
            listProcesses.Enabled = false;
            tabControl.Enabled = false;
            listThreads.Enabled = false;
        }

        #endregion   

        #region Helper functions

        private bool IsDangerousPID(int pid)
        {
            if (pid == 4)
                return true;

            try
            {
                Process p = Process.GetProcessById(pid);

                foreach (string s in dangerousNames)
                {
                    if ((Environment.SystemDirectory + "\\" + s).ToLower() == Misc.GetRealPath(p.MainModule.FileName).ToLower())
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private void SetProcessPriority(ProcessPriorityClass priority)
        {
            try
            {
                Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text)).PriorityClass = priority;
            }
            catch (Exception ex)
            {
                MessageBox.Show("The priority could not be set:\n\n" + ex.Message, "Process Hacker", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetThreadPriority(ThreadPriorityLevel priority)
        {
            try
            {
                Process p = Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text));
                ProcessThread thread = null;

                foreach (ProcessThread t in p.Threads)
                {
                    if (t.Id.ToString() == listThreads.SelectedItems[0].SubItems[0].Text)
                    {
                        thread = t;
                        break;
                    }
                }

                if (thread == null)
                {
                    MessageBox.Show("Thread not found.", "Process Hacker", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                thread.PriorityLevel = priority;
            }
            catch (Exception ex)
            {
                MessageBox.Show("The priority could not be set:\n\n" + ex.Message, "Process Hacker", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Updaters  

        #region Misc Decl.

        delegate string MiscInfoDelegate(Process p);

        string[] misctoplevel = { "Process", "DEP", "Handles", "Memory" };

        string[][] miscinfo = {
                                  new string[] { "Priority Boost Enabled", "Total CPU Time",
                                  "Privileged CPU Time", "User CPU Time", "Start Time"},
                                  new string[] { "Status", "Permanent" },
                                  new string[] { "Handle Count" },
                                  new string[] { "Non-paged System Memory Size", "Paged Memory Size", 
                                      "Paged System Memory Size", "Peak Paged Memory Size", "Peak Virtual Memory Size", 
                                      "Peak Working Set", "Private Memory Size", "Virtual Memory Size", "Working Set"}
                              };

        MiscInfoDelegate[][] miscinfofuncs = {
                                  // Process
                                  new MiscInfoDelegate[]
                                  {
                                      delegate (Process p)
                                      {                    
                                          return p.PriorityBoostEnabled ? "Yes" : "No";
                                      },  

                                      delegate (Process p)
                                      {
                                          return Misc.GetNiceTimeSpan(p.TotalProcessorTime);
                                      },            

                                      delegate (Process p)
                                      {
                                          return Misc.GetNiceTimeSpan(p.PrivilegedProcessorTime);
                                      },    

                                      delegate (Process p)
                                      {
                                          return Misc.GetNiceTimeSpan(p.UserProcessorTime);
                                      },

                                      delegate (Process p)
                                      {
                                          return Misc.GetNiceDateTime(p.StartTime);
                                      }
                                  },

                                  // DEP
                                  new MiscInfoDelegate[]
                                  {
                                      delegate (Process p)
                                      {
                                          Win32.DEPFLAGS flags = 0;
                                          int perm = 0;

                                          Win32.GetProcessDEPPolicy(p.Handle.ToInt32(), ref flags, ref perm);

                                          return flags == Win32.DEPFLAGS.PROCESS_DEP_DISABLE ? "Disabled" :
                                              (flags == Win32.DEPFLAGS.PROCESS_DEP_ENABLE ? "Enabled" :
                                              (flags == (Win32.DEPFLAGS.PROCESS_DEP_ENABLE |
                                              Win32.DEPFLAGS.PROCESS_DEP_DISABLE_ATL_THUNK_EMULATION)) ? 
                                              "Enabled, DEP-ATL thunk emulation disabled" : "Unknown"
                                              );
                                      },

                                      delegate (Process p) 
                                      {     
                                          Win32.DEPFLAGS flags = 0;
                                          int perm = 0;

                                          Win32.GetProcessDEPPolicy(p.Handle.ToInt32(), ref flags, ref perm);

                                          return perm == 0 ? "No" : "Yes";
                                      }
                                  },

                                  // Handles
                                  new MiscInfoDelegate[]
                                  {
                                      delegate (Process p) { return p.HandleCount.ToString(); }   
                                  },

                                  // Memory
                                  new MiscInfoDelegate[]
                                  {
                                      delegate (Process p) { return Misc.GetNiceSizeName(p.NonpagedSystemMemorySize64); },    
                                      delegate (Process p) { return Misc.GetNiceSizeName(p.PagedMemorySize64); },            
                                      delegate (Process p) { return Misc.GetNiceSizeName(p.PagedSystemMemorySize64); },  
                                      delegate (Process p) { return Misc.GetNiceSizeName(p.PeakPagedMemorySize64); },  
                                      delegate (Process p) { return Misc.GetNiceSizeName(p.PeakVirtualMemorySize64); },   
                                      delegate (Process p) { return Misc.GetNiceSizeName(p.PeakWorkingSet64); },  
                                      delegate (Process p) { return Misc.GetNiceSizeName(p.PrivateMemorySize64); },
                                      delegate (Process p) { return Misc.GetNiceSizeName(p.VirtualMemorySize64); },
                                      delegate (Process p) { return Misc.GetNiceSizeName(p.WorkingSet64); }  
                                  }
                              };

        #endregion

        private void InitMiscInfo()
        {
            treeMisc.BeginUpdate();

            TreeNode n;

            for (int i = 0; i < misctoplevel.Length; i++)
            {
                n = treeMisc.Nodes.Add(misctoplevel[i]);

                for (int j = 0; j < miscinfo[i].Length; j++)
                {
                    n.Nodes.Add(miscinfo[i][j] + ": Unknown");
                }
            }

            treeMisc.ExpandAll();

            treeMisc.EndUpdate();
        }

        private void UpdateMiscInfo()
        {
            Process p;

            try
            {
                p = Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text));
            }
            catch
            {
                return;
            }

            treeMisc.BeginUpdate();

            for (int i = 0; i < misctoplevel.Length; i++)
            {
                for (int j = 0; j < miscinfo[i].Length; j++)
                {
                    try
                    {
                        string newtext = miscinfo[i][j] + ": " + 
                            miscinfofuncs[i][j].Invoke(p);

                        if (treeMisc.Nodes[i].Nodes[j].Text != newtext)
                            treeMisc.Nodes[i].Nodes[j].Text = newtext;
                    }
                    catch
                    {
                        treeMisc.Nodes[i].Nodes[j].Text = miscinfo[i][j] +
                            ": Unknown";
                    }
                }
            }

            p.Close();

            treeMisc.EndUpdate();
        }

        private void UpdateDriversInfo()
        {
            int RequiredSize = 0;
            int[] ImageBases;
            List<int> done = new List<int>();
            ListViewItem primary = null;

            Win32.EnumDeviceDrivers(null, 0, ref RequiredSize);

            ImageBases = new int[RequiredSize];

            Win32.EnumDeviceDrivers(ImageBases, RequiredSize * sizeof(int), ref RequiredSize);

            listModules.BeginUpdate();

            for (int i = 0; i < RequiredSize; i++)
            {
                if (done.Contains(ImageBases[i]))
                    break;

                if (ImageBases[i] == 0)
                    continue;

                StringBuilder name = new StringBuilder(256);
                StringBuilder filename = new StringBuilder(256);
                string realname = "";
                string desc = "";
                ListViewItem item = new ListViewItem();

                Win32.GetDeviceDriverBaseName(ImageBases[i], name, 255);
                Win32.GetDeviceDriverFileName(ImageBases[i], filename, 255);

                try
                {
                    System.IO.FileInfo fi = new System.IO.FileInfo(Misc.GetRealPath(filename.ToString()));

                    realname = fi.FullName;

                    desc = FileVersionInfo.GetVersionInfo(realname).FileDescription;
                }
                catch
                { }

                item = new ListViewItem();

                item.SubItems.Add(new ListViewItem.ListViewSubItem());
                item.SubItems.Add(new ListViewItem.ListViewSubItem());
                item.SubItems.Add(new ListViewItem.ListViewSubItem());

                item.ToolTipText = realname;
                item.SubItems[0].Text = name.ToString();
                item.SubItems[1].Text = String.Format("0x{0:x8}", ImageBases[i]);
                item.SubItems[2].Text = "";
                item.SubItems[3].Text = desc;

                try
                {            
                    bool kernel = false;

                    foreach (string k in kernelNames)
                    {
                        if (realname.ToLower() == Environment.SystemDirectory.ToLower() + "\\" + k.ToLower())
                        {
                            kernel = true;

                            break;
                        }
                    }

                    if (kernel)
                    {
                        primary = item;
                    }
                    else
                    {
                        listModules.Items.Add(item);
                    }
                }
                catch
                { }

                done.Add(ImageBases[i]);
            }

            // sorts the list
            listModules.Sorting = SortOrder.Ascending;
            listModules.Sorting = SortOrder.None;

            if (primary != null)
            {
                primary.Font = new Font(primary.Font, FontStyle.Bold);
                listModules.Items.Insert(0, primary);
            }

            listModules.EndUpdate();
        }

        private void UpdateMemoryInfo()
        {
            Process p;

            try
            {
                p = Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text));
            }
            catch
            {
                return;
            }

            Win32.MEMORY_BASIC_INFORMATION info = new Win32.MEMORY_BASIC_INFORMATION();
            int address = 0;

            listMemory.BeginUpdate();
            try
            {
                while (true)
                {
                    if (Win32.VirtualQueryEx(p.Handle.ToInt32(), address, ref info, 
                        Marshal.SizeOf(typeof(Win32.MEMORY_BASIC_INFORMATION))) == 0)
                    {
                        break;
                    }
                    else
                    {
                        ListViewItem item = new ListViewItem();

                        item.SubItems.Add(new ListViewItem.ListViewSubItem());
                        item.SubItems.Add(new ListViewItem.ListViewSubItem());
                        item.SubItems.Add(new ListViewItem.ListViewSubItem());
                        item.SubItems.Add(new ListViewItem.ListViewSubItem());

                        item.SubItems[0].Text = String.Format("0x{0:x8}", info.BaseAddress);
                        item.SubItems[1].Text = String.Format("0x{0:x8}", info.RegionSize);
                        item.SubItems[2].Text = info.State.ToString().Replace("MEM_", "").Replace("0", "");
                        item.SubItems[3].Text = info.Type.ToString().Replace("MEM_", "").Replace("0", "");
                        item.SubItems[4].Text = info.Protect.ToString().Replace("PAGE_", "");

                        listMemory.Items.Add(item);

                        address += info.RegionSize;
                    }
                }

                tabMemory.Enabled = true;
            }
            catch
            {
                tabMemory.Enabled = false;
            }
            listMemory.EndUpdate();
        }

        // .NET based
        private void UpdateModuleInfo()
        {
            Process p = null;
            ListViewItem primary = null;

            try
            {
                p = Process.GetProcessById(Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text));
            }
            catch
            {
                listModules.Items.Clear();
                listModules.Enabled = false;

                return;
            }

            // Get drivers instead
            if (p.Id == 4)
            {
                UpdateDriversInfo();

                return;
            }

            listModules.BeginUpdate();

            try
            {
                ListViewItem item;

                item = new ListViewItem();

                foreach (ProcessModule m in p.Modules)
                {
                    item = new ListViewItem();
 
                    item.SubItems.Add(new ListViewItem.ListViewSubItem());
                    item.SubItems.Add(new ListViewItem.ListViewSubItem());
                    item.SubItems.Add(new ListViewItem.ListViewSubItem());

                    try { item.ToolTipText = Misc.GetRealPath(m.FileName); }
                    catch { item.ToolTipText = ""; }

                    try { item.SubItems[0].Text = m.ModuleName; }
                    catch { item.SubItems[0].Text = "(error)"; }

                    try { item.SubItems[1].Text = String.Format("0x{0:x8}", m.BaseAddress.ToInt32()); }
                    catch { item.SubItems[1].Text = ""; }

                    try { item.SubItems[2].Text = Misc.GetNiceSizeName(m.ModuleMemorySize); }
                    catch { item.SubItems[2].Text = ""; }

                    try { item.SubItems[3].Text = FileVersionInfo.GetVersionInfo(Misc.GetRealPath(m.FileName)).FileDescription; }
                    catch { item.SubItems[3].Text = ""; }

                    try
                    {
                        if (m.ModuleName.ToLower() == listProcesses.SelectedItems[0].SubItems[0].Text.ToLower())
                        {
                            primary = item;
                        }
                        else
                        {
                            listModules.Items.Add(item);
                        }
                    }
                    catch
                    { }
                }

                // sorts the list
                listModules.Sorting = SortOrder.Ascending;
                listModules.Sorting = SortOrder.None;

                if (primary != null)
                {
                    primary.Font = new Font(primary.Font, FontStyle.Bold);
                    listModules.Items.Insert(0, primary);
                }

                listModules.Enabled = true;
            }
            catch (Exception ex)
            {
                listModules.Items.Clear();
                listModules.Items.Add(ex.Message);
                listModules.Enabled = false;
            }

            listModules.EndUpdate();
        }

        // toolhelp based
        private void UpdateModuleInfo2()
        {
            int pid;
            int snapshot;
            Win32.MODULEENTRY32 module = new Win32.MODULEENTRY32();
            ListViewItem primary = null;

            try
            {
                pid = Int32.Parse(listProcesses.SelectedItems[0].SubItems[1].Text);
            }
            catch
            {
                return;
            }

            // Get drivers instead
            if (pid == 4)
            {
                UpdateDriversInfo();

                return;
            }

            snapshot = Win32.CreateToolhelp32Snapshot(Win32.SnapshotFlags.Module, pid);

            module.dwSize = Marshal.SizeOf(typeof(Win32.MODULEENTRY32));

            if (snapshot != 0 && Marshal.GetLastWin32Error() == 0 && pid != 0)
            {
                listModules.BeginUpdate();

                try
                {
                    ListViewItem item;

                    item = new ListViewItem();

                    Win32.Module32First(snapshot, ref module);

                    do
                    {
                        item = new ListViewItem();

                        item.SubItems.Add(new ListViewItem.ListViewSubItem());
                        item.SubItems.Add(new ListViewItem.ListViewSubItem());

                        item.ToolTipText = module.szExePath;
                        item.SubItems[0].Text = module.szModule;
                        item.SubItems[1].Text = String.Format("0x{0:x8}", module.modBaseAddr);
                        item.SubItems[2].Text = Misc.GetNiceSizeName(module.modBaseSize);

                        if (module.szModule.ToLower() == listProcesses.SelectedItems[0].SubItems[0].Text.ToLower())
                        {
                            primary = item;
                        }
                        else
                        {
                            listModules.Items.Add(item);
                        }
                    } while (Win32.Module32Next(snapshot, ref module) != 0);

                    // sorts the list
                    listModules.Sorting = SortOrder.Ascending;
                    listModules.Sorting = SortOrder.None;

                    if (primary != null)
                    {
                        primary.Font = new Font(primary.Font, FontStyle.Bold);
                        listModules.Items.Insert(0, primary);
                    }

                    listModules.Enabled = true;
                }
                catch (Exception ex)
                {
                    listModules.Items.Clear();
                    listModules.Items.Add(ex.Message);
                    listModules.Enabled = false;
                }
            }
            else if (pid == 0)
            {
                listModules.Items.Clear();
                listModules.Enabled = false;
            }
            else if (Marshal.GetLastWin32Error() == 5)
            {
                listModules.Items.Clear();
                listModules.Items.Add("Access is denied.");
                listModules.Enabled = false;
            }
            else
            {
                listModules.Items.Clear();
                listModules.Items.Add("Error " + Marshal.GetLastWin32Error() + ".");
                listModules.Enabled = false;
            }

            listModules.EndUpdate();
        }

        private void UpdateProcessExtra()
        {
            listModules.Items.Clear();
            listMemory.Items.Clear();

            GC.Collect();

            if (listProcesses.SelectedItems.Count != 1)
                return;

            this.Cursor = Cursors.WaitCursor;
            Application.DoEvents();

            UpdateModuleInfo();
            UpdateMemoryInfo();
            UpdateMiscInfo();
            DoThreadListUpdate();
            ThreadQueueUpdated();
                             
            this.Cursor = Cursors.Default;
        }

        #endregion

        private void formViewer_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Stop threads
            processUpdaterThread.Abort();
            threadUpdaterThread.Abort();

            SaveSettings();

            // kill, just in case we are forming an operation we don't want random .net errors about disposed objects.
            Process.GetCurrentProcess().Kill();
        }

        public HackerWindow()
        {
            InitializeComponent();

            this.Cursor = Cursors.WaitCursor;
            listProcesses.BeginUpdate();
            
            PropertyInfo property = typeof(ListView).GetProperty("DoubleBuffered", 
                BindingFlags.NonPublic | BindingFlags.Instance);
                                 
            property.SetValue(listMemory, true, null);
            property.SetValue(listModules, true, null);
            property.SetValue(listProcesses, true, null);
            property.SetValue(listThreads, true, null);
            typeof(TreeView).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(
                treeMisc, true, null);

            if (Win32.EnableTokenPrivilege("SeDebugPrivilege") == 0)
                MessageBox.Show("Debug privilege could not be acquired!" +
                    " This will result in reduced functionality.", "Process Hacker",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

            InitMiscInfo();

            processUpdaterThread = new Thread(new ThreadStart(ProcessListUpdater));
            processUpdaterThread.Priority = ThreadPriority.Lowest;
            processUpdaterThread.Start();

            threadUpdaterThread = new Thread(new ThreadStart(ThreadListUpdater));
            threadUpdaterThread.Priority = ThreadPriority.Lowest;
            threadUpdaterThread.Start();

            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            timerFire.Interval = RefreshInterval;
            timerFire.Enabled = true;
            timerFire_Tick(null, null);

            listProcesses_SelectedIndexChanged(null, null);
            tabControl.SelectedTab = tabProcess;

            newResultsWindowMenuItem.Click +=new EventHandler(PerformSearch);
            literalSearchMenuItem.Click += new EventHandler(PerformSearch);
            regexSearchMenuItem.Click += new EventHandler(PerformSearch);
            stringScanMenuItem.Click += new EventHandler(PerformSearch);
            heapScanMenuItem.Click += new EventHandler(PerformSearch);

            listViews.Add(listProcesses);
            listViews.Add(listThreads);
        }

        private void HackerWindow_Load(object sender, EventArgs e)
        {
            LoadSettings();
            Program.UpdateWindows();
        }
    }
}                           