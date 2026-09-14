using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
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
        public const int NIN_BALLOONTIMEOUT   = 0x0404;   // WM_USER + 4
        public const int NIN_BALLOONUSERCLICK = 0x0405;   // WM_USER + 5

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
        public static extern bool DestroyIcon(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT p);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        // --- HID enumeration: only used to tell whether an external touch screen is attached ---
        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
            public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid g);
        [DllImport("hid.dll")] public static extern bool HidD_GetPreparsedData(IntPtr h, out IntPtr p);
        [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr p);
        [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr p, out HIDP_CAPS c);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr enumerator, IntPtr hwnd, int flags);
        [DllImport("setupapi.dll")]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid g, int index, ref SP_DEVICE_INTERFACE_DATA data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int size, ref int required, IntPtr info);
        [DllImport("setupapi.dll")]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr h);

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
        public string DevicePath;   // \\?\DISPLAY#CND30FE#...#{guid} - what touch mappings refer to
        public bool IsPrimary;

        public string OrientationText { get { return L.Orient(Orientation); } }
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

    /// <summary>
    /// Interface strings. English by default; Traditional Chinese when the config asks
    /// for it, or when Windows itself is running in Chinese.
    /// </summary>
    internal static class L
    {
        private static bool _zh;

        /// <param name="mode">"en", "zh-TW", or anything else meaning "follow Windows".</param>
        public static void Use(string mode)
        {
            if (string.Equals(mode, "zh-TW", StringComparison.OrdinalIgnoreCase)) _zh = true;
            else if (string.Equals(mode, "en", StringComparison.OrdinalIgnoreCase)) _zh = false;
            else _zh = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                        .Equals("zh", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsChinese { get { return _zh; } }

        private static string S(string en, string zh) { return _zh ? zh : en; }

        public static string Orient(int q)
        {
            switch (q)
            {
                case 1:  return S("Portrait 90°",   "直向 90°");
                case 2:  return S("Landscape 180°", "橫向 180°");
                case 3:  return S("Portrait 270°",  "直向 270°");
                default: return S("Landscape 0°",   "橫向 0°");
            }
        }

        public static string Side(int i)
        {
            switch (i)
            {
                case 1:  return S("Right", "右邊");
                case 2:  return S("Directly above", "正上方");
                case 3:  return S("Directly below", "正下方");
                default: return S("Left", "左邊");
            }
        }

        public static string AlignName(bool horizontal, int i)
        {
            if (horizontal)
            {
                switch (i)
                {
                    case 1:  return S("Centre vertically", "垂直置中");
                    case 2:  return S("Bottom edges level", "下緣切齊");
                    default: return S("Top edges level", "上緣切齊");
                }
            }
            switch (i)
            {
                case 1:  return S("Centre horizontally", "水平置中");
                case 2:  return S("Right edges level", "右緣切齊");
                default: return S("Left edges level", "左緣切齊");
            }
        }

        public static string BalloonTitle   { get { return S("Monitor orientation", "螢幕方向"); } }
        public static string NoTarget       { get { return S("Target monitor not found", "找不到目標螢幕"); } }
        public static string NotConnected   { get { return S("Not connected", "未連接"); } }
        public static string NoMonitorFound { get { return S("Monitor not found", "找不到螢幕"); } }
        public static string TipHint        { get { return S("Left click to toggle / right click for menu", "左鍵切換 / 右鍵選單"); } }

        public static string BadOrientation { get { return S("Invalid orientation value", "方向值無效"); } }
        public static string ReadFailed     { get { return S("Could not read the current display settings", "讀取目前顯示設定失敗"); } }

        public static string ModeRejected(int rc)
        {
            return S("Display mode rejected (CDS_TEST rc=" + rc + ")",
                     "顯示模式不被接受 (CDS_TEST rc=" + rc + ")");
        }

        public static string StageFailed(int rc)
        {
            return S("Could not stage the change (rc=" + rc + ")",
                     "排程失敗 (rc=" + rc + ")");
        }

        public static string ApplyFailed(int rc)
        {
            return S("Could not apply the change (rc=" + rc + ")",
                     "套用失敗 (rc=" + rc + ")");
        }

        public static string TogglePair     { get { return S("Left click toggles: ", "左鍵切換組合："); } }
        public static string TargetMonitor  { get { return S("Target monitor", "目標螢幕"); } }
        public static string NoDisplays     { get { return S("(no displays available)", "(沒有可用螢幕)"); } }
        public static string PrimarySuffix  { get { return S("primary", "主要"); } }
        public static string PositionMenu   { get { return S("External monitor position: ", "外接螢幕位置："); } }
        public static string AlignAuto      { get { return S("Automatic (side by side → bottom edges, stacked → centred)", "自動（左右→下緣切齊，上下→水平置中）"); } }
        public static string KeepLayout     { get { return S("Keep position on rotate and reconnect", "轉向/重新連接時固定位置"); } }
        public static string LearnPlacement { get { return S("Adopt the position I set in Windows Settings", "記住我在設定中拖曳的位置"); } }
        public static string HideDisconnect { get { return S("Hide icon while the monitor is away", "螢幕斷線時隱藏圖示"); } }
        public static string Autostart      { get { return S("Start with Windows", "開機自動啟動"); } }
        public static string LanguageMenu   { get { return S("Language", "語言"); } }
        public static string LangFollow     { get { return S("Follow Windows", "跟隨系統"); } }
        public static string FixTouch       { get { return S("Fix touch mapping…", "修正觸控對應…"); } }
        public static string TouchNotMapped { get { return S("Touches on this monitor may land on another screen. Click here to fix.", "在這台螢幕上觸控，可能會跑到其他螢幕。點這裡修正。"); } }
        public static string TouchStale     { get { return S("This monitor is connected differently than before, so its touch mapping no longer applies. Click here to fix.", "這台螢幕的連接方式和之前不同，原本的觸控對應已失效。點這裡修正。"); } }
        public static string TouchFixed     { get { return S("Touch is now mapped to this monitor.", "觸控已對應到這台螢幕。"); } }
        public static string TouchUpdated   { get { return S("Touch mapping updated. Tap the monitor to check.", "觸控對應已更新，請點點看螢幕確認。"); } }
        public static string TouchUnchanged { get { return S("Touch mapping was not changed.", "觸控對應沒有變更。"); } }
        public static string TouchToolMissing { get { return S("Windows' touch setup tool (MultiDigiMon.exe) was not found.", "找不到 Windows 觸控設定工具 (MultiDigiMon.exe)。"); } }

        public static string TouchInstructions(string monitor)
        {
            return S("When the white prompt appears on " + monitor + ", tap it. On any other screen, press Enter.",
                     "白色提示出現在 " + monitor + " 上時用手指點它；出現在其他螢幕時按 Enter 跳過。");
        }

        public static string Exit           { get { return S("Exit", "結束"); } }
    }

    // ------------------------------------------------------------------ Touch
    internal enum TouchMapState { NoTouchScreen, NotMapped, Stale, Ok }

    /// <summary>
    /// Reads Windows' touch-digitizer-to-display association. Windows ties a touch panel to
    /// a monitor either through an explicit entry under Wisp\Pen\Digimon (written by the
    /// Tablet PC Settings setup, MultiDigiMon.exe) or, failing that, by matching USB container
    /// IDs. Portable monitors often fail the container match, and Windows then hands the touch
    /// panel to the built-in laptop screen. Nothing here writes: the key lives in HKLM, so
    /// changing it needs elevation and goes through Windows' own tool.
    /// </summary>
    internal static class Touch
    {
        private const string DigimonKey = @"SOFTWARE\Microsoft\Wisp\Pen\Digimon";
        private const ushort UsagePageDigitizer = 0x0D;
        private const ushort UsageTouchScreen = 0x04;

        public static string ToolPath
        {
            get
            {
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                // A 32-bit process on 64-bit Windows is redirected away from System32.
                if (!Environment.Is64BitProcess && Environment.Is64BitOperatingSystem)
                    return Path.Combine(win, @"Sysnative\MultiDigiMon.exe");
                return Path.Combine(win, @"System32\MultiDigiMon.exe");
            }
        }

        /// <summary>Interface paths of USB-attached HID touch screens. Built-in I2C panels are skipped.</summary>
        public static List<string> ExternalTouchScreens()
        {
            var found = new List<string>();
            Guid hid;
            Native.HidD_GetHidGuid(out hid);
            IntPtr set = Native.SetupDiGetClassDevs(ref hid, IntPtr.Zero, IntPtr.Zero, 0x12); // PRESENT | DEVICEINTERFACE
            if (set == IntPtr.Zero || set == new IntPtr(-1)) return found;
            try
            {
                for (int i = 0; ; i++)
                {
                    var did = new Native.SP_DEVICE_INTERFACE_DATA();
                    did.cbSize = Marshal.SizeOf(typeof(Native.SP_DEVICE_INTERFACE_DATA));
                    if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hid, i, ref did)) break;

                    int required = 0;
                    Native.SetupDiGetDeviceInterfaceDetail(set, ref did, IntPtr.Zero, 0, ref required, IntPtr.Zero);
                    if (required <= 0) continue;

                    string path = null;
                    IntPtr buf = Marshal.AllocHGlobal(required);
                    try
                    {
                        Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);   // sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA_W)
                        if (Native.SetupDiGetDeviceInterfaceDetail(set, ref did, buf, required, ref required, IntPtr.Zero))
                            path = Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4));
                    }
                    finally { Marshal.FreeHGlobal(buf); }

                    if (string.IsNullOrEmpty(path) || path.IndexOf("vid_", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    IntPtr h = Native.CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);  // caps need no access rights
                    if (h == new IntPtr(-1)) continue;
                    try
                    {
                        IntPtr pp;
                        if (!Native.HidD_GetPreparsedData(h, out pp)) continue;
                        try
                        {
                            Native.HIDP_CAPS caps;
                            if (Native.HidP_GetCaps(pp, out caps) == 0x110000              // HIDP_STATUS_SUCCESS
                                && caps.UsagePage == UsagePageDigitizer && caps.Usage == UsageTouchScreen)
                                found.Add(path);
                        }
                        finally { Native.HidD_FreePreparsedData(pp); }
                    }
                    finally { Native.CloseHandle(h); }
                }
            }
            finally { Native.SetupDiDestroyDeviceInfoList(set); }
            return found;
        }

        public static Dictionary<string, string> ReadMappings()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var k = hklm.OpenSubKey(DigimonKey))
                {
                    if (k == null) return map;
                    foreach (string n in k.GetValueNames())
                        map[n] = Convert.ToString(k.GetValue(n));
                }
            }
            catch { }
            return map;
        }

        /// <summary>The whole Digimon key flattened, to notice that the setup tool changed something.</summary>
        public static string Snapshot()
        {
            var sb = new StringBuilder();
            foreach (var kv in ReadMappings()) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
            return sb.ToString();
        }

        /// <summary>"display#cnd30fe#4&amp;38adc3c2&amp;2&amp;uid224795" out of any form a display path takes.</summary>
        private static string DisplayKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string lower = s.ToLowerInvariant();
            int start = lower.IndexOf("display#", StringComparison.Ordinal);
            if (start < 0) return "";
            int end = lower.IndexOf("#{", start, StringComparison.Ordinal);
            return end > start ? lower.Substring(start, end - start) : lower.Substring(start);
        }

        public static TouchMapState Evaluate(Monitor target, out string detail)
        {
            detail = "";
            if (target == null) { detail = "no target monitor"; return TouchMapState.NoTouchScreen; }

            var maps = ReadMappings();

            string want = DisplayKey(target.DevicePath);
            if (want.Length > 0)
                foreach (var kv in maps)
                    if (DisplayKey(kv.Value) == want) { detail = "mapped"; return TouchMapState.Ok; }

            // Same model, different instance: the monitor came back through another port or
            // dock, got a new UID, and the old entry no longer matches it.
            if (!string.IsNullOrEmpty(target.HardwareId))
            {
                string model = "display#" + target.HardwareId.ToLowerInvariant() + "#";
                foreach (var kv in maps)
                    if (DisplayKey(kv.Value).StartsWith(model, StringComparison.Ordinal))
                    { detail = "entry points at " + kv.Value; return TouchMapState.Stale; }
            }

            if (ExternalTouchScreens().Count == 0) { detail = "no external touch screen"; return TouchMapState.NoTouchScreen; }
            detail = maps.Count == 0 ? "no Digimon entries at all" : "no entry for this monitor";
            return TouchMapState.NotMapped;
        }
    }

    internal static class Displays
    {
        private static Dictionary<string, string> FriendlyNames(Dictionary<string, string> devicePaths)
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

                    if (!string.IsNullOrEmpty(tgt.monitorDevicePath) && !devicePaths.ContainsKey(src.viewGdiDeviceName))
                        devicePaths[src.viewGdiDeviceName] = tgt.monitorDevicePath;

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
            var devicePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var names = FriendlyNames(devicePaths);
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

                string devicePath;
                devicePaths.TryGetValue(dd.DeviceName, out devicePath);

                list.Add(new Monitor
                {
                    Adapter = dd.DeviceName,
                    MonitorId = devId,
                    DevicePath = devicePath ?? "",
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
            if (orientation < 0 || orientation > 3) return L.BadOrientation;

            var dm = new Native.DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(Native.DEVMODE));
            if (!Native.EnumDisplaySettings(target.Adapter, Native.ENUM_CURRENT_SETTINGS, ref dm))
                return L.ReadFailed;

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
                return L.ModeRejected(rc);

            // NORESET stages the change, then the NULL commit applies the whole desktop at
            // once. Without staging, a position change can be rejected as an overlap while
            // the old size is still in effect.
            rc = Native.ChangeDisplaySettingsEx(target.Adapter, ref dm, IntPtr.Zero,
                                                Native.CDS_UPDATEREGISTRY | Native.CDS_NORESET, IntPtr.Zero);
            if (rc != Native.DISP_CHANGE_SUCCESSFUL && rc != Native.DISP_CHANGE_RESTART)
                return L.StageFailed(rc);

            rc = Native.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            if (rc == Native.DISP_CHANGE_SUCCESSFUL || rc == Native.DISP_CHANGE_RESTART) return null;
            return L.ApplyFailed(rc);
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
        public bool HideWhenDisconnected = true;
        public string Language = "auto";   // "auto" | "en" | "zh-TW"

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
                    else if (k == "HideWhenDisconnected") c.HideWhenDisconnected = IsTrue(v);
                    else if (k == "Language") c.Language = v;
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
                sb.AppendLine("HideWhenDisconnected=" + (HideWhenDisconnected ? "1" : "0"));
                sb.AppendLine("Language=" + Language + "   ; auto | en | zh-TW");
                sb.AppendLine("Placement=" + (int)Placement + "   ; 0=left 1=right 2=above 3=below");
                sb.AppendLine("Align=" + AlignMode + "   ; -1=auto 0=start 1=centre 2=end");
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
        private static readonly int WM_TASKBARCREATED = Native.RegisterWindowMessage("TaskbarCreated");

        private readonly Action _onToggle;
        private readonly Action<int, int> _onContextMenu;
        private readonly Action _onTaskbarCreated;
        private readonly Func<bool> _isVersion4;
        private readonly Action _onBalloonClick;

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
                             Func<bool> isVersion4, Action onBalloonClick)
        {
            _onToggle = onToggle;
            _onContextMenu = onContextMenu;
            _onTaskbarCreated = onTaskbarCreated;
            _isVersion4 = isVersion4;
            _onBalloonClick = onBalloonClick;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
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
                    select ? " -> toggle" : (menu ? " -> menu" : (evt == Native.NIN_BALLOONUSERCLICK ? " -> balloon" : " -> ignored"))));

                if (select) _onToggle();
                else if (menu) _onContextMenu(x, y);
                else if (evt == Native.NIN_BALLOONUSERCLICK && _onBalloonClick != null) _onBalloonClick();
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
        private string _lastError;   // surfaced in the diagnostics log

        // touch-to-display mapping: read here, fixed through Windows' own tool
        private bool _touchCheckPending;    // look once the desktop settles after a (re)connect
        private bool _touchWarned;          // one bubble per connection, not one per tick
        private bool _touchFixRunning;
        private Action _balloonAction;      // what clicking the bubble currently on screen does

        public TrayApp()
        {
            _cfg = Config.Load();
            L.Use(_cfg.Language);

            _sync = new Control();
            IntPtr force = _sync.Handle;    // realise the UI-thread window
            GC.KeepAlive(force);

            _menu = new ContextMenuStrip();
            // _tray is assigned immediately below; the probe only runs once messages arrive.
            _win = new MessageWindow(Toggle, ShowMenu, OnTaskbarCreated,
                                     delegate { return _tray != null && _tray.Version4; }, OnBalloonClick);
            _tray = new TrayIcon(_win.Handle);

            if (string.IsNullOrEmpty(_cfg.TargetHardwareId))
            {
                var pick = PickDefaultTarget();
                if (pick != null) { _cfg.TargetHardwareId = pick.HardwareId; _cfg.Save(); }
            }


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
                sb.AppendLine("language    : " + (L.IsChinese ? "zh-TW" : "en") + "   (setting: " + _cfg.Language + ")");
                sb.AppendLine();
                sb.AppendLine("match rule  : " + how);
                sb.AppendLine("target      : " + (m != null
                    ? m.Name + "  (" + m.Adapter + ", " + m.OrientationText + ", " + m.Width + "x" + m.Height + ")"
                    : "NONE"));
                sb.AppendLine("toggle pair : " + L.Orient(_cfg.OrientA) + " <-> " + L.Orient(_cfg.OrientB));
                sb.AppendLine("last apply  : " + (_lastError ?? "(not attempted)"));
                string touchDetail;
                var touchState = Touch.Evaluate(m, out touchDetail);
                var touchScreens = Touch.ExternalTouchScreens();
                sb.AppendLine("touch map   : " + touchState + "   (" + touchDetail + ")");
                sb.AppendLine("touch screen: " + (touchScreens.Count == 0 ? "(none detected)" : string.Join(" | ", touchScreens.ToArray())));
                sb.AppendLine("monitor path: " + (m != null ? m.DevicePath : "-"));
                var digimon = Touch.ReadMappings();
                if (digimon.Count == 0) sb.AppendLine("digimon     : (no entries)");
                foreach (var kv in digimon) sb.AppendLine("digimon     : " + kv.Key + "  ->  " + kv.Value);
                sb.AppendLine();
                string[] sideNames = { "left", "right", "above", "below" };
                string[] alignNames = { "start", "centre", "end" };
                Align eff = _cfg.EffectiveAlign(_cfg.Placement);
                sb.AppendLine("placement   : " + sideNames[(int)_cfg.Placement]
                    + " / " + alignNames[(int)eff]
                    + (_cfg.AlignMode < 0 ? " (auto)" : " (explicit)")
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
                Warn(L.NoTarget);
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

            _lastError = (err ?? "ok") + "   (" + m.Orientation + " -> " + orientation + " at "
                       + DateTime.Now.ToString("HH:mm:ss") + ")";
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
            if (m == null) { Warn(L.NoTarget); return; }
            Apply(m, q);
        }

        private void Warn(string msg)
        {
            _balloonAction = null;
            _tray.Balloon(L.BalloonTitle, msg, true);
        }

        private void Info(string msg)
        {
            _balloonAction = null;
            _tray.Balloon(L.BalloonTitle, msg, false);
        }

        private void OnBalloonClick()
        {
            var action = _balloonAction;
            _balloonAction = null;
            if (action != null) action();
        }

        // ------------------------------------------------------------ touch
        /// <summary>
        /// Once the desktop has settled after a (re)connect, check where this monitor's touches
        /// would go and offer a one-click fix when they would land on another screen.
        /// </summary>
        private void CheckTouch(Monitor target)
        {
            if (_touchWarned || _touchFixRunning) return;

            string detail;
            var state = Touch.Evaluate(target, out detail);
            if (state != TouchMapState.NotMapped && state != TouchMapState.Stale) return;

            _touchWarned = true;
            _tray.Balloon(L.BalloonTitle, state == TouchMapState.Stale ? L.TouchStale : L.TouchNotMapped, true);
            _balloonAction = LaunchTouchFix;    // after Balloon: clicking the bubble runs the fix
        }

        private void LaunchTouchFix()
        {
            if (_touchFixRunning) return;

            string tool = Touch.ToolPath;
            if (!File.Exists(tool)) { Warn(L.TouchToolMissing); return; }

            var target = Target();
            string before = Touch.Snapshot();
            try
            {
                // The tool's manifest requires elevation, so ShellExecute raises the UAC prompt.
                var psi = new System.Diagnostics.ProcessStartInfo(tool, "-touch");
                psi.UseShellExecute = true;
                var p = System.Diagnostics.Process.Start(psi);
                if (p == null) return;

                _touchFixRunning = true;
                Info(L.TouchInstructions(target != null ? target.Name : "?"));

                p.EnableRaisingEvents = true;
                p.Exited += delegate
                {
                    try { _sync.BeginInvoke((Action)delegate { OnTouchFixDone(before); }); } catch { }
                };
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _touchFixRunning = false;
                if (ex.NativeErrorCode != 1223) Warn(ex.Message);   // 1223 = UAC declined
            }
            catch (Exception ex)
            {
                _touchFixRunning = false;
                Warn(ex.Message);
            }
        }

        private void OnTouchFixDone(string before)
        {
            _touchFixRunning = false;

            string detail;
            var state = Touch.Evaluate(Target(), out detail);
            bool changed = Touch.Snapshot() != before;

            if (state == TouchMapState.Ok) Info(L.TouchFixed);
            else if (changed) Info(L.TouchUpdated);
            else Warn(L.TouchUnchanged);

            WriteLog("touch setup finished");
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
                _touchCheckPending = true;
                _touchWarned = false;
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

                if (_touchCheckPending && present && _lastSignature != null)
                {
                    _touchCheckPending = false;
                    CheckTouch(m);
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

            string name = present ? m.Name : L.NotConnected;
            string tip = name + "\n" + (present ? m.OrientationText : L.NoMonitorFound)
                       + "\n" + L.TipHint;

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
                m != null ? m.Name + "  \u2014  " + m.OrientationText : L.NoTarget);
            header.Enabled = false;
            _menu.Items.Add(header);
            _menu.Items.Add(new ToolStripSeparator());

            for (int q = 0; q < 4; q++)
            {
                int qq = q;
                var it = new ToolStripMenuItem(L.Orient(q), null, delegate { ApplyQuadrant(qq); });
                it.Checked = (m != null && m.Orientation == q);
                it.Enabled = (m != null);
                _menu.Items.Add(it);
            }

            _menu.Items.Add(new ToolStripSeparator());

            // the pair the left click flips between
            var pair = new ToolStripMenuItem(L.TogglePair
                + L.Orient(_cfg.OrientA) + " \u2194 " + L.Orient(_cfg.OrientB));
            for (int q = 0; q < 4; q++)
            {
                int qq = q;
                var a = new ToolStripMenuItem("A = " + L.Orient(q), null,
                    delegate { _cfg.OrientA = qq; _cfg.Save(); });
                a.Checked = _cfg.OrientA == q;
                pair.DropDownItems.Add(a);
            }
            pair.DropDownItems.Add(new ToolStripSeparator());
            for (int q = 0; q < 4; q++)
            {
                int qq = q;
                var b = new ToolStripMenuItem("B = " + L.Orient(q), null,
                    delegate { _cfg.OrientB = qq; _cfg.Save(); });
                b.Checked = _cfg.OrientB == q;
                pair.DropDownItems.Add(b);
            }
            _menu.Items.Add(pair);

            // which monitor to act on
            var pick = new ToolStripMenuItem(L.TargetMonitor);
            foreach (var mon in all)
            {
                Monitor cap = mon;
                string label = cap.Name + "  (" + cap.Adapter
                    + (cap.IsPrimary ? ", " + L.PrimarySuffix : "") + ")";
                var it = new ToolStripMenuItem(label, null, delegate
                {
                    _cfg.TargetHardwareId = cap.HardwareId; _cfg.Save(); _lastState = null; Refresh();
                });
                it.Checked = (m != null && cap.HardwareId == m.HardwareId);
                pick.DropDownItems.Add(it);
            }
            if (all.Count == 0)
            {
                var none = new ToolStripMenuItem(L.NoDisplays);
                none.Enabled = false;
                pick.DropDownItems.Add(none);
            }
            _menu.Items.Add(pick);

            // position and alignment
            var place = new ToolStripMenuItem(L.PositionMenu + L.Side((int)_cfg.Placement));
            for (int i = 0; i < 4; i++)
            {
                Placement pp = (Placement)i;
                var pi = new ToolStripMenuItem(L.Side(i), null, delegate
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

            var autoAlign = new ToolStripMenuItem(L.AlignAuto, null, delegate
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
                var ai = new ToolStripMenuItem(L.AlignName(horizontal, i), null, delegate
                {
                    _cfg.AlignMode = ai2; _cfg.Save();
                    var t = Target(); if (t != null) EnforceLayout(t);
                    _lastState = null; Refresh();
                });
                ai.Checked = _cfg.AlignMode == ai2;
                place.DropDownItems.Add(ai);
            }
            place.DropDownItems.Add(new ToolStripSeparator());

            var keep = new ToolStripMenuItem(L.KeepLayout, null,
                delegate { _cfg.KeepLayout = !_cfg.KeepLayout; _cfg.Save(); });
            keep.Checked = _cfg.KeepLayout;
            place.DropDownItems.Add(keep);

            var learn = new ToolStripMenuItem(L.LearnPlacement, null,
                delegate { _cfg.LearnPlacement = !_cfg.LearnPlacement; _cfg.Save(); });
            learn.Checked = _cfg.LearnPlacement;
            place.DropDownItems.Add(learn);

            _menu.Items.Add(place);

            var hide = new ToolStripMenuItem(L.HideDisconnect, null, delegate
            {
                _cfg.HideWhenDisconnected = !_cfg.HideWhenDisconnected;
                _cfg.Save(); _lastState = null; Refresh();
            });
            hide.Checked = _cfg.HideWhenDisconnected;
            _menu.Items.Add(hide);

            // Only offered when there is a touch screen to map, or a mapping to repair.
            string touchDetail;
            var touchState = Touch.Evaluate(m, out touchDetail);
            if (touchState != TouchMapState.NoTouchScreen)
            {
                string mark = touchState == TouchMapState.Ok ? "  \u2713" : "  \u26A0";
                var fixTouch = new ToolStripMenuItem(L.FixTouch + mark, null, delegate { LaunchTouchFix(); });
                fixTouch.Enabled = !_touchFixRunning;
                _menu.Items.Add(fixTouch);
            }

            // language
            var lang = new ToolStripMenuItem(L.LanguageMenu);
            string[] langCodes = { "auto", "en", "zh-TW" };
            string[] langNames = { L.LangFollow, "English", "繁體中文" };
            for (int i = 0; i < langCodes.Length; i++)
            {
                string code = langCodes[i];
                var li = new ToolStripMenuItem(langNames[i], null, delegate
                {
                    _cfg.Language = code; _cfg.Save();
                    L.Use(code);
                    _lastState = null; Refresh();
                });
                li.Checked = string.Equals(_cfg.Language, code, StringComparison.OrdinalIgnoreCase);
                lang.DropDownItems.Add(li);
            }
            _menu.Items.Add(lang);

            var auto = new ToolStripMenuItem(L.Autostart, null,
                delegate { SetAutostart(!IsAutostart()); });
            auto.Checked = IsAutostart();
            _menu.Items.Add(auto);

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem(L.Exit, null, delegate { ExitApp(); }));
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
            else if (cmd == "touch") LaunchTouchFix();
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
