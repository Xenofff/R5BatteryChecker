using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace R5BatteryChecker
{
    static class Program
    {
        private const string AppGuid = "R5BatteryChecker_Unique_Mutex_84A12B";

        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, AppGuid, out createdNew))
            {
                if (!createdNew)
                {
                    // Already running
                    return;
                }

                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (var trayContext = new TrayAppContext())
                {
                    Application.Run(trayContext);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }

    public class BatteryInfo
    {
        public bool IsConnected { get; set; }
        public int Percentage { get; set; }
        public bool IsCharging { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon;
        private readonly ContextMenuStrip contextMenu;
        private readonly ToolStripMenuItem statusMenuItem;
        private readonly ToolStripMenuItem autoStartMenuItem;
        private readonly System.Windows.Forms.Timer pollTimer;
        private readonly SynchronizationContext syncContext;

        private BatteryInfo lastInfo = new BatteryInfo { IsConnected = false, Percentage = -1, IsCharging = false };
        private bool isPolling = false;
        private IntPtr currentIconHandle = IntPtr.Zero;

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "AttackSharkR5Battery";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public TrayAppContext()
        {
            syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            contextMenu = new ContextMenuStrip();
            contextMenu.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            statusMenuItem = new ToolStripMenuItem("Attack Shark R5 Ultra: Поиск...")
            {
                Enabled = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            contextMenu.Items.Add(statusMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            var refreshMenuItem = new ToolStripMenuItem("Обновить сейчас", null, OnRefreshClicked);
            contextMenu.Items.Add(refreshMenuItem);

            autoStartMenuItem = new ToolStripMenuItem("Автозагрузка с Windows", null, OnAutoStartClicked)
            {
                Checked = IsAutoStartEnabled()
            };
            contextMenu.Items.Add(autoStartMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());
            var exitMenuItem = new ToolStripMenuItem("Выход", null, OnExitClicked);
            contextMenu.Items.Add(exitMenuItem);

            notifyIcon = new NotifyIcon
            {
                Text = "Attack Shark R5 Ultra: Поиск устройства...",
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            // Set initial icon
            UpdateIcon(lastInfo);

            // Timer for periodic polling every 30 seconds
            pollTimer = new System.Windows.Forms.Timer { Interval = 30000 };
            pollTimer.Tick += (s, e) => TriggerPoll();
            pollTimer.Start();

            // Initial immediate poll
            TriggerPoll();
        }

        private void TriggerPoll()
        {
            if (isPolling) return;
            isPolling = true;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                BatteryInfo info = R5HidReader.QueryBattery();
                syncContext.Post(state =>
                {
                    var res = (BatteryInfo)state;
                    OnBatteryUpdated(res);
                    isPolling = false;
                }, info);
            });
        }

        private void OnBatteryUpdated(BatteryInfo info)
        {
            lastInfo = info;

            if (info.IsConnected)
            {
                string chargingText = info.IsCharging ? " (Заряжается ⚡)" : "";
                string title = string.Format("Attack Shark R5 Ultra: {0}%{1}", info.Percentage, chargingText);
                statusMenuItem.Text = title;

                string tooltip = title;
                if (tooltip.Length >= 64) tooltip = tooltip.Substring(0, 63);
                notifyIcon.Text = tooltip;
            }
            else
            {
                if (lastInfo.Percentage >= 0)
                {
                    string title = string.Format("Attack Shark R5 Ultra: {0}% (Спит)", lastInfo.Percentage);
                    statusMenuItem.Text = title;
                    notifyIcon.Text = title;
                }
                else
                {
                    statusMenuItem.Text = "Attack Shark R5 Ultra: Не подключена";
                    notifyIcon.Text = "Attack Shark R5 Ultra: Не подключена";
                }
            }

            UpdateIcon(info);
        }

        private void UpdateIcon(BatteryInfo info)
        {
            int size = SystemInformation.SmallIconSize.Width;
            if (size < 16) size = 16;
            float scale = (float)size / 16f;

            using (Bitmap bmp = new Bitmap(size, size))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                string text;
                Color textColor;

                if (!info.IsConnected && info.Percentage < 0)
                {
                    text = "--";
                    textColor = Color.FromArgb(170, 170, 170);
                }
                else
                {
                    int pct = Math.Max(0, Math.Min(100, info.Percentage));
                    text = pct.ToString();

                    if (info.IsCharging)
                    {
                        textColor = Color.FromArgb(0, 245, 255); // Vivid Cyan for charging
                    }
                    else if (pct > 40)
                    {
                        textColor = Color.FromArgb(75, 255, 95); // Vivid Lime Green
                    }
                    else if (pct >= 20)
                    {
                        textColor = Color.FromArgb(255, 210, 20); // Amber / Yellow
                    }
                    else
                    {
                        textColor = Color.FromArgb(255, 60, 60); // Red
                    }
                }

                string fontName = (text.Length >= 3) ? "Arial Narrow" : "Arial";
                float baseSize = (text.Length >= 3) ? 11f : (text.Length == 1 ? 15f : 14f);
                if (text == "--") baseSize = 13f;
                float fontSize = baseSize * scale;
                float shadowDist = Math.Max(1f, (float)Math.Round(scale));

                using (Font font = new Font(fontName, fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush shadowBrush = new SolidBrush(Color.FromArgb(240, 0, 0, 0)))
                using (Brush textBrush = new SolidBrush(textColor))
                {
                    var sf = new StringFormat(StringFormat.GenericTypographic)
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.NoWrap
                    };

                    // Draw 1px drop shadow for crisp readability on both dark and light taskbars
                    g.DrawString(text, font, shadowBrush, new RectangleF(shadowDist, shadowDist, size, size), sf);
                    g.DrawString(text, font, textBrush, new RectangleF(0, 0, size, size), sf);
                }

                // If charging, draw a tiny bright dot at top-right corner
                if (info.IsCharging)
                {
                    using (Brush boltBrush = new SolidBrush(Color.FromArgb(255, 255, 0)))
                    {
                        int dotSize = Math.Max(2, (int)(3 * scale));
                        g.FillRectangle(boltBrush, size - dotSize, 0, dotSize, dotSize);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                Icon newIcon = Icon.FromHandle(hIcon);
                notifyIcon.Icon = newIcon;

                if (currentIconHandle != IntPtr.Zero)
                {
                    DestroyIcon(currentIconHandle);
                }
                currentIconHandle = hIcon;
            }
        }

        private void OnRefreshClicked(object sender, EventArgs e)
        {
            TriggerPoll();
        }

        private void OnAutoStartClicked(object sender, EventArgs e)
        {
            bool current = autoStartMenuItem.Checked;
            bool newState = !current;
            SetAutoStart(newState);
            autoStartMenuItem.Checked = IsAutoStartEnabled();
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    return key != null && key.GetValue(AppName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exePath = Application.ExecutablePath;
                        key.SetValue(AppName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось изменить настройку автозагрузки: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            pollTimer.Stop();
            notifyIcon.Visible = false;
            if (currentIconHandle != IntPtr.Zero)
            {
                DestroyIcon(currentIconHandle);
                currentIconHandle = IntPtr.Zero;
            }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pollTimer.Dispose();
                notifyIcon.Dispose();
                contextMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public static class R5HidReader
    {
        [StructLayout(LayoutKind.Sequential)]
        struct GUID
        {
            public int Data1;
            public short Data2;
            public short Data3;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] Data4;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public GUID InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll", SetLastError = true)]
        static extern void HidD_GetHidGuid(out GUID HidGuid);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr SetupDiGetClassDevs(ref GUID ClassGuid, string Enumerator, IntPtr hwndParent, int Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref GUID InterfaceClassGuid, int MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, ref int RequiredSize, IntPtr DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetPreparsedData(IntPtr HidDeviceObject, out IntPtr PreparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        static extern int HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_SetFeature(IntPtr HidDeviceObject, byte[] ReportBuffer, int ReportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetFeature(IntPtr HidDeviceObject, byte[] ReportBuffer, int ReportBufferLength);

        const int DIGCF_PRESENT = 0x02;
        const int DIGCF_DEVICEINTERFACE = 0x10;
        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_READ = 0x01;
        const uint FILE_SHARE_WRITE = 0x02;
        const uint OPEN_EXISTING = 3;

        public static BatteryInfo QueryBattery()
        {
            var result = new BatteryInfo
            {
                IsConnected = false,
                Percentage = -1,
                IsCharging = false,
                LastUpdated = DateTime.Now
            };

            GUID hidGuid;
            HidD_GetHidGuid(out hidGuid);

            IntPtr hDevInfo = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (hDevInfo == new IntPtr(-1)) return result;

            SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
            ifData.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));

            int index = 0;
            while (SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref hidGuid, index, ref ifData))
            {
                int reqSize = 0;
                SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, IntPtr.Zero, 0, ref reqSize, IntPtr.Zero);
                if (reqSize > 0)
                {
                    IntPtr detailBuffer = Marshal.AllocHGlobal(reqSize);
                    Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 5);

                    if (SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, detailBuffer, reqSize, ref reqSize, IntPtr.Zero))
                    {
                        IntPtr pDevicePath = new IntPtr(detailBuffer.ToInt64() + 4);
                        string path = Marshal.PtrToStringUni(pDevicePath);

                        if (path != null)
                        {
                            string low = path.ToLower();
                            // VID 373E: R5 Ultra Wired (0046), R5 Ultra Wireless 4K/8K (0047)
                            if (low.Contains("vid_373e") && (low.Contains("pid_0046") || low.Contains("pid_0047")))
                            {
                                if (TryReadDevice(path, result))
                                {
                                    Marshal.FreeHGlobal(detailBuffer);
                                    break;
                                }
                            }
                        }
                    }
                    Marshal.FreeHGlobal(detailBuffer);
                }
                index++;
            }

            SetupDiDestroyDeviceInfoList(hDevInfo);
            return result;
        }

        private static bool TryReadDevice(string path, BatteryInfo result)
        {
            IntPtr handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle == new IntPtr(-1)) return false;

            bool success = false;
            try
            {
                IntPtr preparsed;
                if (HidD_GetPreparsedData(handle, out preparsed))
                {
                    HIDP_CAPS caps;
                    HidP_GetCaps(preparsed, out caps);
                    HidD_FreePreparsedData(preparsed);

                    // Attack Shark R5 vendor control interface has FeatureReportByteLength 65
                    if (caps.FeatureReportByteLength == 65)
                    {
                        byte[] outBuf = new byte[65];
                        outBuf[0] = 0;
                        outBuf[3] = 2;
                        outBuf[4] = 2;
                        outBuf[6] = 131; // 0x83 command

                        if (HidD_SetFeature(handle, outBuf, 65))
                        {
                            Thread.Sleep(50);
                            byte[] inBuf = new byte[65];
                            inBuf[0] = 0;

                            if (HidD_GetFeature(handle, inBuf, 65))
                            {
                                // Check protocol match: inBuf[1]==0xA1 (161), inBuf[4]==2, inBuf[6]==0x83 (131)
                                if (inBuf[1] == 161 && inBuf[4] == 2 && inBuf[6] == 131)
                                {
                                    int charging = inBuf[7];
                                    int battery = inBuf[8];

                                    // If charging and 100%, web driver logic caps to 99% until complete
                                    if (charging == 1 && battery == 100) battery = 99;

                                    result.IsConnected = true;
                                    result.IsCharging = (charging == 1);
                                    result.Percentage = battery;
                                    result.LastUpdated = DateTime.Now;
                                    success = true;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                success = false;
            }
            finally
            {
                CloseHandle(handle);
            }

            return success;
        }
    }
}
