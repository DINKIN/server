﻿#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AgentInterface.Api.Models;
using AgentInterface.Api.ScreenShare;

#endregion

namespace AgentInterface.Api.Win32
{
    public class Display
    {
       
       
        [Flags]
        public enum ChangeDisplaySettingsFlags : uint
        {
            CDS_NONE = 0,
            CDS_UPDATEREGISTRY = 0x00000001,
            CDS_TEST = 0x00000002,
            CDS_FULLSCREEN = 0x00000004,
            CDS_GLOBAL = 0x00000008,
            CDS_SET_PRIMARY = 0x00000010,
            CDS_VIDEOPARAMETERS = 0x00000020,
            CDS_ENABLE_UNSAFE_MODES = 0x00000100,
            CDS_DISABLE_UNSAFE_MODES = 0x00000200,
            CDS_RESET = 0x40000000,
            CDS_RESET_EX = 0x20000000,
            CDS_NORESET = 0x10000000
        }

        [Flags]
        public enum DisplayDeviceStateFlags
        {
            /// <summary>The device is part of the desktop.</summary>
            AttachedToDesktop = 0x1,
            MultiDriver = 0x2,

            /// <summary>The device is part of the desktop.</summary>
            PrimaryDevice = 0x4,

            /// <summary>Represents a pseudo device used to mirror application drawing for remoting or other purposes.</summary>
            MirroringDriver = 0x8,

            /// <summary>The device is VGA compatible.</summary>
            VgaCompatible = 0x10,

            /// <summary>The device is removable; it cannot be the primary display.</summary>
            Removable = 0x20,

            /// <summary>The device has more display modes than its output devices support.</summary>
            ModesPruned = 0x8000000,
            Remote = 0x4000000,
            Disconnect = 0x2000000
        }

        public const int DMDO_DEFAULT = 0;
        public const int DMDO_90 = 1;
        public const int DMDO_180 = 2;
        public const int DMDO_270 = 3;

        public const int ErrorSuccess = 0;

        public const int ENUM_CURRENT_SETTINGS = -1;
        public const int CDS_UPDATEREGISTRY = 0x01;
        public const int CDS_TEST = 0x02;


        private const int ENUM_REGISTRY_SETTINGS = -2;

        public static int EnumCurrentSettings { get; } = -1;

        public static int EnumRegistrySettings { get; } = -2;

        private static string MonitorFriendlyName(Luid adapterId, uint targetId)
        {
            var deviceName = new DisplayconfigTargetDeviceName
            {
                header =
                {
                    size = (uint) Marshal.SizeOf(typeof(DisplayconfigTargetDeviceName)),
                    adapterId = adapterId,
                    id = targetId,
                    type = DisplayconfigDeviceInfoType.DisplayconfigDeviceInfoGetTargetName
                }
            };
            var error = DisplayConfigGetDeviceInfo(ref deviceName);
            if (error != ErrorSuccess)
                throw new Win32Exception(error);
            return deviceName.monitorFriendlyDeviceName;
        }

        private static IEnumerable<string> GetAllMonitorsFriendlyNames()
        {
            uint pathCount, modeCount;
            var error = GetDisplayConfigBufferSizes(QueryDeviceConfigFlags.QdcOnlyActivePaths, out pathCount,
                out modeCount);
            if (error != ErrorSuccess)
                throw new Win32Exception(error);

            var displayPaths = new DisplayconfigPathInfo[pathCount];
            var displayModes = new DisplayconfigModeInfo[modeCount];
            error = QueryDisplayConfig(QueryDeviceConfigFlags.QdcOnlyActivePaths,
                ref pathCount, displayPaths, ref modeCount, displayModes, IntPtr.Zero);
            if (error != ErrorSuccess)
                throw new Win32Exception(error);

            for (var i = 0; i < modeCount; i++)
                if (displayModes[i].infoType == DisplayconfigModeInfoType.DisplayconfigModeInfoTypeTarget)
                    yield return MonitorFriendlyName(displayModes[i].adapterId, displayModes[i].id);
        }

