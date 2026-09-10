using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MonitorRotateTray
{
    // ---------------------------------------------------------------- Win32
    internal static class Native
    {
        public const int ENUM_CURRENT_SETTINGS = -1;

        public const int DM_POSITION           = 0x00000020;
        public const int DM_DISPLAYORIENTATION = 0x00000080;
        public const int DM_PELSWIDTH          = 0x00080000;
        public const int DM_PELSHEIGHT         = 0x00100000;

        public const uint CDS_UPDATEREGISTRY = 0x00000001;
        public const uint CDS_TEST           = 0x00000002;
        public const uint CDS_NORESET        = 0x10000000;

        public const int DISP_CHANGE_SUCCESSFUL = 0;
        public const int DISP_CHANGE_RESTART    = 1;

        public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
        public const int DISPLAY_DEVICE_PRIMARY_DEVICE      = 0x00000004;

        // --- hotkey ---
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;

        // --- shell notify icon ---
        public const int NIM_ADD = 0x0, NIM_MODIFY = 0x1, NIM_DELETE = 0x2, NIM_SETVERSION = 0x4;
        public const int NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04,
                         NIF_INFO = 0x10, NIF_GUID = 0x20, NIF_SHOWTIP = 0x80;
        public const int NOTIFYICON_VERSION_4 = 4;

        public const int NIIF_INFO = 0x01, NIIF_WARNING = 0x02;

        public const int WM_APP = 0x8000;
        public const int WM_TRAYCALLBACK = WM_APP + 100;

        public const int WM_LBUTTONUP   = 0x0202;
        public const int WM_RBUTTONUP   = 0x0205;
        public const int WM_CONTEXTMENU = 0x007B;
        public const int NIN_SELECT     = 0x0400;   // WM_USER + 0
        public const int NIN_KEYSELECT  = 0x0401;   // WM_USER + 1

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public int uVersionOrTimeout;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]  public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY;
            public int dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
            public int dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]  public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        // --- DisplayConfig: only used to recover real EDID monitor names ---
        [StructLayout(LayoutKind.Sequential)]
        public struct LUID { public uint Low; public int High; }

        [StructLayout(LayoutKind.Sequential)]
        public struct PATH_SOURCE_INFO { public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags; }

        [StructLayout(LayoutKind.Sequential)]
        public struct PATH_TARGET_INFO
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx; public uint outputTechnology; public uint rotation;
            public uint scaling; public uint refreshNum; public uint refreshDen; public uint scanLineOrdering;
            [MarshalAs(UnmanagedType.Bool)] public bool targetAvailable; public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PATH_INFO { public PATH_SOURCE_INFO src; public PATH_TARGET_INFO tgt; public uint flags; }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVICE_INFO_HEADER { public uint type; public uint size; public LUID adapterId; public uint id; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct TARGET_DEVICE_NAME
        {
            public DEVICE_INFO_HEADER header; public uint flags; public uint outputTechnology;
            public ushort edidManufactureId; public ushort edidProductCodeId; public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]  public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SOURCE_DEVICE_NAME
        {
            public DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        /// <summary>Overload for the commit call, which passes a NULL device and DEVMODE.</summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int GetDisplayConfigBufferSizes(uint flags, out uint np, out uint nm);

        [DllImport("user32.dll")]
        public static extern int QueryDisplayConfig(uint flags, ref uint np, [Out] PATH_INFO[] p, ref uint nm, [Out] byte[] m, IntPtr cur);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref TARGET_DEVICE_NAME r);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref SOURCE_DEVICE_NAME r);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT p);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        public static int LoWord(IntPtr v) { return unchecked((short)(long)v); }
        public static int HiWord(IntPtr v) { return unchecked((short)((long)v >> 16)); }
    }

    // ------------------------------------------------------------------ Model
    internal class Monitor
    {
        public string Adapter;      // \\.\DISPLAY1
        public string MonitorId;    // MONITOR\CND30FE\{guid}\0006
        public string HardwareId;   // CND30FE
        public string Name;         // MSI MP161E6T
        public int Orientation;     // 0 = 0deg, 1 = 90, 2 = 180, 3 = 270
        public int Width, Height;
        public int PosX, PosY;      // top-left in virtual-desktop coords; primary is (0,0)
        public bool IsPrimary;

        public string OrientationText { get { return Names.Orient(Orientation); } }
        public bool IsPortrait { get { return Orientation == 1 || Orientation == 3; } }
        public int CenterX { get { return PosX + Width / 2; } }
        public int CenterY { get { return PosY + Height / 2; } }
    }

    /// <summary>Which side of the primary display the external monitor sits on.</summary>
    internal enum Placement { Left = 0, Right = 1, Above = 2, Below = 3 }

    /// <summary>
    /// Where along the shared edge the external monitor sits. For Left/Right this is
    /// vertical (Start = top edges level); for Above/Below it is horizontal.
    /// </summary>
    internal enum Align { Start = 0, Center = 1, End = 2 }

    internal static class Names
    {
        public static string Orient(int q)
        {
            switch (q)
            {
                case 1:  return "\u76F4\u5411 90\u00B0";     // 直向 90
                case 2:  return "\u6A6B\u5411 180\u00B0";    // 橫向 180
                case 3:  return "\u76F4\u5411 270\u00B0";    // 直向 270
                default: return "\u6A6B\u5411 0\u00B0";      // 橫向 0
            }
        }
    }

    internal static class Displays
    {
        private static Dictionary<string, string> FriendlyNames()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                uint np = 0, nm = 0;
                if (Native.GetDisplayConfigBufferSizes(2, out np, out nm) != 0) return map;
                var paths = new Native.PATH_INFO[np];
                var modes = new byte[nm * 64];
                if (Native.QueryDisplayConfig(2, ref np, paths, ref nm, modes, IntPtr.Zero) != 0) return map;

                for (int i = 0; i < np; i++)
                {
                    var src = new Native.SOURCE_DEVICE_NAME();
                    src.header.type = 1;
                    src.header.size = (uint)Marshal.SizeOf(typeof(Native.SOURCE_DEVICE_NAME));
                    src.header.adapterId = paths[i].src.adapterId;
                    src.header.id = paths[i].src.id;
                    if (Native.DisplayConfigGetDeviceInfo(ref src) != 0) continue;

                    var tgt = new Native.TARGET_DEVICE_NAME();
                    tgt.header.type = 2;
                    tgt.header.size = (uint)Marshal.SizeOf(typeof(Native.TARGET_DEVICE_NAME));
                    tgt.header.adapterId = paths[i].tgt.adapterId;
                    tgt.header.id = paths[i].tgt.id;
                    if (Native.DisplayConfigGetDeviceInfo(ref tgt) != 0) continue;

                    string name = (tgt.monitorFriendlyDeviceName ?? "").Trim();
                    if (name.Length > 0 && !map.ContainsKey(src.viewGdiDeviceName))
                        map[src.viewGdiDeviceName] = name;
                }
            }
            catch { }
            return map;
        }

        public static List<Monitor> Enumerate()
        {
            var names = FriendlyNames();
            var list = new List<Monitor>();
            for (uint i = 0; ; i++)
            {
                var dd = new Native.DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
                if (!Native.EnumDisplayDevices(null, i, ref dd, 0)) break;
                if ((dd.StateFlags & Native.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) continue;

                var dm = new Native.DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(Native.DEVMODE));
                if (!Native.EnumDisplaySettings(dd.DeviceName, Native.ENUM_CURRENT_SETTINGS, ref dm)) continue;

                var mon = new Native.DISPLAY_DEVICE();
                mon.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));
                Native.EnumDisplayDevices(dd.DeviceName, 0, ref mon, 0);

                string devId = mon.DeviceID ?? "";
                string hw = "";
                var parts = devId.Split('\\');
                if (parts.Length >= 2) hw = parts[1];

                string friendly;
                if (!names.TryGetValue(dd.DeviceName, out friendly) || string.IsNullOrEmpty(friendly))
                    friendly = string.IsNullOrEmpty(mon.DeviceString) ? dd.DeviceString : mon.DeviceString;

                list.Add(new Monitor
                {
                    Adapter = dd.DeviceName,
                    MonitorId = devId,
                    HardwareId = hw,
                    Name = friendly,
                    Orientation = dm.dmDisplayOrientation,
                    Width = dm.dmPelsWidth,
                    Height = dm.dmPelsHeight,
                    PosX = dm.dmPositionX,
                    PosY = dm.dmPositionY,
                    IsPrimary = (dd.StateFlags & Native.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0
                });
            }
            return list;
        }

        /// <summary>Which side of the primary the monitor currently sits on, by centre offset.</summary>
        public static Placement InferPlacement(Monitor primary, Monitor target)
        {
            int dx = target.CenterX - primary.CenterX;
            int dy = target.CenterY - primary.CenterY;
            if (Math.Abs(dx) >= Math.Abs(dy)) return dx < 0 ? Placement.Left : Placement.Right;
            return dy < 0 ? Placement.Above : Placement.Below;
        }

        /// <summary>
        /// Top-left corner that puts a targetW x targetH monitor flush against the primary
        /// on the requested side. The primary always sits at (0,0), so the two rectangles
        /// share an edge exactly - no gaps, no diagonal offsets, no overlap.
        /// </summary>
        public static void ComputePosition(Monitor primary, int targetW, int targetH,
                                           Placement p, Align a, out int x, out int y)
        {
            int pw = primary.Width, ph = primary.Height;

            if (p == Placement.Left || p == Placement.Right)
            {
                x = (p == Placement.Left) ? -targetW : pw;
                if (a == Align.Start) y = 0;
                else if (a == Align.End) y = ph - targetH;
                else y = (ph - targetH) / 2;
            }
            else
            {
                y = (p == Placement.Above) ? -targetH : ph;
                if (a == Align.Start) x = 0;
                else if (a == Align.End) x = pw - targetW;
                else x = (pw - targetW) / 2;
            }
        }

        /// <summary>
        /// Set orientation and position in one shot. Doing them together matters: if the
        /// size changes first, Windows reflows the desktop on its own and drops the monitor
        /// wherever it likes - which is exactly the "position keeps moving" problem.
        /// Returns null on success, otherwise a human-readable error.
        /// </summary>
        public static string ApplyLayout(Monitor target, Monitor primary, int orientation,
                                         Placement p, Align a)
        {
            if (orientation < 0 || orientation > 3) return "\u65B9\u5411\u503C\u7121\u6548";

            var dm = new Native.DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(Native.DEVMODE));
            if (!Native.EnumDisplaySettings(target.Adapter, Native.ENUM_CURRENT_SETTINGS, ref dm))
                return "\u8B80\u53D6\u76EE\u524D\u986F\u793A\u8A2D\u5B9A\u5931\u6557";

            int fields = Native.DM_DISPLAYORIENTATION | Native.DM_PELSWIDTH | Native.DM_PELSHEIGHT;

            if ((((dm.dmDisplayOrientation ^ orientation) & 1)) == 1)
            {
                int t = dm.dmPelsWidth; dm.dmPelsWidth = dm.dmPelsHeight; dm.dmPelsHeight = t;
            }
            dm.dmDisplayOrientation = orientation;

            // The primary is pinned at (0,0) by Windows, so it is only the secondary we place.
            bool reposition = !target.IsPrimary && primary != null && primary.Adapter != target.Adapter;
            if (reposition)
            {
                int x, y;
                ComputePosition(primary, dm.dmPelsWidth, dm.dmPelsHeight, p, a, out x, out y);
                dm.dmPositionX = x;
                dm.dmPositionY = y;
                fields |= Native.DM_POSITION;
            }

            dm.dmFields = fields;

            bool unchanged = dm.dmDisplayOrientation == target.Orientation
                             && (!reposition || (dm.dmPositionX == target.PosX && dm.dmPositionY == target.PosY));
            if (unchanged) return null;

            int rc = Native.ChangeDisplaySettingsEx(target.Adapter, ref dm, IntPtr.Zero, Native.CDS_TEST, IntPtr.Zero);
            if (rc != Native.DISP_CHANGE_SUCCESSFUL)
                return "\u986F\u793A\u6A21\u5F0F\u4E0D\u88AB\u63A5\u53D7 (CDS_TEST rc=" + rc + ")";

            // NORESET stages the change, then the NULL commit applies the whole desktop at
            // once. Without staging, a position change can be rejected as an overlap while
            // the old size is still in effect.
            rc = Native.ChangeDisplaySettingsEx(target.Adapter, ref dm, IntPtr.Zero,
                                                Native.CDS_UPDATEREGISTRY | Native.CDS_NORESET, IntPtr.Zero);
            if (rc != Native.DISP_CHANGE_SUCCESSFUL && rc != Native.DISP_CHANGE_RESTART)
                return "\u6392\u7A0B\u5931\u6557 (rc=" + rc + ")";

            rc = Native.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            if (rc == Native.DISP_CHANGE_SUCCESSFUL || rc == Native.DISP_CHANGE_RESTART) return null;
            return "\u5957\u7528\u5931\u6557 (rc=" + rc + ")";
        }
    }

    // ----------------------------------------------------------------- Config
    internal class Config
    {
        public string TargetHardwareId = "";     // EDID vendor+product, e.g. CND30FE (model level)
        public string TargetNameMatch = "MSI";   // EDID friendly-name substring
        public bool FallbackToExternal = true;   // last resort: any non-primary display
        public int OrientA = 0;   // landscape
        public int OrientB = 3;   // portrait
        public bool Hotkey = true;
        public bool HideWhenDisconnected = true;

        public Placement Placement = Placement.Left;
        public int AlignMode = -1;         // -1 = auto (see EffectiveAlign), else 0/1/2
        public bool KeepLayout = true;     // enforce placement on rotate / reconnect
        public bool LearnPlacement = true; // adopt the side you drag it to in Settings

        /// <summary>
        /// Auto means: side by side sits on the same desk, so bottom edges line up;
        /// stacked means directly above/below, so centre them horizontally.
        /// </summary>
        public Align EffectiveAlign(Placement p)
        {
            if (AlignMode >= 0 && AlignMode <= 2) return (Align)AlignMode;
            return (p == Placement.Left || p == Placement.Right) ? Align.End : Align.Center;
        }

        private static string Dir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "MonitorRotateTray");
            }
        }

        private static string FilePath { get { return Path.Combine(Dir, "config.ini"); } }

        public static Config Load()
        {
            var c = new Config();
            try
            {
                if (!File.Exists(FilePath)) return c;
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1);
                    int semi = v.IndexOf(';');          // trailing "; explanation"
                    if (semi >= 0) v = v.Substring(0, semi);
                    v = v.Trim();
                    int n;
                    if (k == "TargetHardwareId") c.TargetHardwareId = v;
                    else if (k == "TargetNameMatch") c.TargetNameMatch = v;
                    else if (k == "FallbackToExternal") c.FallbackToExternal = IsTrue(v);
                    else if (k == "OrientA" && int.TryParse(v, out n)) c.OrientA = n;
                    else if (k == "OrientB" && int.TryParse(v, out n)) c.OrientB = n;
                    else if (k == "Hotkey") c.Hotkey = IsTrue(v);
                    else if (k == "HideWhenDisconnected") c.HideWhenDisconnected = IsTrue(v);
                    else if (k == "KeepLayout") c.KeepLayout = IsTrue(v);
                    else if (k == "LearnPlacement") c.LearnPlacement = IsTrue(v);
                    else if (k == "Placement" && int.TryParse(v, out n) && n >= 0 && n <= 3)
                        c.Placement = (Placement)n;
                    else if (k == "Align" && int.TryParse(v, out n) && n >= -1 && n <= 2)
                        c.AlignMode = n;
                }
            }
            catch { }
            return c;
        }

        private static bool IsTrue(string v)
        {
            return v == "1" || v.ToLowerInvariant() == "true";
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                sb.AppendLine("TargetHardwareId=" + TargetHardwareId);
                sb.AppendLine("TargetNameMatch=" + TargetNameMatch);
                sb.AppendLine("FallbackToExternal=" + (FallbackToExternal ? "1" : "0"));
                sb.AppendLine("OrientA=" + OrientA);
                sb.AppendLine("OrientB=" + OrientB);
                sb.AppendLine("Hotkey=" + (Hotkey ? "1" : "0"));
                sb.AppendLine("HideWhenDisconnected=" + (HideWhenDisconnected ? "1" : "0"));
                sb.AppendLine("Placement=" + (int)Placement + "   ; 0=左 1=右 2=上 3=下");
                sb.AppendLine("Align=" + AlignMode + "   ; -1=自動(左右→下緣, 上下→水平置中) 0=起始 1=置中 2=結束");
                sb.AppendLine("KeepLayout=" + (KeepLayout ? "1" : "0"));
                sb.AppendLine("LearnPlacement=" + (LearnPlacement ? "1" : "0"));
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(true));
            }
            catch { }
        }
    }

    // -------------------------------------------------------------- Tray icon
    /// <summary>
    /// Shell_NotifyIcon wrapper using NIF_GUID. The GUID is what makes Windows
    /// remember the icon's position in the tray across hide/show and restarts;
    /// WinForms' NotifyIcon keys on (hWnd, uID), both of which change every run,
    /// so it always lands back in the overflow area.
    /// </summary>
    internal class TrayIcon : IDisposable
    {
        // Stable identity. Windows binds this GUID to the executable's full path,
        // so moving the .exe resets the remembered position exactly once.
        private static readonly Guid IconGuid = new Guid("7B2F4C31-9A6D-4E58-B0C7-3D1E8F5A2604");

        private readonly IntPtr _hwnd;
        private bool _shown;
        private bool _useGuid = true;
        private IntPtr _hIcon = IntPtr.Zero;
        private string _tip = "";

        public TrayIcon(IntPtr hwnd) { _hwnd = hwnd; }

        public bool Shown { get { return _shown; } }

        /// <summary>Diagnostics for the startup log: how the icon actually got added.</summary>
        public string LastStatus = "(not attempted)";
        public bool UsingGuid { get { return _useGuid; } }

        /// <summary>True once NIM_SETVERSION(4) succeeded - decides which click events to trust.</summary>
        public bool Version4;

        private Native.NOTIFYICONDATA Base(int flags)
        {
            var d = new Native.NOTIFYICONDATA();
            d.cbSize = Marshal.SizeOf(typeof(Native.NOTIFYICONDATA));
            d.hWnd = _hwnd;
            d.uID = 1;
            d.uCallbackMessage = Native.WM_TRAYCALLBACK;
            d.szTip = _tip ?? "";
            d.szInfo = "";
            d.szInfoTitle = "";
            d.uFlags = flags | (_useGuid ? Native.NIF_GUID : 0);
            if (_useGuid) d.guidItem = IconGuid;
            return d;
        }

        public void Show(IntPtr hIcon, string tip)
        {
            SetIconHandle(hIcon);
            _tip = Trim(tip, 127);

            if (_shown) { Update(hIcon, tip); return; }

            const int ADD_FLAGS = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP | Native.NIF_SHOWTIP;

            var d = Base(ADD_FLAGS);
            d.hIcon = _hIcon;

            if (Native.Shell_NotifyIcon(Native.NIM_ADD, ref d))
            {
                LastStatus = "added with GUID (struct=" + d.cbSize + ")";
            }
            else
            {
                int e1 = Marshal.GetLastWin32Error();

                // A stale GUID registration (e.g. the .exe was moved) makes NIM_ADD
                // fail. Clear it and retry once, then fall back to a plain uID icon.
                var del = Base(0);
                Native.Shell_NotifyIcon(Native.NIM_DELETE, ref del);

                d = Base(ADD_FLAGS);
                d.hIcon = _hIcon;
                if (Native.Shell_NotifyIcon(Native.NIM_ADD, ref d))
                {
                    LastStatus = "added with GUID after clearing stale registration (first err=" + e1 + ")";
                }
                else
                {
                    int e2 = Marshal.GetLastWin32Error();
                    _useGuid = false;
                    d = Base(ADD_FLAGS);
                    d.hIcon = _hIcon;
                    if (Native.Shell_NotifyIcon(Native.NIM_ADD, ref d))
                    {
                        LastStatus = "GUID rejected (err=" + e1 + "/" + e2 + "), fell back to uID mode "
                                   + "- tray position will NOT persist";
                    }
                    else
                    {
                        LastStatus = "FAILED entirely (err=" + e1 + "/" + e2 + "/"
                                   + Marshal.GetLastWin32Error() + ", struct=" + d.cbSize + ")";
                        return;
                    }
                }
            }

            var ver = Base(0);
            ver.uVersionOrTimeout = Native.NOTIFYICON_VERSION_4;
            Version4 = Native.Shell_NotifyIcon(Native.NIM_SETVERSION, ref ver);
            if (!Version4)
                LastStatus += "; SETVERSION failed (err=" + Marshal.GetLastWin32Error()
                            + "), falling back to classic click events";

            _shown = true;
        }

        public void Update(IntPtr hIcon, string tip)
        {
            if (!_shown) { Show(hIcon, tip); return; }
            SetIconHandle(hIcon);
            _tip = Trim(tip, 127);
            var d = Base(Native.NIF_ICON | Native.NIF_TIP | Native.NIF_SHOWTIP);
            d.hIcon = _hIcon;
            Native.Shell_NotifyIcon(Native.NIM_MODIFY, ref d);
        }

        public void Hide()
        {
            if (!_shown) return;
            var d = Base(0);
            Native.Shell_NotifyIcon(Native.NIM_DELETE, ref d);
            _shown = false;
        }

        /// <summary>Re-add after an explorer.exe restart (TaskbarCreated).</summary>
        public void Readd()
        {
            if (!_shown) return;
            _shown = false;
            IntPtr icon = _hIcon;
            _hIcon = IntPtr.Zero;      // keep the handle; Show() would destroy it
            Show(icon, _tip);
        }

        public void Balloon(string title, string text, bool warning)
        {
            if (!_shown) return;
            var d = Base(Native.NIF_INFO);
            d.szInfoTitle = Trim(title, 63);
            d.szInfo = Trim(text, 255);
            d.dwInfoFlags = warning ? Native.NIIF_WARNING : Native.NIIF_INFO;
            Native.Shell_NotifyIcon(Native.NIM_MODIFY, ref d);
        }

        private void SetIconHandle(IntPtr hIcon)
        {
            if (hIcon == _hIcon) return;
            IntPtr old = _hIcon;
            _hIcon = hIcon;
            if (old != IntPtr.Zero) Native.DestroyIcon(old);
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max);
        }

        public void Dispose()
        {
            Hide();
            if (_hIcon != IntPtr.Zero) { Native.DestroyIcon(_hIcon); _hIcon = IntPtr.Zero; }
        }
    }

    // ---------------------------------------------------------- Message window
    internal class MessageWindow : NativeWindow
    {
        private const int HOTKEY_ID = 0xB001;
        private static readonly int WM_TASKBARCREATED = Native.RegisterWindowMessage("TaskbarCreated");

        private readonly Action _onToggle;
        private readonly Action<int, int> _onContextMenu;
        private readonly Action _onTaskbarCreated;
        private readonly Func<bool> _isVersion4;
        private bool _hotkeyRegistered;

        /// <summary>Ring buffer of raw tray callbacks, so a failing click can be traced.</summary>
        public readonly List<string> RawLog = new List<string>();
        public int CallbackCount;

        private void Record(string s)
        {
            CallbackCount++;
            RawLog.Add(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s);
            if (RawLog.Count > 20) RawLog.RemoveAt(0);
        }

        public MessageWindow(Action onToggle, Action<int, int> onContextMenu, Action onTaskbarCreated,
                             Func<bool> isVersion4)
        {
            _onToggle = onToggle;
            _onContextMenu = onContextMenu;
            _onTaskbarCreated = onTaskbarCreated;
            _isVersion4 = isVersion4;
            CreateHandle(new CreateParams());
        }

        public bool RegisterHotkey()
        {
            if (_hotkeyRegistered) return true;
            _hotkeyRegistered = Native.RegisterHotKey(Handle, HOTKEY_ID,
                Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)Keys.R);
            return _hotkeyRegistered;
        }

        public void UnregisterHotkey()
        {
            if (!_hotkeyRegistered) return;
            Native.UnregisterHotKey(Handle, HOTKEY_ID);
            _hotkeyRegistered = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                Record("HOTKEY");
                _onToggle();
                return;
            }

            if (m.Msg == Native.WM_TRAYCALLBACK)
            {
                // NOTIFYICON_VERSION_4: wParam = anchor x/y, lParam = event id | (uID << 16)
                // Classic (version 0):    wParam = uID,      lParam = the mouse message
                int evt = Native.LoWord(m.LParam);
                int x = Native.LoWord(m.WParam);
                int y = Native.HiWord(m.WParam);

                // Version 4 delivers BOTH the raw mouse messages and the NIN_* events for
                // a single click. Acting on both fires the action twice - a toggle would
                // rotate and immediately rotate back, looking like nothing happened. So in
                // v4 listen only to NIN_*; the raw messages are the fallback for v0.
                bool v4 = _isVersion4 != null && _isVersion4();
                bool select, menu;
                if (v4)
                {
                    select = (evt == Native.NIN_SELECT || evt == Native.NIN_KEYSELECT);
                    menu = (evt == Native.WM_CONTEXTMENU);
                }
                else
                {
                    select = (evt == Native.WM_LBUTTONUP);
                    menu = (evt == Native.WM_CONTEXTMENU || evt == Native.WM_RBUTTONUP);
                }

                Record(string.Format("TRAY evt=0x{0:X4} w=0x{1:X8} l=0x{2:X8} v4={3}{4}",
                    evt, m.WParam.ToInt64(), m.LParam.ToInt64(), v4,
                    select ? " -> toggle" : (menu ? " -> menu" : " -> ignored")));

                if (select) _onToggle();
                else if (menu) _onContextMenu(x, y);
                return;
            }

            if (m.Msg == WM_TASKBARCREATED)
            {
                _onTaskbarCreated();
                return;
            }

            base.WndProc(ref m);
        }
    }

    // -------------------------------------------------------------------- App
    internal class TrayApp : ApplicationContext
    {
        private const string PipeName = "MonitorRotateTray";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "MonitorRotateTray";

        private readonly Config _cfg;
        private readonly Control _sync;
        private readonly MessageWindow _win;
        private readonly TrayIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly System.Windows.Forms.Timer _watchdog;

        private volatile bool _exiting;
        private string _lastState = null;   // used to skip redundant icon updates
        private bool _wasPresent;           // for detecting the reconnect edge
        private bool _applying;             // guards against re-entrant Refresh while we change modes

        // Hotplug is not atomic: Windows parks the monitor somewhere of its own choosing,
        // fires several change events, and may reflow again after we act. So we (a) never
        // trust geometry until it has held still, and (b) keep re-asserting for a while
        // after a reconnect in case Windows moves it back.
        private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(1500);
        private static readonly TimeSpan VerifyWindow = TimeSpan.FromSeconds(12);
        private string _lastSignature;
        private DateTime _stableSince = DateTime.MinValue;
        private DateTime _verifyUntil = DateTime.MinValue;
        private bool _enforcePending;

        public TrayApp()
        {
            _cfg = Config.Load();

            _sync = new Control();
            IntPtr force = _sync.Handle;    // realise the UI-thread window
            GC.KeepAlive(force);

            _menu = new ContextMenuStrip();
            // _tray is assigned immediately below; the probe only runs once messages arrive.
            _win = new MessageWindow(Toggle, ShowMenu, OnTaskbarCreated,
                                     delegate { return _tray != null && _tray.Version4; });
            _tray = new TrayIcon(_win.Handle);

            if (string.IsNullOrEmpty(_cfg.TargetHardwareId))
            {
                var pick = PickDefaultTarget();
                if (pick != null) { _cfg.TargetHardwareId = pick.HardwareId; _cfg.Save(); }
            }

            if (_cfg.Hotkey) _win.RegisterHotkey();

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            // Safety net: DisplaySettingsChanged is reliable, but a cheap poll
            // covers hotplug races during resume-from-sleep.
            _watchdog = new System.Windows.Forms.Timer();
            _watchdog.Interval = 1000;
            _watchdog.Tick += delegate { Refresh(); };
            _watchdog.Start();

            StartPipeServer();
            Refresh();
            WriteStartupLog();
        }

        private void WriteStartupLog() { WriteLog("startup"); }

        /// <summary>Full state snapshot, for troubleshooting. Rewritten on demand by "diag".</summary>
        private void WriteLog(string reason)
        {
            try
            {
                string how;
                var m = Target(out how);
                var all = Displays.Enumerate();

                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonitorRotateTray");
                Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("time        : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "   (" + reason + ")");
                sb.AppendLine("exe         : " + Application.ExecutablePath);
                sb.AppendLine("pid         : " + System.Diagnostics.Process.GetCurrentProcess().Id);
                sb.AppendLine("autostart   : " + IsAutostart());
                sb.AppendLine();
                sb.AppendLine("tray add    : " + _tray.LastStatus);
                sb.AppendLine("guid mode   : " + _tray.UsingGuid + "   (false = position will not persist)");
                sb.AppendLine("version 4   : " + _tray.Version4 + "   (true = act on NIN_* only, not raw clicks)");
                sb.AppendLine("icon shown  : " + _tray.Shown);
                sb.AppendLine("hotkey      : " + _cfg.Hotkey);
                sb.AppendLine();
                sb.AppendLine("match rule  : " + how);
                sb.AppendLine("target      : " + (m != null
                    ? m.Name + "  (" + m.Adapter + ", " + m.OrientationText + ", " + m.Width + "x" + m.Height + ")"
                    : "NONE"));
                sb.AppendLine("toggle pair : " + Names.Orient(_cfg.OrientA) + " <-> " + Names.Orient(_cfg.OrientB));
                sb.AppendLine();
                string[] sideNames = { "左", "右", "上", "下" };
                string[] alignNames = { "起始", "置中", "結束" };
                Align eff = _cfg.EffectiveAlign(_cfg.Placement);
                sb.AppendLine("placement   : " + sideNames[(int)_cfg.Placement]
                    + " / " + alignNames[(int)eff]
                    + (_cfg.AlignMode < 0 ? " (自動)" : " (指定)")
                    + "   keep=" + _cfg.KeepLayout + " learn=" + _cfg.LearnPlacement);
                var prim = Primary();
                if (m != null && prim != null && !m.IsPrimary)
                {
                    int wx, wy;
                    Displays.ComputePosition(prim, m.Width, m.Height, _cfg.Placement, eff, out wx, out wy);
                    sb.AppendLine("position    : actual (" + m.PosX + "," + m.PosY + ")"
                        + "  expected (" + wx + "," + wy + ")"
                        + ((m.PosX == wx && m.PosY == wy) ? "   OK" : "   MISMATCH"));
                }
                sb.AppendLine();
                sb.AppendLine("displays attached:");
                foreach (var d in all)
                    sb.AppendLine("  " + d.Adapter + "  hwid=" + (d.HardwareId == "" ? "(none)" : d.HardwareId)
                        + "  pos=(" + d.PosX + "," + d.PosY + ")"
                        + "  " + d.Width + "x" + d.Height + "  orient=" + d.Orientation
                        + (d.IsPrimary ? "  PRIMARY" : "") + "  \"" + d.Name + "\"");
                sb.AppendLine();
                sb.AppendLine("tray callbacks received: " + _win.CallbackCount);
                if (_win.RawLog.Count == 0)
                    sb.AppendLine("  (none - clicks are NOT reaching the app)");
                else
                    foreach (var l in _win.RawLog) sb.AppendLine("  " + l);

                File.WriteAllText(Path.Combine(dir, "last-run.log"), sb.ToString(), new UTF8Encoding(true));
            }
            catch { }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            try { _sync.BeginInvoke((Action)Refresh); } catch { }
        }

        private void OnTaskbarCreated()
        {
            try { _sync.BeginInvoke((Action)delegate { _tray.Readd(); _lastState = null; Refresh(); }); }
            catch { }
        }

        private static Monitor PickDefaultTarget()
        {
            var all = Displays.Enumerate();
            foreach (var m in all) if (!m.IsPrimary) return m;
            return all.Count > 0 ? all[0] : null;
        }

        private Monitor Target()
        {
            string how;
            return Target(out how);
        }

        private static Monitor Primary()
        {
            foreach (var m in Displays.Enumerate()) if (m.IsPrimary) return m;
            return null;
        }

        /// <summary>
        /// Adopt the side - and, when it is unambiguous, the alignment - that the monitor
        /// actually sits at, so rearranging in Windows Settings sticks. Only ever called on
        /// geometry that has held still, because mid-hotplug Windows parks the monitor in
        /// arbitrary places and learning from that corrupts the memory.
        /// </summary>
        private void LearnLayout(Monitor target)
        {
            if (!_cfg.LearnPlacement || target == null || target.IsPrimary) return;
            var primary = Primary();
            if (primary == null || primary.Adapter == target.Adapter) return;

            bool changed = false;

            Placement actual = Displays.InferPlacement(primary, target);
            if (actual != _cfg.Placement) { _cfg.Placement = actual; changed = true; }

            // Only record an alignment if the user's arrangement differs from what we would
            // apply anyway - otherwise enforcing "auto" would silently harden into a value.
            int wx, wy;
            Displays.ComputePosition(primary, target.Width, target.Height,
                                     _cfg.Placement, _cfg.EffectiveAlign(_cfg.Placement), out wx, out wy);
            if (target.PosX != wx || target.PosY != wy)
            {
                for (int i = 0; i <= 2; i++)
                {
                    int cx, cy;
                    Displays.ComputePosition(primary, target.Width, target.Height,
                                             _cfg.Placement, (Align)i, out cx, out cy);
                    if (target.PosX == cx && target.PosY == cy)
                    {
                        if (_cfg.AlignMode != i) { _cfg.AlignMode = i; changed = true; }
                        break;
                    }
                }
            }

            if (changed) _cfg.Save();
        }

        /// <summary>Re-seat the monitor on its remembered side without changing orientation.</summary>
        private void EnforceLayout(Monitor target)
        {
            if (!_cfg.KeepLayout || target == null || target.IsPrimary) return;
            var primary = Primary();
            if (primary == null || primary.Adapter == target.Adapter) return;

            string err = Displays.ApplyLayout(target, primary, target.Orientation,
                                              _cfg.Placement, _cfg.EffectiveAlign(_cfg.Placement));
            if (err != null) Warn(err);
        }

        /// <summary>True when the monitor is not where the remembered layout says it should be.</summary>
        private bool LayoutDrifted(Monitor target)
        {
            if (target == null || target.IsPrimary) return false;
            var primary = Primary();
            if (primary == null || primary.Adapter == target.Adapter) return false;

            int wx, wy;
            Displays.ComputePosition(primary, target.Width, target.Height,
                                     _cfg.Placement, _cfg.EffectiveAlign(_cfg.Placement), out wx, out wy);
            return target.PosX != wx || target.PosY != wy;
        }

        private static string Signature(List<Monitor> all)
        {
            var sb = new StringBuilder();
            foreach (var d in all)
                sb.Append(d.Adapter).Append(':').Append(d.PosX).Append(',').Append(d.PosY)
                  .Append(',').Append(d.Width).Append('x').Append(d.Height)
                  .Append(',').Append(d.Orientation).Append(';');
            return sb.ToString();
        }

        /// <summary>
        /// Resolve the monitor to rotate, most specific rule first. The hardware id is
        /// the EDID vendor+product code (CND30FE), which identifies the *model*, not a
        /// particular unit - so any MP161E6T matches, whatever port or display number
        /// Windows happens to give it.
        /// </summary>
        private Monitor Target(out string how)
        {
            var all = Displays.Enumerate();

            if (!string.IsNullOrEmpty(_cfg.TargetHardwareId))
                foreach (var m in all)
                    if (string.Equals(m.HardwareId, _cfg.TargetHardwareId, StringComparison.OrdinalIgnoreCase))
                    { how = "hardware id = " + _cfg.TargetHardwareId; return m; }

            if (!string.IsNullOrEmpty(_cfg.TargetNameMatch))
                foreach (var m in all)
                    if (!m.IsPrimary && (m.Name ?? "").IndexOf(
                            _cfg.TargetNameMatch, StringComparison.OrdinalIgnoreCase) >= 0)
                    { how = "name contains \"" + _cfg.TargetNameMatch + "\""; return m; }

            if (_cfg.FallbackToExternal)
                foreach (var m in all)
                    if (!m.IsPrimary)
                    { how = "fallback: first external display"; return m; }

            how = "no match";
            return null;
        }

        // ------------------------------------------------------------ actions
        public void Toggle()
        {
            var m = Target();
            if (m == null)
            {
                Warn("\u627E\u4E0D\u5230\u76EE\u6A19\u87A2\u5E55");
                return;
            }
            int next = (m.Orientation == _cfg.OrientA) ? _cfg.OrientB : _cfg.OrientA;
            Apply(m, next);
        }

        private void Apply(Monitor m, int orientation)
        {
            // Learn where it is now, so a rotation keeps the side the user last chose.
            LearnLayout(m);

            // A mode change warps the pointer to the middle of the primary display, which
            // is jarring when you only clicked the tray icon. Put it back where it was.
            Native.POINT cursor;
            bool haveCursor = Native.GetCursorPos(out cursor);

            string err;
            _applying = true;
            try
            {
                err = Displays.ApplyLayout(m, Primary(), orientation,
                                           _cfg.Placement, _cfg.EffectiveAlign(_cfg.Placement));
            }
            finally { _applying = false; }

            if (haveCursor)
            {
                RestoreCursor(cursor);
                // Windows moves the pointer again as the desktop finishes reconfiguring,
                // so re-assert once the message queue has drained.
                try { _sync.BeginInvoke((Action)delegate { RestoreCursor(cursor); }); }
                catch { }
            }

            if (err != null) Warn(err);
            _lastState = null;
            _verifyUntil = DateTime.Now + VerifyWindow;
            _lastSignature = null;
            Refresh();
        }

        /// <summary>Put the pointer back, but only if that spot still lands on a display.</summary>
        private static void RestoreCursor(Native.POINT p)
        {
            foreach (var d in Displays.Enumerate())
                if (p.X >= d.PosX && p.X < d.PosX + d.Width &&
                    p.Y >= d.PosY && p.Y < d.PosY + d.Height)
                {
                    Native.SetCursorPos(p.X, p.Y);
                    return;
                }
        }

        private void ApplyQuadrant(int q)
        {
            var m = Target();
            if (m == null) { Warn("\u627E\u4E0D\u5230\u76EE\u6A19\u87A2\u5E55"); return; }
            Apply(m, q);
        }

        private void Warn(string msg)
        {
            _tray.Balloon("\u87A2\u5E55\u65B9\u5411", msg, true);
        }

        // ----------------------------------------------------------------- UI
        private void Refresh()
        {
            if (_applying) return;

            var all = Displays.Enumerate();
            string sig = Signature(all);
            if (sig != _lastSignature)
            {
                _lastSignature = sig;
                _stableSince = DateTime.Now;    // configuration is still moving
            }
            bool settled = (DateTime.Now - _stableSince) >= SettleTime;

            var m = Target();
            bool present = m != null;

            if (present && !_wasPresent)
            {
                _enforcePending = true;                       // reconnect edge
                _verifyUntil = DateTime.Now + VerifyWindow;
            }
            _wasPresent = present;

            if (present && settled)
            {
                bool inVerify = DateTime.Now <= _verifyUntil;

                if (_enforcePending || (inVerify && LayoutDrifted(m)))
                {
                    _applying = true;
                    try { EnforceLayout(m); }
                    finally { _applying = false; }
                    _enforcePending = false;
                    _lastSignature = null;      // geometry just changed; re-settle
                    m = Target();
                    present = m != null;
                    _wasPresent = present;
                }
                else if (!inVerify)
                {
                    // Outside the verify window the user is in charge: adopt what they set.
                    LearnLayout(m);
                }
            }

            if (!present && _cfg.HideWhenDisconnected)
            {
                if (_tray.Shown) { _tray.Hide(); _lastState = null; }
                return;
            }

            string state = (present ? m.Orientation.ToString() + "|" + m.Name : "none")
                         + "|" + (LightTaskbar() ? "L" : "D");
            if (state == _lastState && _tray.Shown) return;
            _lastState = state;

            bool portrait = present && m.IsPortrait;
            IntPtr hIcon = RenderIcon(portrait, present);

            string name = present ? m.Name : "\u672A\u9023\u63A5";
            string tip = name + "\n" + (present ? m.OrientationText : "\u627E\u4E0D\u5230\u87A2\u5E55")
                       + "\n\u5DE6\u9375\u5207\u63DB / \u53F3\u9375\u9078\u55AE";

            if (_tray.Shown) _tray.Update(hIcon, tip);
            else _tray.Show(hIcon, tip);
        }

        private static bool LightTaskbar()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(
                           @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("SystemUsesLightTheme");
                        if (v is int) return ((int)v) != 0;
                    }
                }
            }
            catch { }
            return false;
        }

        private static IntPtr RenderIcon(bool portrait, bool available)
        {
            const int S = 32;
            using (var bmp = new Bitmap(S, S))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    Color fg = LightTaskbar() ? Color.FromArgb(28, 28, 28) : Color.FromArgb(243, 243, 243);
                    if (!available) fg = Color.FromArgb(110, fg);

                    int w = portrait ? 16 : 25;
                    int h = portrait ? 25 : 16;
                    var body = new Rectangle((S - w) / 2, (S - h) / 2 - 2, w, h);

                    using (var pen = new Pen(fg, 2.6f))
                    {
                        using (var path = Rounded(body, 3)) g.DrawPath(pen, path);

                        int cx = S / 2;
                        int y0 = body.Bottom + 2;
                        g.DrawLine(pen, cx, y0, cx, y0 + 2);
                        g.DrawLine(pen, cx - 5, y0 + 3, cx + 5, y0 + 3);
                    }
                }
                return bmp.GetHicon();
            }
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private void ShowMenu(int x, int y)
        {
            BuildMenu();
            Native.SetForegroundWindow(_win.Handle);
            if (x == 0 && y == 0) { var p = Cursor.Position; x = p.X; y = p.Y; }
            _menu.Show(new Point(x, y));
        }

        private void BuildMenu()
        {
            _menu.Items.Clear();

            var all = Displays.Enumerate();
            var m = Target();

            var header = new ToolStripMenuItem(
                m != null ? m.Name + "  \u2014  " + m.OrientationText : "\u627E\u4E0D\u5230\u76EE\u6A19\u87A2\u5E55");
            header.Enabled = false;
            _menu.Items.Add(header);
            _menu.Items.Add(new ToolStripSeparator());

            for (int q = 0; q < 4; q++)
            {
                int qq = q;
                var it = new ToolStripMenuItem(Names.Orient(q), null, delegate { ApplyQuadrant(qq); });
                it.Checked = (m != null && m.Orientation == q);
                it.Enabled = (m != null);
                _menu.Items.Add(it);
            }

            _menu.Items.Add(new ToolStripSeparator());

            // 左鍵切換組合
            var pair = new ToolStripMenuItem("\u5DE6\u9375\u5207\u63DB\u7D44\u5408\uFF1A"
                + Names.Orient(_cfg.OrientA) + " \u2194 " + Names.Orient(_cfg.OrientB));
            for (int q = 0; q < 4; q++)
            {
                int qq = q;
                var a = new ToolStripMenuItem("A = " + Names.Orient(q), null,
                    delegate { _cfg.OrientA = qq; _cfg.Save(); });
                a.Checked = _cfg.OrientA == q;
                pair.DropDownItems.Add(a);
            }
            pair.DropDownItems.Add(new ToolStripSeparator());
            for (int q = 0; q < 4; q++)
            {
                int qq = q;
                var b = new ToolStripMenuItem("B = " + Names.Orient(q), null,
                    delegate { _cfg.OrientB = qq; _cfg.Save(); });
                b.Checked = _cfg.OrientB == q;
                pair.DropDownItems.Add(b);
            }
            _menu.Items.Add(pair);

            // 目標螢幕
            var pick = new ToolStripMenuItem("\u76EE\u6A19\u87A2\u5E55");
            foreach (var mon in all)
            {
                Monitor cap = mon;
                string label = cap.Name + "  (" + cap.Adapter
                    + (cap.IsPrimary ? ", \u4E3B\u8981" : "") + ")";
                var it = new ToolStripMenuItem(label, null, delegate
                {
                    _cfg.TargetHardwareId = cap.HardwareId; _cfg.Save(); _lastState = null; Refresh();
                });
                it.Checked = (m != null && cap.HardwareId == m.HardwareId);
                pick.DropDownItems.Add(it);
            }
            if (all.Count == 0)
            {
                var none = new ToolStripMenuItem("(\u6C92\u6709\u53EF\u7528\u87A2\u5E55)");
                none.Enabled = false;
                pick.DropDownItems.Add(none);
            }
            _menu.Items.Add(pick);

            // 斷線時隱藏圖示
            string[] sides = { "\u5DE6\u908A", "\u53F3\u908A", "\u6B63\u4E0A\u65B9", "\u6B63\u4E0B\u65B9" };
            var place = new ToolStripMenuItem("\u5916\u63A5\u87A2\u5E55\u4F4D\u7F6E\uFF1A" + sides[(int)_cfg.Placement]);
            for (int i = 0; i < 4; i++)
            {
                Placement pp = (Placement)i;
                var pi = new ToolStripMenuItem(sides[i], null, delegate
                {
                    _cfg.Placement = pp; _cfg.Save();
                    var t = Target(); if (t != null) EnforceLayout(t);
                    _lastState = null; Refresh();
                });
                pi.Checked = _cfg.Placement == pp;
                place.DropDownItems.Add(pi);
            }
            place.DropDownItems.Add(new ToolStripSeparator());

            bool horizontal = _cfg.Placement == Placement.Left || _cfg.Placement == Placement.Right;
            string[] aligns = horizontal
                ? new string[] { "\u4E0A\u7DE3\u5207\u9F4A", "\u5782\u76F4\u7F6E\u4E2D", "\u4E0B\u7DE3\u5207\u9F4A" }
                : new string[] { "\u5DE6\u7DE3\u5207\u9F4A", "\u6C34\u5E73\u7F6E\u4E2D", "\u53F3\u7DE3\u5207\u9F4A" };

            var autoAlign = new ToolStripMenuItem(
                "\u81EA\u52D5\uFF08\u5DE6\u53F3\u2192\u4E0B\u7DE3\u5207\u9F4A\uFF0C\u4E0A\u4E0B\u2192\u6C34\u5E73\u7F6E\u4E2D\uFF09", null, delegate
                {
                    _cfg.AlignMode = -1; _cfg.Save();
                    var t = Target(); if (t != null) EnforceLayout(t);
                    _lastState = null; Refresh();
                });
            autoAlign.Checked = _cfg.AlignMode < 0;
            place.DropDownItems.Add(autoAlign);

            for (int i = 0; i < 3; i++)
            {
                int ai2 = i;
                var ai = new ToolStripMenuItem(aligns[i], null, delegate
                {
                    _cfg.AlignMode = ai2; _cfg.Save();
                    var t = Target(); if (t != null) EnforceLayout(t);
                    _lastState = null; Refresh();
                });
                ai.Checked = _cfg.AlignMode == ai2;
                place.DropDownItems.Add(ai);
            }
            place.DropDownItems.Add(new ToolStripSeparator());

            var keep = new ToolStripMenuItem("\u8F49\u5411/\u91CD\u65B0\u9023\u63A5\u6642\u56FA\u5B9A\u4F4D\u7F6E", null,
                delegate { _cfg.KeepLayout = !_cfg.KeepLayout; _cfg.Save(); });
            keep.Checked = _cfg.KeepLayout;
            place.DropDownItems.Add(keep);

            var learn = new ToolStripMenuItem("\u8A18\u4F4F\u6211\u5728\u8A2D\u5B9A\u4E2D\u62D6\u66F3\u7684\u4F4D\u7F6E", null,
                delegate { _cfg.LearnPlacement = !_cfg.LearnPlacement; _cfg.Save(); });
            learn.Checked = _cfg.LearnPlacement;
            place.DropDownItems.Add(learn);

            _menu.Items.Add(place);

            var hide = new ToolStripMenuItem("\u87A2\u5E55\u65B7\u7DDA\u6642\u96B1\u85CF\u5716\u793A", null, delegate
            {
                _cfg.HideWhenDisconnected = !_cfg.HideWhenDisconnected;
                _cfg.Save(); _lastState = null; Refresh();
            });
            hide.Checked = _cfg.HideWhenDisconnected;
            _menu.Items.Add(hide);

            // 熱鍵
            var hk = new ToolStripMenuItem("\u71B1\u9375 Ctrl+Alt+R", null, delegate
            {
                _cfg.Hotkey = !_cfg.Hotkey;
                _cfg.Save();
                if (_cfg.Hotkey)
                {
                    if (!_win.RegisterHotkey())
                        Warn("\u71B1\u9375\u8A3B\u518A\u5931\u6557\uFF0C\u53EF\u80FD\u5DF2\u88AB\u5360\u7528");
                }
                else _win.UnregisterHotkey();
            });
            hk.Checked = _cfg.Hotkey;
            _menu.Items.Add(hk);

            // 開機自動啟動
            var auto = new ToolStripMenuItem("\u958B\u6A5F\u81EA\u52D5\u555F\u52D5", null,
                delegate { SetAutostart(!IsAutostart()); });
            auto.Checked = IsAutostart();
            _menu.Items.Add(auto);

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem("\u7D50\u675F", null, delegate { ExitApp(); }));
        }

        // ---------------------------------------------------------- autostart
        private static bool IsAutostart()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(RunValue) != null;
            }
            catch { return false; }
        }

        private static void SetAutostart(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(RunValue, false);
                }
            }
            catch { }
        }

        // ------------------------------------ named pipe: hook for BLE dongle
        private void StartPipeServer()
        {
            var t = new Thread(delegate ()
            {
                while (!_exiting)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                                   PipeTransmissionMode.Byte, PipeOptions.None))
                        {
                            server.WaitForConnection();
                            if (_exiting) return;
                            using (var r = new StreamReader(server, Encoding.UTF8))
                            {
                                string line;
                                while ((line = r.ReadLine()) != null)
                                {
                                    if (_exiting) return;
                                    string cmd = line.Trim().ToLowerInvariant();
                                    try { _sync.BeginInvoke((Action)delegate { HandleCommand(cmd); }); }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch
                    {
                        if (_exiting) return;
                        Thread.Sleep(500);
                    }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void HandleCommand(string cmd)
        {
            int q;
            if (cmd == "toggle") Toggle();
            else if (cmd == "landscape") ApplyQuadrant(_cfg.OrientA);
            else if (cmd == "portrait") ApplyQuadrant(_cfg.OrientB);
            else if (cmd == "show")
            {
                // escape hatch: force the icon back while the monitor is away
                _cfg.HideWhenDisconnected = false;
                _cfg.Save();
                _lastState = null;
                Refresh();
            }
            else if (cmd == "diag") WriteLog("on demand");
            else if (cmd == "layout")
            {
                var t = Target();
                if (t != null) { EnforceLayout(t); _lastState = null; Refresh(); }
                WriteLog("layout enforced");
            }
            else if (cmd == "quit" || cmd == "exit") ExitApp();
            else if (int.TryParse(cmd, out q) && q >= 0 && q <= 3) ApplyQuadrant(q);
        }

        // ----------------------------------------------------------- lifetime
        private void ExitApp()
        {
            _exiting = true;
            _watchdog.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _win.UnregisterHotkey();
            _tray.Dispose();
            _win.DestroyHandle();
            try
            {
                using (var c = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                    c.Connect(200);   // unblock WaitForConnection
            }
            catch { }
            ExitThread();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // CLI passthrough: MonitorRotateTray.exe toggle|landscape|portrait|show|quit|0..3
            if (args.Length > 0)
            {
                try
                {
                    using (var c = new NamedPipeClientStream(".", "MonitorRotateTray", PipeDirection.Out))
                    {
                        c.Connect(2000);
                        var w = new StreamWriter(c, new UTF8Encoding(false));
                        w.WriteLine(args[0]);
                        w.Flush();
                    }
                    return;
                }
                catch
                {
                    Environment.Exit(1);
                }
            }

            bool created;
            using (new Mutex(true, "Local\\MonitorRotateTray.Instance", out created))
            {
                if (!created) return;   // already running
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }
}
