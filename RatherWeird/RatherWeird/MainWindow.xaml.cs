﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using WindowHook;
using DirtyInvocation;
using Microsoft.Win32;
using RatherWeird.Utility;
using CheckBox = System.Windows.Controls.CheckBox;
using FileDialog = Microsoft.Win32.FileDialog;
using TextBox = System.Windows.Controls.TextBox;

namespace RatherWeird
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly SystemWatcher _systemWatcher = new SystemWatcher();
        private readonly KeyboardWatcher _keyboardWatcher = new KeyboardWatcher();
        private readonly MemoryManipulator _memoryManipulator = new MemoryManipulator();

        private Process _latestRa3 = null;

        private Process LatestRa3
        {
            get { return _latestRa3; }
            set
            {
                if (value.Id != _latestRa3?.Id)
                {
                    _latestRa3 = value;

                    _memoryManipulator.LockProcess();

                    _memoryManipulator.UnlockProcess(LatestRa3,
                        MemoryManipulator.ProcessAccessFlags.All |
                        MemoryManipulator.ProcessAccessFlags.VirtualMemoryOperation);

                    SwapHealthbarLogic();
                }
            }
        }

        private SettingEntries settings;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SystemWatcherSystemChanged(object sender, ProcessArgs e)
        {
            if (e.Process.ProcessName!= Constants.Ra3ProcessName)
                return;

            LatestRa3 = e.Process;
            
            if (settings.RemoveBorder)
            {
                WindowInvocation.DropBorder(e.Process);
                WindowInvocation.ResizeWindow(e.Process);
            }

            if (settings.LockCursor)
                WindowInvocation.LockToProcess(e.Process);

            if (settings.InvokeAltUp)
            {
                Task.Delay(100).ContinueWith(_ =>
                {
                    Messaging.SimulateAltKeyPress(e.Process.MainWindowHandle);
                    // Messaging.InvokeSysKeyPress(e.Process.MainWindowHandle, (uint) Keys.Menu);
                    // Messaging.InvokeSysKeyPress(e.Process.MainWindowHandle, (int)Keys.Menu); // ALT key
                });

            }
        }

        private void chLockCursor_Click(object sender, RoutedEventArgs e)
        {
            var adhocSender = sender as CheckBox;
            settings.LockCursor = adhocSender?.IsChecked == true;
        }

        private void chInvokeAltUp_Click(object sender, RoutedEventArgs e)
        {
            var adhocSender = sender as CheckBox;
            settings.InvokeAltUp = adhocSender?.IsChecked == true;
        }

        

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            settings = Preferences.Load();

            SetupControls();

            _systemWatcher.Hook();
            _keyboardWatcher.HookKeyboard();

            _systemWatcher.ForegroundChanged += SystemWatcherSystemChanged;
            _systemWatcher.ShowWindow += SystemWatcherOnShowWindow;
            _systemWatcher.HideWindow += SystemWatcherOnHideWindow;
            _keyboardWatcher.KeyboardInputChanged += _keyboardWatcher_KeyboardInputChanged;

            var ra3Procs = Process.GetProcessesByName(Constants.Ra3ProcessName);
            if (ra3Procs.Length > 0)
            {
                LatestRa3 = ra3Procs[0];
            }


            DispatcherTimer tmr = new DispatcherTimer();
            tmr.Tick += Tmr_Tick;
            tmr.Interval = new TimeSpan(0, 0, 0, 1);
            // tmr.Start();
        }

        private void SystemWatcherOnHideWindow(object sender, ProcessArgs e)
        {
            // :( ??
        }

        private void SystemWatcherOnShowWindow(object sender, ProcessArgs e)
        {
            if (e.Process.ProcessName != Constants.Ra3ProcessName)
                return;

            LatestRa3 = e.Process;
        }

        private void _keyboardWatcher_KeyboardInputChanged(object sender, KeyboardInputArgs e)
        {
            HookNumpadEnter(e);
        }

        private void HookNumpadEnter(KeyboardInputArgs e)
        {
            if (settings.HookNumpadEnter == false)
                return;

            // Manual filter..
            if (e.Key != Keys.Enter)
                return;

            // Check for first bit which tells that this key is extended:
            // Ref.: https://msdn.microsoft.com/en-us/library/windows/desktop/ms644967(v=vs.85).aspx
            if (e.KeyboardMessage == WM.KeyDown
                && (e.Flags & 1) == 1)
            {

                if (LatestRa3 != null)
                    InvokeEnter(LatestRa3.MainWindowHandle);
            }
        }

        private void Tmr_Tick(object sender, EventArgs e)
        {
            if (LatestRa3 == null)
                return;

            var ra3Connections = Utility.Networking.GetAllTCPConnections()
                .Where(connection => connection.owningPid == LatestRa3.Id);

            foreach (var mibTcprowOwnerPid in ra3Connections)
            {
                Console.WriteLine(mibTcprowOwnerPid.RemoteAddress.ToString());
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _systemWatcher.Unhook();
            _keyboardWatcher.UnhookKeyboard();
            _memoryManipulator.LockProcess();

            Preferences.Write(settings);
        }

        private void SetupControls()
        {
            chInvokeAltUp.IsChecked = settings.InvokeAltUp;
            chLockCursor.IsChecked = settings.LockCursor;
            chLaunchRa3Windowed.IsChecked = settings.LaunchRa3Windowed;
            chRemoveBorders.IsChecked = settings.RemoveBorder;
            chHookNumpadEnter.IsChecked = settings.HookNumpadEnter;
            chSwapHealthbarLogic.IsChecked = settings.SwapHealthbarLogic;
            chDisableWinKey.IsChecked = settings.DisableWinKey;

            txtRa3Path.Text = GetRa3Executable();
        }

        private void chLaunchRa3Windowed_Click(object sender, RoutedEventArgs e)
        {
            var adhocSender = sender as CheckBox;
            settings.LaunchRa3Windowed = adhocSender?.IsChecked == true;
        }

        private bool CheckFileExistence(string fileToCheck)
        {
            try
            {
                return File.Exists(fileToCheck);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string GetRa3ExecutableFromRegistry()
        {
            string pathToRa3 = "";
            try
            {
                pathToRa3 = (string) Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\RA3.exe"
                    , null
                    , null
                );
            }
            catch (Exception)
            {
                // TODO: Report..?
            }

            return pathToRa3;
        }

        private string GetRa3Executable()
        {
            if (CheckFileExistence(settings.Ra3ExecutablePath))
                return settings.Ra3ExecutablePath;

            string pathFromRegistry = GetRa3ExecutableFromRegistry();
            if (CheckFileExistence(pathFromRegistry))
                return pathFromRegistry;

            return "";
        }

        private void chRefreshPathToRa3_Click(object sender, RoutedEventArgs e)
        {
            var adhocSender = sender as CheckBox;
            settings.RefreshPathToRa3 = adhocSender?.IsChecked == true;
        }

        private void btnLaunchRa3_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                string pathToRa3 = GetRa3Executable();
                if (pathToRa3 == "")
                    return; // TODO: Call message!?

                string arguments = settings.LaunchRa3Windowed
                    ? " -win"
                    : "";

                Process.Start(pathToRa3, arguments);
            });
        }

        private void txtRa3Path_TextChanged(object sender, TextChangedEventArgs e)
        {
            var adhocSender = sender as TextBox;
            settings.Ra3ExecutablePath = adhocSender?.Text;
        }

        private void chRemoveBorders_Click(object sender, RoutedEventArgs e)
        {
            var adhocSender = sender as CheckBox;
            settings.RemoveBorder = adhocSender?.IsChecked == true;
        }

        private void InvokeEnter(IntPtr handle)
        {
            // The following describes the lPARAM for KEYDOWN:
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms646280(v=vs.85).aspx
            // lParam needs to have it's OEM value set (probnably 00011100, I didn't change it) and extended type to 0.
            // I copied the the repeat of 1.
            Messaging.SendMessage(handle, (int)Messaging.WM.KeyDown, (uint)Keys.Enter, 0x1C0001);
            Messaging.SendMessage(handle, (int)Messaging.WM.Char, (uint)Keys.Enter, 0x1C0001);
            Messaging.SendMessage(handle, (int)Messaging.WM.KeyUp, (uint)Keys.Enter, 0xC01C0001);
        }
        

        private void chHookNumpadEnter_Click(object sender, RoutedEventArgs e)
        {
            var adhocSender = sender as CheckBox;
            settings.HookNumpadEnter = adhocSender?.IsChecked == true;
        }

        private void chSwapHealthbarLogic_Click(object sender, RoutedEventArgs e)
        {
            var adhocSender = sender as CheckBox;
            settings.SwapHealthbarLogic = adhocSender?.IsChecked == true;

            SwapHealthbarLogic();
        }

        private void SwapHealthbarLogic()
        {
            if (LatestRa3 == null)
            {
                return;
            }
            
            byte byteToWrite = settings.SwapHealthbarLogic ? (byte)116 : (byte)117;
            _memoryManipulator.WriteByte((IntPtr)0x0052EB93, byteToWrite);
        }

        private void SwapWinKeyState(bool disableKey)
        {
            if (disableKey)
            {
                _keyboardWatcher.DisableKey(() =>
                {
                    Process foregroundProcess = WindowInvocation.GetForegroundProcess();

                    if (foregroundProcess == null)
                        return false;

                    return foregroundProcess.ProcessName == Constants.Ra3ProcessName;
                }, Keys.LWin, Keys.RWin);
            }
            else
            {
                _keyboardWatcher.EnableKey(Keys.LWin, Keys.RWin);
            }
        }

        private void chDisableWinKey_Click(object sender, RoutedEventArgs e)
        {
            var adhocSender = sender as CheckBox;
            settings.DisableWinKey = adhocSender?.IsChecked == true;
        }

        private void chDisableWinKey_Checked(object sender, RoutedEventArgs e)
        {
            SwapWinKeyState(true);
        }

        private void chDisableWinKey_Unchecked(object sender, RoutedEventArgs e)
        {
            SwapWinKeyState(false);
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog diag = new Microsoft.Win32.OpenFileDialog();
            diag.Filter = "Red Alert 3 Executable|RA3.exe";

            bool? result = diag.ShowDialog();
            if (result == true)
            {
                settings.Ra3ExecutablePath = diag.FileName;
                txtRa3Path.Text = diag.FileName;
                settings.RefreshPathToRa3 = false;
            }

        }
    }
}