        public static List<string> DeviceFriendlyName()
        {
            return GetAllMonitorsFriendlyNames().ToList();
        }

        [DllImport("user32.dll")]
        public static extern int ChangeDisplaySettings(
            ref Devmode devMode, int flags);

        [DllImport("user32.dll")]
        public static extern DISP_CHANGE ChangeDisplaySettingsEx(string lpszDeviceName, ref Devmode lpDevMode,
            IntPtr hwnd, ChangeDisplaySettingsFlags dwflags, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern DISP_CHANGE ChangeDisplaySettingsEx(string lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd,
            ChangeDisplaySettingsFlags dwflags, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(
            string deviceName, int modeNum, ref Devmode devMode);

        private static List<DisplayInformation> UpdateDisplays()
        {
            var monitors = new List<DisplayInformation>();
            var d = new DisplayDevice();
            d.cb = Marshal.SizeOf(d);
            try
            {
                for (uint id = 0; EnumDisplayDevices(null, id, ref d, 0); id++)
                {
                    if (d.StateFlags.HasFlag(DisplayDeviceStateFlags.AttachedToDesktop))
                    {
                        var device = d.DeviceName;

                        var vDevMode = new Devmode();
                        var i = 0;
                        var supportedResolutions = new Dictionary<string, List<ResolutionInformation>>();
                        while (EnumDisplaySettings(device, i, ref vDevMode))
                        {
                            var width = vDevMode.dmPelsWidth;
                            var height = vDevMode.dmPelsHeight;
                            var bpp = vDevMode.dmBitsPerPel;
                            var orientation = vDevMode.dmDisplayOrientation.ToString();
                            var freq = vDevMode.dmDisplayFrequency;
                            var resolutionKey = $"{width}x{height}";
                            var resolution = new ResolutionInformation
                            {
                                BitsPerPixel = bpp,
                                Frequency = freq,
                                Height = height,
                                Width = width,
                                Orientation = orientation
                            };
                            if (supportedResolutions.ContainsKey(resolutionKey))
                            {
                                supportedResolutions[resolutionKey].Add(resolution);
                            }
                            else
                            {
                                supportedResolutions.Add(resolutionKey, new List<ResolutionInformation>());
                                supportedResolutions[resolutionKey].Add(resolution);
                            }
                            i++;
                        }
                        var cDevMode = new Devmode();
                        EnumDisplaySettings(device, ENUM_CURRENT_SETTINGS, ref cDevMode);

                        var currentResolution = new ResolutionInformation
                        {
                            BitsPerPixel = cDevMode.dmBitsPerPel,
                            Frequency = cDevMode.dmDisplayFrequency,
                            Height = cDevMode.dmPelsHeight,
                            Width = cDevMode.dmPelsWidth,
                            Orientation = cDevMode.dmDisplayOrientation.ToString(),
                            X = cDevMode.dmPositionX,
                            Y = cDevMode.dmPositionY
                        };
                        var monitor = new DisplayInformation
                        {
                            Primary = d.StateFlags.HasFlag(DisplayDeviceStateFlags.PrimaryDevice),
                            Attached = d.StateFlags.HasFlag(DisplayDeviceStateFlags.AttachedToDesktop),
                            Removable = d.StateFlags.HasFlag(DisplayDeviceStateFlags.Removable),
                            VgaCompatible = d.StateFlags.HasFlag(DisplayDeviceStateFlags.VgaCompatible),
                            MirroringDriver = d.StateFlags.HasFlag(DisplayDeviceStateFlags.MirroringDriver),
                            MultiDriver = d.StateFlags.HasFlag(DisplayDeviceStateFlags.MultiDriver),
                            ModesPruned = d.StateFlags.HasFlag(DisplayDeviceStateFlags.ModesPruned),
                            Remote = d.StateFlags.HasFlag(DisplayDeviceStateFlags.Remote),
                            Disconnect = d.StateFlags.HasFlag(DisplayDeviceStateFlags.Disconnect),
                            FriendlyName = $"{GetAllMonitorsFriendlyNames().ElementAt((int)id)} on {d.DeviceString}",
                            SupportedResolutions = supportedResolutions,
                            CurrentResolution = currentResolution,
                            DeviceName = device
                        };
                        monitors.Add(monitor);
                        d.cb = Marshal.SizeOf(d);
                        EnumDisplayDevices(d.DeviceName, 0, ref d, 0);
                    }
                    d.cb = Marshal.SizeOf(d);
                }
                return monitors;
            }
            catch
            {

            }
            string errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            Console.WriteLine(errorMessage);
            return monitors;
        }

        public static List<DisplayInformation> DisplayInformation()
        { 
            return UpdateDisplays();
        }

        public static string SetPrimary(string deviceName)
        {
            var id = int.Parse(Regex.Match(deviceName, @"\d+").Value) - 1;
            var originalMode = new Devmode();
            originalMode.dmSize = (short) Marshal.SizeOf(originalMode);
            EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref originalMode);
            var offsetx = originalMode.dmPositionX;
            var offsety = originalMode.dmPositionY;
            originalMode.dmPositionX = 0;
            originalMode.dmPositionY = 0;

            ChangeDisplaySettingsEx(deviceName, ref originalMode, (IntPtr) null,
                ChangeDisplaySettingsFlags.CDS_SET_PRIMARY | ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY |
                ChangeDisplaySettingsFlags.CDS_NORESET, IntPtr.Zero);
            var device = new DisplayDevice();
            device.cb = Marshal.SizeOf(device);

            // Update remaining devices
            for (uint otherid = 0; EnumDisplayDevices(null, otherid, ref device, 0); otherid++)
            {
                if (device.StateFlags.HasFlag(DisplayDeviceStateFlags.AttachedToDesktop) && otherid != id)
                {
                    device.cb = Marshal.SizeOf(device);
                    var otherDeviceMode = new Devmode();

                    EnumDisplaySettings(device.DeviceName, -1, ref otherDeviceMode);

                    otherDeviceMode.dmPositionX -= offsetx;
                    otherDeviceMode.dmPositionY -= offsety;

                    ChangeDisplaySettingsEx(
                        device.DeviceName,
                        ref otherDeviceMode,
                        (IntPtr) null,
                        ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY | ChangeDisplaySettingsFlags.CDS_NORESET,
                        IntPtr.Zero);
                }

                device.cb = Marshal.SizeOf(device);
            }

            // Apply settings
            return
                GetMessageForCode(ChangeDisplaySettingsEx(null, IntPtr.Zero, (IntPtr) null,
                    ChangeDisplaySettingsFlags.CDS_NONE, (IntPtr) null));
        }
        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        public static string Rotate(int angle, int width, int height, string deviceName)
        {
            var originalMode = new Devmode();
            originalMode.dmSize = (short) Marshal.SizeOf(originalMode);
            EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref originalMode);

            // swap height and width
            var temp = originalMode.dmPelsHeight;
            originalMode.dmPelsHeight = originalMode.dmPelsWidth;
            originalMode.dmPelsWidth = temp;

            originalMode.dmPelsWidth = width;
            originalMode.dmPelsHeight = height;
            switch (angle)
            {
                case 0:
                    originalMode.dmDisplayOrientation = ScreenOrientation.Angle0;
                    break;
                case 90:
                    originalMode.dmDisplayOrientation = ScreenOrientation.Angle90;
                    break;
                case 180:
                    originalMode.dmDisplayOrientation = ScreenOrientation.Angle180;
                    break;
                case 270:
                    originalMode.dmDisplayOrientation = ScreenOrientation.Angle270;
                    break;
            }
            return GetMessageForCode(ChangeDisplaySettingsEx(deviceName, ref originalMode, IntPtr.Zero,
                ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY, IntPtr.Zero));
        }

