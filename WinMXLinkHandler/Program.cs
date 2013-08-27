/*
 *  WinMX Link Handler - An application that allows you to join WinMX chat rooms using new WinMX magnet links.
 *  Copyright (C) 2013 WinMX Unlimited
 *  Copyright (C) 2013 Josh Glazebrook
 *
 *  WinMX Link Handler is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  WinMX Link Handler is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace WinMXLinkHandler
{
    class Program
    {

        #region "Dll Imports"
        // Win32 DLL References
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, IntPtr windowTitle);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, StringBuilder lParam);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public static int WM_SETTEXT = 0x0C;
        public static int WM_GETTEXT = 0x0D;
        public static int VK_RETURN = 0x0D;
        public static int WM_KEYDOWN = 0x0100;
        #endregion


        static void Main(string[] args)
        { 
            try
            {
                if (args.Length == 1)
                {
                    string input = System.Web.HttpUtility.UrlDecode(args[0]);

                    // Check for mxlnk: or mxlnk:// 
                    if (input.ToLower().StartsWith("mxlnk://") || input.ToLower().StartsWith("mxlnk:"))
                    {
                        // Remove leading link headers
                        input = Regex.Replace(input, "([Mm][Xx][Ll][Nn][Kk]://)|([Mm][Xx][Ll][Nn][Kk]:)", "").TrimEnd('/');


                        // Decode & Decompress
                        byte[] buff = Convert.FromBase64String(input);
                        string result = "";

                        using (var mem = new MemoryStream(buff))
                        {
                            mem.Position = 0;
                            using (var gz = new DeflateStream(mem, CompressionMode.Decompress))
                            {
                                using (var reader = new StreamReader(gz, Encoding.Default))
                                {
                                    result = reader.ReadToEnd();
                                    buff = System.Text.Encoding.Default.GetBytes(result);
                                }
                            }
                        }

                        // Check for mxlnk type ident byte. (0x47 == ChatRoom)
                        if (buff[0] == 0x47)
                        {
                            // Split Information Chunks. (RoomName, Users, Limit, Topic, Extra)
                            string[] info = result.Substring(1, result.Length - 1).Split(new char[] { '\0' }, StringSplitOptions.None);

                            // Validate Information
                            if (info.Length == 5 && info[0].Length > 12 && Regex.IsMatch(info[0].Substring(info[0].Length - 12, 12), "([0-F]{12})"))
                            {
                                JoinChat(info[0]);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        static void JoinChat(string RoomName)
        {
            IntPtr RoomList = FindRoomListWindow();

            if (RoomList != IntPtr.Zero)
            {
                IntPtr edit = FindWindowEx(RoomList, IntPtr.Zero, "edit", null);
                
                // Read Filter Text
                StringBuilder FilterContent = new StringBuilder(100);

                SendMessage(edit, WM_GETTEXT, new IntPtr(100), FilterContent);
                
                // Set Filter Text To Room Name
                SendMessage(edit, WM_SETTEXT, new IntPtr(RoomName.Length), new StringBuilder(RoomName));

                // Send Enter Key
                PostMessage(edit, WM_KEYDOWN, new IntPtr(VK_RETURN), IntPtr.Zero);

                // Set Filter Text Back To Original
                // SendMessage(edit, WM_SETTEXT, new IntPtr(FilterContent.Length), FilterContent);
            }
        }

        static IntPtr FindRoomListWindow()
        {
            // Check for floating window first
            IntPtr TopWindow = FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, null);
            while (TopWindow != IntPtr.Zero)
            {
                StringBuilder Title = new StringBuilder(100);
                GetWindowText(TopWindow, Title, 100);

                if (Title.ToString() == "WinMX Peer Network")
                {
                    return TopWindow;
                }
                TopWindow = FindWindowEx(IntPtr.Zero, TopWindow, null, null);
            }

            // Check for inner window

            TopWindow = FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, null);

            while (TopWindow != IntPtr.Zero)
            {
                StringBuilder Title = new StringBuilder(100);
                GetWindowText(TopWindow, Title, 100);

                if (Title.ToString().StartsWith("WinMX v3."))
                {
                    IntPtr InnerWindow = FindWindowEx(TopWindow, IntPtr.Zero, null, null);
                    while (InnerWindow != IntPtr.Zero)
                    {
                        Title = new StringBuilder(100);
                        GetWindowText(InnerWindow, Title, 100);
                        if (Title.ToString().Contains("WinMX Peer Network") && Title.ToString().Contains("on WinMX Peer Network") == false)
                        {
                            return InnerWindow;
                        }
                        InnerWindow = FindWindowEx(TopWindow, InnerWindow, null, null);
                    }
                }


                TopWindow = FindWindowEx(IntPtr.Zero, TopWindow, null, null);
            }

            // Failed
            return IntPtr.Zero;
        }


    }
}
