﻿using MediaCrush;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace MediaCrushWindows
{
    static class Program
    {
        static UploadWindow UploadWindow;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            _hookID = SetHook(_proc);

            var icon = new NotifyIcon();
            icon.Icon = Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().Location);
            icon.Visible = true;
            icon.Text = "Click here to upload files to MediaCrush";

            var menu = new ContextMenu();
            var settings = new MenuItem("Settings");
            var exit = new MenuItem("Exit");
            menu.MenuItems.Add(settings);
            menu.MenuItems.Add(exit);
            exit.Click += (s, e) => Application.Exit();
            settings.Click += settings_Click;
            icon.ContextMenu = menu;

            icon.Click += icon_Click;

            UploadWindow = new UploadWindow();
            UploadWindow.Visibility = System.Windows.Visibility.Hidden;
            UploadWindow.Show();
            UploadWindow.Visibility = System.Windows.Visibility.Hidden;

            Application.ApplicationExit += (s, e) => icon.Dispose();
            Application.Run();
            UnhookWindowsHookEx(_hookID);
        }

        static void icon_Click(object sender, EventArgs e)
        {
            if (UploadWindow.Visibility == System.Windows.Visibility.Visible)
                UploadWindow.Visibility = System.Windows.Visibility.Hidden;
            else
                UploadWindow.Visibility = System.Windows.Visibility.Visible;
        }

        static void settings_Click(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private static void CaptureScreenshot()
        {
            UploadWindow.Dispatcher.Invoke(new Action(() =>
            {
                var screen = CaptureVirtualScreen(); // Capture the screen as it appears the moment they press the key combo
                var tool = new ScreenCapture(screen);
                if (tool.ShowDialog().GetValueOrDefault(true))
                {
                    var bitmap = new Bitmap((int)tool.Selection.Width, (int)tool.Selection.Height);
                    int left = (int)tool.Selection.Left;
                    int top = (int)tool.Selection.Top;
                    using (var graphics = Graphics.FromImage(bitmap))
                        graphics.DrawImage(screen, new Point(-left, -top));
                    var file = Path.GetTempFileName() + ".png";
                    bitmap.Save(file, ImageFormat.Png);
                    UploadWindow.Visibility = System.Windows.Visibility.Visible;
                    UploadWindow.UploadFile(file);
                }
            }));
        }

        private static Bitmap CaptureVirtualScreen()
        {
            int left = SystemInformation.VirtualScreen.Left;
            int top = SystemInformation.VirtualScreen.Top;
            int width = SystemInformation.VirtualScreen.Width;
            int height = SystemInformation.VirtualScreen.Height;

            var bitmap = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(bitmap))
                graphics.CopyFromScreen(left, top, 0, 0, bitmap.Size);
            return bitmap;
        }

        private static bool ControlPressed = false;
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) 
        {
            if (nCode >= 0)
            {
                Keys number = (Keys)Marshal.ReadInt32(lParam);
                if (wParam.ToInt32() == WM_KEYDOWN)
                {
                    if (number == Keys.LControlKey || number == Keys.RControlKey || number == Keys.Control || number == Keys.ControlKey)
                        ControlPressed = true;
                }
                else if (wParam.ToInt32() == WM_KEYUP)
                {
                    if (number == Keys.LControlKey || number == Keys.RControlKey || number == Keys.Control || number == Keys.ControlKey)
                        ControlPressed = false;
                    else if (number == Keys.PrintScreen && ControlPressed)
                    {
                        var thread = new Thread(new ThreadStart(CaptureScreenshot));
                        thread.SetApartmentState(ApartmentState.STA);
                        thread.Start();
                    }
                }
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }
        private delegate IntPtr LowLevelKeyboardProc( int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);  
    }
}