        public static string ChangeResolution(string deviceName, int width, int height, int bbp, int freq)
        {
            var originalMode = new Devmode();
            originalMode.dmSize = (short) Marshal.SizeOf(originalMode);
            EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref originalMode);
            var newMode = originalMode;
            newMode.dmDeviceName = deviceName;
            newMode.dmPelsWidth = width;
            newMode.dmPelsHeight = height;
            newMode.dmBitsPerPel = bbp;
            newMode.dmDisplayFrequency = freq;
            return GetMessageForCode(ChangeDisplaySettingsEx(deviceName, ref newMode, IntPtr.Zero,
                ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY, IntPtr.Zero));
        }

        private static string GetMessageForCode(DISP_CHANGE code)
        {
            string message;
            switch (code)
            {
                case DISP_CHANGE.Successful:
                    message = "Resolution updated.";
                    break;
                case DISP_CHANGE.Restart:
                    message = "A restart is required for this resolution to take effect.";
                    break;
                case DISP_CHANGE.BadMode:
                    message = $"resolution is not valid.";
                    break;
                case DISP_CHANGE.BadDualView:
                    message = "The settings change was unsuccessful because system is DualView capable.";
                    break;
                case DISP_CHANGE.BadFlags:
                    message = "An invalid set of flags was passed in.";
                    break;
                case DISP_CHANGE.BadParam:
                    message =
                        "An invalid parameter was passed in. This can include an invalid flag or combination of flags.";
                    break;
                case DISP_CHANGE.Failed:
                    message = "Resolution failed to update.";
                    break;
                case DISP_CHANGE.NotUpdated:
                    message = "Unable to write settings to the registry.";
                    break;
                default:
                    message = "Unknown return value from ChangeDisplaySettings API.";
                    break;
            }
            return message;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DisplayDevice lpDisplayDevice,
            uint dwFlags);

        public enum DISP_CHANGE
        {
            Successful = 0,
            Restart = 1,
            Failed = -1,
            BadMode = -2,
            NotUpdated = -3,
            BadFlags = -4,
            BadParam = -5,
            BadDualView = -6
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Devmode
        {
            private const int Cchdevicename = 0x20;
            private const int Cchformname = 0x20;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)] public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public ScreenOrientation dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DisplayDevice
        {
            [MarshalAs(UnmanagedType.U4)] public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            [MarshalAs(UnmanagedType.U4)] public DisplayDeviceStateFlags StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        #region enums

        public enum QueryDeviceConfigFlags : uint
        {
            QdcAllPaths = 0x00000001,
            QdcOnlyActivePaths = 0x00000002,
            QdcDatabaseCurrent = 0x00000004
        }

        public enum DisplayconfigVideoOutputTechnology : uint
        {
            DisplayconfigOutputTechnologyOther = 0xFFFFFFFF,
            DisplayconfigOutputTechnologyHd15 = 0,
            DisplayconfigOutputTechnologySvideo = 1,
            DisplayconfigOutputTechnologyCompositeVideo = 2,
            DisplayconfigOutputTechnologyComponentVideo = 3,
            DisplayconfigOutputTechnologyDvi = 4,
            DisplayconfigOutputTechnologyHdmi = 5,
            DisplayconfigOutputTechnologyLvds = 6,
            DisplayconfigOutputTechnologyDJpn = 8,
            DisplayconfigOutputTechnologySdi = 9,
            DisplayconfigOutputTechnologyDisplayportExternal = 10,
            DisplayconfigOutputTechnologyDisplayportEmbedded = 11,
            DisplayconfigOutputTechnologyUdiExternal = 12,
            DisplayconfigOutputTechnologyUdiEmbedded = 13,
            DisplayconfigOutputTechnologySdtvdongle = 14,
            DisplayconfigOutputTechnologyMiracast = 15,
            DisplayconfigOutputTechnologyInternal = 0x80000000,
            DisplayconfigOutputTechnologyForceUint32 = 0xFFFFFFFF
        }

        public enum DisplayconfigScanlineOrdering : uint
        {
            DisplayconfigScanlineOrderingUnspecified = 0,
            DisplayconfigScanlineOrderingProgressive = 1,
            DisplayconfigScanlineOrderingInterlaced = 2,
            DisplayconfigScanlineOrderingInterlacedUpperfieldfirst = DisplayconfigScanlineOrderingInterlaced,
            DisplayconfigScanlineOrderingInterlacedLowerfieldfirst = 3,
            DisplayconfigScanlineOrderingForceUint32 = 0xFFFFFFFF
        }

        public enum DisplayconfigRotation : uint
        {
            DisplayconfigRotationIdentity = 1,
            DisplayconfigRotationRotate90 = 2,
            DisplayconfigRotationRotate180 = 3,
            DisplayconfigRotationRotate270 = 4,
            DisplayconfigRotationForceUint32 = 0xFFFFFFFF
        }

        public enum DisplayconfigScaling : uint
        {
            DisplayconfigScalingIdentity = 1,
            DisplayconfigScalingCentered = 2,
            DisplayconfigScalingStretched = 3,
            DisplayconfigScalingAspectratiocenteredmax = 4,
            DisplayconfigScalingCustom = 5,
            DisplayconfigScalingPreferred = 128,
            DisplayconfigScalingForceUint32 = 0xFFFFFFFF
        }
        [DllImport("user32")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lpRect, MonitorEnumProc callback, int dwData);

        private delegate bool MonitorEnumProc(IntPtr hDesktop, IntPtr hdc, ref Rect pRect, int dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public enum DisplayconfigPixelformat : uint
        {
            DisplayconfigPixelformat8Bpp = 1,
            DisplayconfigPixelformat16Bpp = 2,
            DisplayconfigPixelformat24Bpp = 3,
            DisplayconfigPixelformat32Bpp = 4,
            DisplayconfigPixelformatNongdi = 5,
            DisplayconfigPixelformatForceUint32 = 0xffffffff
        }

        public enum DisplayconfigModeInfoType : uint
        {
            DisplayconfigModeInfoTypeSource = 1,
            DisplayconfigModeInfoTypeTarget = 2,
            DisplayconfigModeInfoTypeForceUint32 = 0xFFFFFFFF
        }

        public enum DisplayconfigDeviceInfoType : uint
        {
            DisplayconfigDeviceInfoGetSourceName = 1,
            DisplayconfigDeviceInfoGetTargetName = 2,
            DisplayconfigDeviceInfoGetTargetPreferredMode = 3,
            DisplayconfigDeviceInfoGetAdapterName = 4,
            DisplayconfigDeviceInfoSetTargetPersistence = 5,
            DisplayconfigDeviceInfoGetTargetBaseType = 6,
            DisplayconfigDeviceInfoForceUint32 = 0xFFFFFFFF
        }

        #endregion

        #region structs

        [StructLayout(LayoutKind.Sequential)]
        public struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DisplayconfigPathSourceInfo
        {
            public Luid adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DisplayconfigPathTargetInfo
        {
            public Luid adapterId;
            public uint id;
            public uint modeInfoIdx;
            private readonly DisplayconfigVideoOutputTechnology outputTechnology;
            private readonly DisplayconfigRotation rotation;
            private readonly DisplayconfigScaling scaling;
            private readonly DisplayconfigRational refreshRate;
            private readonly DisplayconfigScanlineOrdering scanLineOrdering;
            public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DisplayconfigRational
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DisplayconfigPathInfo
        {
            public DisplayconfigPathSourceInfo sourceInfo;
            public DisplayconfigPathTargetInfo targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Displayconfig2Dregion
        {
            public uint cx;
            public uint cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DisplayconfigVideoSignalInfo
        {
            public ulong pixelRate;
            public DisplayconfigRational hSyncFreq;
            public DisplayconfigRational vSyncFreq;
            public Displayconfig2Dregion activeSize;
            public Displayconfig2Dregion totalSize;
            public uint videoStandard;
            public DisplayconfigScanlineOrdering scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DisplayconfigTargetMode
        {
            public DisplayconfigVideoSignalInfo targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Pointl
        {
            private readonly int x;
            private readonly int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DisplayconfigSourceMode
        {
            public uint width;
            public uint height;
            public DisplayconfigPixelformat pixelFormat;
            public Pointl position;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct DisplayconfigModeInfoUnion
        {
            [FieldOffset(0)] public DisplayconfigTargetMode targetMode;

            [FieldOffset(0)] public DisplayconfigSourceMode sourceMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DisplayconfigModeInfo
        {
            public DisplayconfigModeInfoType infoType;
            public uint id;
            public Luid adapterId;
            public DisplayconfigModeInfoUnion modeInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DisplayconfigTargetDeviceNameFlags
        {
            public uint value;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DisplayconfigDeviceInfoHeader
        {
            public DisplayconfigDeviceInfoType type;
            public uint size;
            public Luid adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DisplayconfigTargetDeviceName
        {
            public DisplayconfigDeviceInfoHeader header;
            public DisplayconfigTargetDeviceNameFlags flags;
            public DisplayconfigVideoOutputTechnology outputTechnology;
            public ushort edidManufactureId;
            public ushort edidProductCodeId;
            public uint connectorInstance;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public RECT(Rectangle r) : this(r.Left, r.Top, r.Right, r.Bottom) { }

            public int X
            {
                get { return Left; }
                set { Right -= (Left - value); Left = value; }
            }

            public int Y
            {
                get { return Top; }
                set { Bottom -= (Top - value); Top = value; }
            }

            public int Height
            {
                get { return Bottom - Top; }
                set { Bottom = value + Top; }
            }

            public int Width
            {
                get { return Right - Left; }
                set { Right = value + Left; }
            }

            

            public Size Size
            {
                get { return new Size(Width, Height); }
                set { Width = value.Width; Height = value.Height; }
            }

            public static implicit operator Rectangle(RECT r)
            {
                return new Rectangle(r.Left, r.Top, r.Width, r.Height);
            }

            public static implicit operator RECT(Rectangle r)
            {
                return new RECT(r);
            }

            public static bool operator ==(RECT r1, RECT r2)
            {
                return r1.Equals(r2);
            }

            public static bool operator !=(RECT r1, RECT r2)
            {
                return !r1.Equals(r2);
            }

            public bool Equals(RECT r)
            {
                return r.Left == Left && r.Top == Top && r.Right == Right && r.Bottom == Bottom;
            }

            public override bool Equals(object obj)
            {
                if (obj is RECT)
                    return Equals((RECT)obj);
                else if (obj is Rectangle)
                    return Equals(new RECT((Rectangle)obj));
                return false;
            }

            public override int GetHashCode()
            {
                return ((Rectangle)this).GetHashCode();
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.CurrentCulture, "{{Left={0},Top={1},Right={2},Bottom={3}}}", Left, Top, Right, Bottom);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public Int32 cbSize;        // Specifies the size, in bytes, of the structure. 
            public Int32 flags;         // Specifies the cursor state. This parameter can be one of the following values:
            public IntPtr hCursor;          // Handle to the cursor. 
            public POINT ptScreenPos;       // A POINT structure that receives the screen coordinates of the cursor. 
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ICONINFO
        {
            public bool fIcon;         // Specifies whether this structure defines an icon or a cursor. A value of TRUE specifies 
            public Int32 xHotspot;     // Specifies the x-coordinate of a cursor's hot spot. If this structure defines an icon, the hot 
            public Int32 yHotspot;     // Specifies the y-coordinate of the cursor's hot spot. If this structure defines an icon, the hot 
            public IntPtr hbmMask;     // (HBITMAP) Specifies the icon bitmask bitmap. If this structure defines a black and white icon, 
            public IntPtr hbmColor;    // (HBITMAP) Handle to the icon color bitmap. This member can be optional if this 
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public Int32 x;
            public Int32 y;
        }
        public const int Width = 0;
        public const int Height = 1;
        public struct ScreenSize
        {
            public int Width;
            public int Height;
        }
        public const Int32 CURSOR_SHOWING = 0x00000001;

        [DllImport("user32.dll", EntryPoint = "GetDesktopWindow")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", EntryPoint = "GetDC")]
        public static extern IntPtr GetDesktopContext(IntPtr ptr);

        [DllImport("user32.dll", EntryPoint = "GetSystemMetrics")]
        public static extern int GetSystemMetrics(int abc);

        [DllImport("user32.dll", EntryPoint = "GetWindowDC")]
        public static extern IntPtr GetWindowDesktopContext(Int32 ptr);

        [DllImport("user32.dll", EntryPoint = "ReleaseDC")]
        public static extern IntPtr ReleaseDesktopContext(IntPtr hWnd, IntPtr hDc);

        [DllImport("user32.dll", EntryPoint = "GetCursorInfo")]
        public static extern bool GetCursorInfo(out CursorInfo pci);

        [DllImport("user32.dll", EntryPoint = "CopyIcon")]
        public static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("user32.dll", EntryPoint = "GetIconInfo")]
        public static extern bool GetIconInfo(IntPtr hIcon, out IconInfo piconinfo);

        [DllImport("user32.dll", EntryPoint = "DestroyIcon")]
        public static extern bool DestroyIcon(IntPtr hIcon);
        #endregion
        [StructLayout(LayoutKind.Sequential)]
        public struct IconInfo
        {
            public bool IsIcon;         // Specifies whether this structure defines an icon or a cursor. A value of TRUE specifies 
            public Int32 Xcoord;     // Specifies the x-coordinate of a cursor's hot spot. If this structure defines an icon, the hot 
            public Int32 Ycoord;     // Specifies the y-coordinate of the cursor's hot spot. If this structure defines an icon, the hot 
            public IntPtr Bitmask;     // (HBITMAP) Specifies the icon bitmask bitmap. If this structure defines a black and white icon, 
            public IntPtr Color;    // (HBITMAP) Handle to the icon color bitmap. This member can be optional if this 
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public Int32 X;
            public Int32 Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CursorInfo
        {
            public Int32 Size;        // Specifies the size, in bytes, of the structure. 
            public Int32 State;         // Specifies the cursor state. This parameter can be one of the following values:
            public IntPtr Handle;          // Handle to the cursor. 
            public Point Coordinates;       // A POINT structure that receives the screen coordinates of the cursor. 
        }

        #region DLL-Imports

        [DllImport("user32.dll")]
        public static extern int GetDisplayConfigBufferSizes(
            QueryDeviceConfigFlags flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        public static extern int QueryDisplayConfig(
            QueryDeviceConfigFlags flags,
            ref uint numPathArrayElements, [Out] DisplayconfigPathInfo[] pathInfoArray,
            ref uint numModeInfoArrayElements, [Out] DisplayconfigModeInfo[] modeInfoArray,
            IntPtr currentTopologyId
            );

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DisplayconfigTargetDeviceName deviceName);

      
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
       public static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

       public static Rectangle GetWindowRectangle()
        {
            RECT scBounds = new RECT();
            GetWindowRect(GetDesktopWindow(), ref scBounds);
            return scBounds;
        }

        public const int SRCCOPY = 13369376;

        [DllImport("gdi32.dll", EntryPoint = "CreateDC")]
        public static extern IntPtr CreateDesktopContext(IntPtr lpszDriver, string lpszDevice, IntPtr lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll", EntryPoint = "DeleteDC")]
        public static extern IntPtr DeleteDesktopContext(IntPtr hDc);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        public static extern IntPtr DeleteObject(IntPtr hDc);

        [DllImport("gdi32.dll", EntryPoint = "BitBlt")]
        public static extern bool BitBlt(IntPtr hdcDest, int xDest,
                                         int yDest, int wDest,
                                         int hDest, IntPtr hdcSource,
                                         int xSrc, int ySrc, int rasterOp);

        [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleBitmap")]
        public static extern IntPtr CreateCompatibleBitmap
                                    (IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")]
        public static extern IntPtr CreateCompatibleDesktopContext(IntPtr hdc);

        [DllImport("gdi32.dll", EntryPoint = "SelectObject")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr bmp);

        #endregion
    }

    
}