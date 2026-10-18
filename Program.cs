using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatusBarCs
{
    static class Nat
    {
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int GWL_EXSTYLE = -20;
        public const int ULW_ALPHA = 2;
        public const int SW_SHOWNOACTIVATE = 4;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int CX, CY; public SIZE(int x, int y) { CX = x; CY = y; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
            public BLENDFUNCTION(byte op, byte flags, byte alpha, byte fmt) { BlendOp = op; BlendFlags = flags; SourceConstantAlpha = alpha; AlphaFormat = fmt; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct OVERLAPPED { public IntPtr Internal, InternalHigh; public uint Offset, OffsetHigh; public IntPtr hEvent; }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst,
            ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string cls, string win);
        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int idx);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfo(uint action, uint param, ref RECT rect, uint winIni);
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr h, int cmd);

        [DllImport("psapi.dll")]
        public static extern bool EmptyWorkingSet(IntPtr h);
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr obj);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sec,
            uint disp, uint flags, IntPtr tmpl);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(IntPtr h, byte[] buf, uint n, out uint written, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(IntPtr h, byte[] buf, uint n, out uint read, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateEvent(IntPtr attr, bool manual, bool init, string name);
        [DllImport("kernel32.dll")]
        public static extern uint WaitForSingleObject(IntPtr h, uint ms);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetOverlappedResult(IntPtr h, IntPtr ov, out uint n, bool wait);
        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIoEx(IntPtr h, IntPtr ov);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid cls, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr dev, ref Guid cls, uint index, ref SP_DEVICE_INTERFACE_DATA data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data,
            ref SP_DEVICE_INTERFACE_DETAIL_DATA detail, uint size, out uint needed, IntPtr dev);
        [DllImport("setupapi.dll")]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("hid.dll")]
        public static extern void HidD_GetHidGuid(out Guid guid);

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SP_DEVICE_INTERFACE_DETAIL_DATA { public int cbSize; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string DevicePath; }

        public static List<string> HidPaths()
        {
            var list = new List<string>();
            Guid guid;
            HidD_GetHidGuid(out guid);
            IntPtr set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
            if (set == IntPtr.Zero || set == (IntPtr)(-1)) return list;
            try
            {
                var did = new SP_DEVICE_INTERFACE_DATA();
                did.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
                for (uint i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref did); i++)
                {
                    var det = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                    det.cbSize = IntPtr.Size == 8 ? 8 : 6;
                    uint need;
                    if (SetupDiGetDeviceInterfaceDetail(set, ref did, ref det, 1024, out need, IntPtr.Zero))
                        list.Add(det.DevicePath);
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return list;
        }

        public static IntPtr Open(string path)
        {
            IntPtr h = CreateFileW(path, 0x80000000 | 0x40000000, 1 | 2, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
            if (h == (IntPtr)(-1) || h == IntPtr.Zero) return IntPtr.Zero;
            return h;
        }

        public static bool Write(IntPtr h, byte[] data, int timeout = 300)
        {
            IntPtr ev = CreateEvent(IntPtr.Zero, true, false, null);
            var ov = new OVERLAPPED(); ov.hEvent = ev;
            IntPtr pov = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(OVERLAPPED)));
            try
            {
                Marshal.StructureToPtr(ov, pov, false);
                uint n;
                bool ok = WriteFile(h, data, (uint)data.Length, out n, pov);
                if (!ok)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == 997)
                    {
                        if (WaitForSingleObject(ev, (uint)timeout) != 0) { CancelIoEx(h, pov); return false; }
                        if (!GetOverlappedResult(h, pov, out n, false)) return false;
                    }
                    else return false;
                }
                return true;
            }
            finally { Marshal.FreeHGlobal(pov); CloseHandle(ev); }
        }

        public static byte[] Read(IntPtr h, int timeout = 300)
        {
            IntPtr ev = CreateEvent(IntPtr.Zero, true, false, null);
            var ov = new OVERLAPPED(); ov.hEvent = ev;
            IntPtr pov = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(OVERLAPPED)));
            var buf = new byte[256];
            try
            {
                Marshal.StructureToPtr(ov, pov, false);
                uint n;
                bool ok = ReadFile(h, buf, 256, out n, pov);
                if (!ok)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err != 997) return null;
                    if (WaitForSingleObject(ev, (uint)timeout) != 0) { CancelIoEx(h, pov); return null; }
                    if (!GetOverlappedResult(h, pov, out n, false)) return null;
                }
                var res = new byte[n];
                Array.Copy(buf, res, n);
                return res;
            }
            finally { Marshal.FreeHGlobal(pov); CloseHandle(ev); }
        }
    }

    class Item
    {
        public string Name;
        public int? Pct;
    }

    class Cfg
    {
        public float scale { get; set; } = 1f;
        public int x { get; set; } = -1;
        public int y { get; set; } = -1;
    }

    class LastCfg
    {
        public int? lap { get; set; }
        public int? k87 { get; set; }
        public int? rapoo { get; set; }
    }

    static class Log
    {
        static readonly string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
        public static void W(string m)
        {
            try { File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + " " + m + Environment.NewLine); } catch { }
        }
    }

    class BarForm : Form
    {
        const int BASE_W = 500, BASE_R = 22, BASE_M = 22;
        static readonly Color COL_TEXT = Color.FromArgb(245, 242, 243, 246);
        static readonly Color COL_SUB = Color.FromArgb(120, 240, 242, 246);
        static readonly Color COL_TRACK = Color.FromArgb(44, 255, 255, 255);
        static readonly Color COL_OK = Color.FromArgb(128, 160, 192);
        static readonly Color COL_LOW = Color.FromArgb(208, 112, 114);

        float S = 1f;
        int W, H, R, M, CW, CH;
        int px = -1, py = -1;
        Bitmap frame;
        Bitmap wall;
        DateTime wallAt = DateTime.MinValue;
        List<Item> items = new List<Item>();
        bool dragOn, resizeOn;
        Point resizeOrigin;
        int dragPx0, dragPy0;
        Point dragCursor;
        DateTime lastMoveRender = DateTime.MinValue;
        System.Windows.Forms.Timer guard;
        float s0;
        int w0;
        bool autoHide, hidden;
        NotifyIcon tray;
        ToolStripMenuItem miAutoHide;
        System.Windows.Forms.Timer timer;
        readonly string cfgPath, lastPath;
        LastCfg last = new LastCfg();

        public BarForm()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            cfgPath = Path.Combine(dir, "statusbar_cs.json");
            lastPath = Path.Combine(dir, "statusbar_cs_last.json");

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = false;
            SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint, true);
            Text = "状态条";

            LoadCfg();
            Layout();
            LoadLast();
            PlaceDefault();
            CaptureWall();

            BuildTray();

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 10000;
            timer.Tick += (s, e) => RefreshData();
            timer.Start();
            Task.Run(() => RefreshData());

            Log.W($"ctor pos={px},{py} size={CW}x{CH} S={S}");
            guard = new System.Windows.Forms.Timer();
            guard.Interval = 250;
            guard.Tick += (s2, e2) =>
            {
                if (hidden) return;
                bool min = WindowState == FormWindowState.Minimized;
                bool inv = !Nat.IsWindowVisible(Handle);
                if (min || inv)
                {
                    if (min) WindowState = FormWindowState.Normal;
                    Nat.ShowWindow(Handle, 4); // SW_SHOWNOACTIVATE
                    Present();
                }
            };

            MouseDown += OnDown;
            MouseMove += OnMove;
            MouseUp += OnUp;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Nat.WS_EX_LAYERED | Nat.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0112 && ((int)m.WParam & 0xFFF0) == 0xF020) return; // 忽略 SC_MINIMIZE
            base.WndProc(ref m);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
                Present();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Log.W("=== exit (reason: " + e.CloseReason + ") ===");
            base.OnFormClosing(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        new void Layout()
        {
            W = (int)Math.Round(BASE_W * S);
            // 高度自适应：标题区 60 + 行数*34 + 底部余量 4
            int rows = Math.Min(Math.Max(items.Count, 1), 4);
            H = (int)Math.Round((60 + rows * 34 + 4) * S);
            R = (int)Math.Round(BASE_R * S);
            M = (int)Math.Round(BASE_M * S);
            CW = W + M * 2;
            CH = H + M * 2;
            Bounds = new Rectangle(px, py, CW, CH);
        }

        int Sx(float v) { return (int)Math.Round(v * S); }

        void LoadCfg()
        {
            try
            {
                if (File.Exists(cfgPath))
                {
                    var c = JsonSerializer.Deserialize<Cfg>(File.ReadAllText(cfgPath));
                    if (c != null)
                    {
                        S = Math.Max(0.7f, Math.Min(1.8f, c.scale));
                        px = c.x; py = c.y;
                    }
                }
            }
            catch { }
        }

        void SaveCfg()
        {
            try
            {
                File.WriteAllText(cfgPath, JsonSerializer.Serialize(new Cfg { scale = S, x = px, y = py }));
            }
            catch { }
        }

        void LoadLast()
        {
            try
            {
                if (File.Exists(lastPath))
                {
                    var c = JsonSerializer.Deserialize<LastCfg>(File.ReadAllText(lastPath));
                    if (c != null) last = c;
                }
            }
            catch { }
            UpdateItems(false);
        }

        void SaveLast()
        {
            try { File.WriteAllText(lastPath, JsonSerializer.Serialize(last)); } catch { }
        }

        void PlaceDefault()
        {
            var r = new Nat.RECT();
            Nat.SystemParametersInfo(0x0030, 0, ref r, 0);
            if (px < 0 || py < 0)
            {
                px = r.Left + (r.Right - r.Left - CW) / 2;
                py = r.Bottom - CH - 20;
            }
            px = Math.Max(r.Left, Math.Min(px, r.Right - CW));
            py = Math.Max(r.Top, Math.Min(py, r.Bottom - CH));
            Location = new Point(px, py);
        }

        // ---------- 壁纸 ----------
        void CaptureWall()
        {
            try
            {
                int sw = Nat.GetSystemMetrics(0), sh = Nat.GetSystemMetrics(1);
                if (sw <= 0 || sh <= 0) return;
                if (wall == null || wall.Width != sw || wall.Height != sh)
                {
                    if (wall != null) wall.Dispose();
                    wall = new Bitmap(sw, sh, PixelFormat.Format32bppArgb);
                }
                using (var g = Graphics.FromImage(wall))
                {
                    IntPtr hdc = g.GetHdc();
                    IntPtr prog = Nat.FindWindow("Progman", null);
                    bool ok = prog != IntPtr.Zero && Nat.PrintWindow(prog, hdc, 2);
                    g.ReleaseHdc(hdc);
                    if (!ok) g.CopyFromScreen(0, 0, 0, 0, new Size(sw, sh));
                }
                ForceOpaque(wall);
                wallAt = DateTime.UtcNow;
                Log.W($"wall captured {sw}x{sh}");
            }
            catch { }
        }

        static void ForceOpaque(Bitmap b)
        {
            try
            {
                var rect = new Rectangle(0, 0, b.Width, b.Height);
                var d = b.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                int stride = d.Stride;
                var buf = new byte[stride * b.Height];
                Marshal.Copy(d.Scan0, buf, 0, buf.Length);
                for (int i = 3; i < buf.Length; i += 4) buf[i] = 255;
                Marshal.Copy(buf, 0, d.Scan0, buf.Length);
                b.UnlockBits(d);
            }
            catch { }
        }

        Bitmap WallCrop(int pad)
        {
            if (wall == null || (DateTime.UtcNow - wallAt).TotalSeconds > 60) CaptureWall();
            int w = W + pad * 2, h = H + pad * 2;
            var c = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(c))
            {
                g.Clear(Color.FromArgb(255, 22, 26, 34));
                if (wall != null)
                {
                    var rect = new Rectangle(px + M - pad, py + M - pad, w, h);
                    var clipped = Rectangle.Intersect(rect, new Rectangle(0, 0, wall.Width, wall.Height));
                    if (clipped.Width > 0 && clipped.Height > 0)
                    {
                        var dest = new Rectangle(clipped.X - rect.X, clipped.Y - rect.Y, clipped.Width, clipped.Height);
                        g.DrawImage(wall, dest, clipped, GraphicsUnit.Pixel);
                    }
                }
            }
            return c;
        }

        static Bitmap Blur(Bitmap src, int radius)
        {
            int w = Math.Max(1, src.Width / 4), h = Math.Max(1, src.Height / 4);
            var small = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(src, new Rectangle(0, 0, w, h));
            }
            var blurred = BoxBlur(small, Math.Max(1, radius / 4));
            small.Dispose();
            var outp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(outp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(blurred, new Rectangle(0, 0, src.Width, src.Height));
            }
            blurred.Dispose();
            return outp;
        }

        static Bitmap BoxBlur(Bitmap src, int r)
        {
            int w = src.Width, h = src.Height;
            var rect = new Rectangle(0, 0, w, h);
            var data = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride;
            var buf = new byte[stride * h];
            Marshal.Copy(data.Scan0, buf, 0, buf.Length);
            src.UnlockBits(data);

            var tmp = new byte[buf.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int a = 0, rr = 0, gg = 0, bb = 0, n = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int xx = Math.Min(w - 1, Math.Max(0, x + k));
                        int i = y * stride + xx * 4;
                        bb += buf[i]; gg += buf[i + 1]; rr += buf[i + 2]; a += buf[i + 3]; n++;
                    }
                    int o = y * stride + x * 4;
                    tmp[o] = (byte)(bb / n); tmp[o + 1] = (byte)(gg / n);
                    tmp[o + 2] = (byte)(rr / n); tmp[o + 3] = (byte)(a / n);
                }
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int a = 0, rr = 0, gg = 0, bb = 0, n = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int yy = Math.Min(h - 1, Math.Max(0, y + k));
                        int i = yy * stride + x * 4;
                        bb += tmp[i]; gg += tmp[i + 1]; rr += tmp[i + 2]; a += tmp[i + 3]; n++;
                    }
                    int o = y * stride + x * 4;
                    buf[o] = (byte)(bb / n); buf[o + 1] = (byte)(gg / n);
                    buf[o + 2] = (byte)(rr / n); buf[o + 3] = (byte)(a / n);
                }
            }
            var outp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var od = outp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(buf, 0, od.Scan0, buf.Length);
            outp.UnlockBits(od);
            return outp;
        }

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        int AvgLum(Bitmap b)
        {
            try
            {
                long sum = 0; int n = 0;
                for (int y = 0; y < b.Height; y += Math.Max(1, b.Height / 12))
                    for (int x = 0; x < b.Width; x += Math.Max(1, b.Width / 12))
                    {
                        var c = b.GetPixel(x, y);
                        sum += (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                        n++;
                    }
                return n == 0 ? 100 : (int)(sum / n);
            }
            catch { return 100; }
        }

        Bitmap Render()
        {
            var bmp = new Bitmap(CW, CH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                int pad = Math.Max(10, Sx(20));
                Bitmap big = WallCrop(pad);
                Bitmap bigBlur = Blur(big, Math.Max(2, Sx(18)));
                big.Dispose();
                // 裁掉模糊产生的边缘伪影
                var blurred = new Bitmap(W, H, PixelFormat.Format32bppArgb);
                using (var g2 = Graphics.FromImage(blurred))
                    g2.DrawImage(bigBlur, new Rectangle(0, 0, W, H), new Rectangle(pad, pad, W, H), GraphicsUnit.Pixel);
                bigBlur.Dispose();
                int lum = AvgLum(blurred);
                int scrim = lum > 150 ? 82 : (lum > 90 ? 40 : 14);

                // 纹理笔刷填充圆角路径：无裁剪接缝
                using (var panel = RoundRect(new RectangleF(M, M, W, H), R))
                {
                    using (var tb = new TextureBrush(blurred, WrapMode.Clamp))
                    {
                        tb.TranslateTransform(M, M);
                        g.FillPath(tb, panel);
                    }
                    if (scrim > 0)
                        using (var b = new SolidBrush(Color.FromArgb(scrim, 20, 22, 28)))
                            g.FillPath(b, panel);
                }
                blurred.Dispose();

                using (var fTitle = new Font("Microsoft YaHei UI", 20 * S, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var fName = new Font("Microsoft YaHei UI", 17 * S, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var fPct = new Font("Microsoft YaHei UI", 17 * S, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var fCap = new Font("Microsoft YaHei UI", 13 * S, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brText = new SolidBrush(COL_TEXT))
                using (var brSub = new SolidBrush(COL_SUB))
                {
                    // 标题条
                    using (var b = new SolidBrush(COL_OK))
                        g.FillRectangle(b, M + Sx(22), M + Sx(22), Math.Max(2, Sx(3)), Sx(20));
                    g.DrawString("状态条", fTitle, brText, M + Sx(34), M + Sx(19));

                    int known = items.Count(i => i.Pct.HasValue);
                    var sf = new StringFormat { Alignment = StringAlignment.Far };
                    g.DrawString(known + " 台设备", fCap, brSub,
                        new RectangleF(M, M + Sx(24), W - Sx(22), Sx(20)), sf);
                    var sfName = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };

                    var sorted = items.OrderByDescending(i => i.Pct ?? -1).Take(4).ToList();
                    // 名称列宽自适应：取最宽名称（带上限），全部行共用 → 进度条起止一致
                    float nameColW = 0;
                    foreach (var it in sorted)
                        nameColW = Math.Max(nameColW, g.MeasureString(it.Name, fName).Width);
                    nameColW = Math.Min(nameColW + 2, Sx(190));
                    float bx0 = M + Sx(22) + nameColW + Sx(10);
                    float bw0 = Math.Min(Sx(250), Math.Max(Sx(60), W - Sx(74) - bx0));
                    int y = M + Sx(60);
                    foreach (var it in sorted)
                    {
                        bool low = it.Pct.HasValue && it.Pct.Value < 30;
                        Color col = low ? COL_LOW : COL_OK;
                        var nameRect = new RectangleF(M + Sx(22), y - Sx(2), nameColW, Sx(24));
                        g.DrawString(it.Name, fName, brText, nameRect, sfName);

                        float bx = bx0, bw = bw0, bh = Math.Max(5, Sx(9));
                        float by = y + Sx(8);
                        using (var p = RoundRect(new RectangleF(bx, by, bw, bh), bh / 2))
                        using (var b = new SolidBrush(COL_TRACK))
                            g.FillPath(b, p);
                        if (it.Pct.HasValue && it.Pct.Value > 0)
                        {
                            float fw = Math.Max(bh, bw * it.Pct.Value / 100f);
                            using (var p = RoundRect(new RectangleF(bx, by, fw, bh), bh / 2))
                            using (var b = new SolidBrush(col))
                                g.FillPath(b, p);
                        }
                        using (var b = new SolidBrush(it.Pct.HasValue ? col : COL_SUB))
                        {
                            string txt = it.Pct.HasValue ? it.Pct.Value + "%" : "—";
                            g.DrawString(txt, fPct, b,
                                new RectangleF(M, y - Sx(2), W - Sx(22), Sx(22)), sf);
                        }
                        y += Sx(34);
                    }
                }

            }
            return bmp;
        }

        void Present()
        {
            if (frame == null) return;
            IntPtr hdcScreen = Nat.GetDC(IntPtr.Zero);
            IntPtr hdcMem = Nat.CreateCompatibleDC(hdcScreen);
            IntPtr hBmp = frame.GetHbitmap(Color.FromArgb(0));
            IntPtr old = Nat.SelectObject(hdcMem, hBmp);
            var size = new Nat.SIZE(CW, CH);
            var src = new Nat.POINT(0, 0);
            var dst = new Nat.POINT(px, py);
            var blend = new Nat.BLENDFUNCTION(0, 0, 255, 1);
            bool ok = Nat.UpdateLayeredWindow(Handle, hdcScreen, ref dst, ref size, hdcMem, ref src, 0, ref blend, Nat.ULW_ALPHA);
            Log.W($"ULW ok={ok} err={Marshal.GetLastWin32Error()} dst={px},{py} size={CW}x{CH} handle={Handle}");
            Nat.SelectObject(hdcMem, old);
            Nat.DeleteObject(hBmp);
            Nat.DeleteDC(hdcMem);
            Nat.ReleaseDC(IntPtr.Zero, hdcScreen);
        }

        DateTime trimAt = DateTime.MinValue;

        void TrimMemory()
        {
            if ((DateTime.UtcNow - trimAt).TotalSeconds < 15) return;
            trimAt = DateTime.UtcNow;
            try
            {
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                Nat.EmptyWorkingSet(Nat.GetCurrentProcess());
            }
            catch { }
        }

        void Redraw()
        {
            var old = frame;
            frame = Render();
            if (old != null && !ReferenceEquals(old, frame)) old.Dispose();
            Present();
            TrimMemory();
        }

        // ---------- 数据 ----------
        static int? ReadLaptop()
        {
            try
            {
                var s = new PowerStatus();
                if (!GetSystemPowerStatus(ref s)) return null;
                if (s.BatteryLifePercent == 255) return null;
                return s.BatteryLifePercent;
            }
            catch { return null; }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PowerStatus
        {
            public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
            public uint BatteryLifeTime, BatteryFullLifeTime;
        }
        [DllImport("kernel32.dll")]
        static extern bool GetSystemPowerStatus(ref PowerStatus s);

        static readonly byte[] K87_PKT = BuildK87();
        static byte[] BuildK87()
        {
            var b = new byte[65];
            byte[] head = { 0x00, 0x55, 0x05, 0x00, 0x78, 0x38, 0x40, 0x00, 0x00 };
            Array.Copy(head, b, head.Length);
            return b;
        }

        static int? ReadK87(List<string> paths)
        {
            foreach (var p in paths)
            {
                string lp = p.ToLowerInvariant();
                if (!lp.Contains("vid_3837") || !lp.Contains("pid_3033")) continue;
                IntPtr h = Nat.Open(p);
                if (h == IntPtr.Zero) continue;
                try
                {
                    while (Nat.Read(h, 15) != null) { }
                    if (!Nat.Write(h, K87_PKT)) continue;
                    var d = Nat.Read(h, 400);
                    if (d != null && d.Length > 41 && d[1] == 0xAA && d[2] == 0x05)
                    {
                        int v = d[41];
                        if (v > 0 && v <= 100) return v;
                    }
                }
                finally { Nat.CloseHandle(h); }
            }
            return null;
        }

        static int rapooSeq = 0;

        static int? ReadRapoo(List<string> paths)
        {
            string pw = null, pr = null;
            foreach (var p in paths)
            {
                string lp = p.ToLowerInvariant();
                if (!lp.Contains("vid_24ae") || !lp.Contains("pid_1417")) continue;
                if (pw == null && lp.Contains("col05")) pw = p;
                if (pr == null && lp.Contains("col09")) pr = p;
            }
            if (pw == null || pr == null) return null;
            IntPtr hw = Nat.Open(pw), hr = Nat.Open(pr);
            if (hw == IntPtr.Zero || hr == IntPtr.Zero)
            {
                if (hw != IntPtr.Zero) Nat.CloseHandle(hw);
                if (hr != IntPtr.Zero) Nat.CloseHandle(hr);
                return null;
            }
            try
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var buf = new byte[32];
                    buf[0] = (byte)(rapooSeq & 0xFF); rapooSeq = (rapooSeq + 1) & 0xFF;
                    buf[1] = 2; buf[2] = 0; buf[3] = 0xAA; buf[4] = 0x01;
                    int sum = 6;
                    for (int i = 0; i < 5; i++) sum += buf[i];
                    buf[5] = (byte)(sum & 0xFF);
                    var send = new byte[33];
                    send[0] = 6;
                    Array.Copy(buf, 0, send, 1, 32);
                    if (!Nat.Write(hw, send)) return null;
                    var end = DateTime.UtcNow.AddMilliseconds(700);
                    while (DateTime.UtcNow < end)
                    {
                        var d = Nat.Read(hr, 120);
                        if (d != null && d.Length > 8 && d[0] == 0x07)
                        {
                            int v = d[8];
                            if (v >= 0 && v <= 100) return v;
                        }
                    }
                    Thread.Sleep(120);
                }
            }
            finally { Nat.CloseHandle(hw); Nat.CloseHandle(hr); }
            return null;
        }

        void UpdateItems(bool read)
        {
            if (read) { }
            items = new List<Item>
            {
                new Item { Name = "笔记本", Pct = last.lap },
                new Item { Name = "键盘 K87S", Pct = last.k87 },
                new Item { Name = "鼠标 VT3 MAX", Pct = last.rapoo },
            };
            Redraw();
        }

        int NextIntervalMs()
        {
            int lap = last.lap ?? 100;
            int ms = lap >= 90 ? 600000 : (lap >= 60 ? 180000 : 60000);
            // 外设读不到时缩短，便于重连后及时刷新
            if (!last.k87.HasValue || !last.rapoo.HasValue) ms = Math.Min(ms, 180000);
            return ms;
        }

        void RefreshData()
        {
            Task.Run(() =>
            {
                try
                {
                    var paths = Nat.HidPaths();
                    int? lap = ReadLaptop();
                    int? k87 = ReadK87(paths);
                    int? rp = ReadRapoo(paths);
                    bool changed = false;
                    if (lap.HasValue && lap != last.lap) { last.lap = lap; changed = true; }
                    if (k87.HasValue && k87 != last.k87) { last.k87 = k87; changed = true; }
                    if (rp.HasValue && rp != last.rapoo) { last.rapoo = rp; changed = true; }
                    if (changed) SaveLast();
                    List<Item> btList = null;
                    try { btList = Bt.Scan(); } catch { btList = null; }
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            var list = new List<Item>
                            {
                                new Item { Name = "笔记本", Pct = last.lap },
                                new Item { Name = "键盘 K87S", Pct = last.k87 },
                                new Item { Name = "鼠标 VT3 MAX", Pct = last.rapoo },
                            };
                            if (btList != null) list.AddRange(btList);
                            items = list;
                            Layout(); // 设备数变化 → 高度自适应
                            Redraw();
                            int iv = NextIntervalMs();
                            timer.Interval = iv;
                            tray.Text = "状态条 | 刷新间隔 " + (iv / 60000.0).ToString("0.##") + " 分钟";
                            Log.W($"next interval {iv}ms lap={last.lap}");
                        }));
                    }
                    catch { }
                }
                catch { }
            });
        }

        // ---------- 交互 ----------
        void OnDown(object s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            int margin = Math.Max(14, Sx(24));
            // 热区必须在不透明面板内（分层窗口透明像素不接鼠标）
            bool corner = e.X >= M + W - margin && e.Y >= M + H - margin;
            if (corner)
            {
                resizeOn = true; resizeOrigin = e.Location; s0 = S; w0 = CW;
            }
            else
            {
                dragOn = true;
                dragPx0 = px; dragPy0 = py;
                dragCursor = Cursor.Position;
            }
            Capture = true;
        }

        void OnMove(object s, MouseEventArgs e)
        {
            if (resizeOn)
            {
                int dw = e.X - resizeOrigin.X;
                float ns = s0 * (w0 + dw) / (float)w0;
                ns = Math.Max(0.7f, Math.Min(1.8f, (float)Math.Round(ns, 3)));
                if (Math.Abs(ns - S) >= 0.01f)
                {
                    S = ns;
                    Layout();
                    Redraw();
                }
                return;
            }
            if (dragOn)
            {
                var cur = Cursor.Position;
                px = dragPx0 + (cur.X - dragCursor.X);
                py = dragPy0 + (cur.Y - dragCursor.Y);
                // 移动过程中实时吸附
                int sw = Nat.GetSystemMetrics(0);
                if (Math.Abs(px + CW / 2 - sw / 2) < Math.Max(24, Sx(32)))
                {
                    px = sw / 2 - CW / 2;
                }
                Location = new Point(px, py);
                // 背景跟着位置重画（限频 30ms）
                if ((DateTime.UtcNow - lastMoveRender).TotalMilliseconds >= 30)
                {
                    lastMoveRender = DateTime.UtcNow;
                    Redraw();
                }
                else Present();
            }
        }

        void OnUp(object s, MouseEventArgs e)
        {
            if (!(dragOn || resizeOn)) return;
            dragOn = false; resizeOn = false;
            Capture = false;
            Location = new Point(px, py);
            Present();
            SaveCfg();
        }

        // ---------- 托盘 ----------
        void BuildTray()
        {
            tray = new NotifyIcon();
            tray.Text = "状态条";
            tray.Icon = MakeIcon();
            var menu = new ContextMenuStrip();
            menu.Items.Add("显示 / 隐藏", null, (s, e) => ToggleHide());
            menu.Items.Add("刷新", null, (s, e) => RefreshData());
            menu.Items.Add("打开日志", null, (s, e) =>
            {
                try { System.Diagnostics.Process.Start("notepad.exe", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log")); }
                catch { }
            });
            miAutoHide = new ToolStripMenuItem("有窗口时自动隐藏", null, (s, e) =>
            {
                autoHide = !autoHide;
                miAutoHide.Checked = autoHide;
                if (!autoHide) hidden = false;
            });
            miAutoHide.Checked = autoHide;
            menu.Items.Add(miAutoHide);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("开机自启", null, (s, e) => InstallAutostart());
            menu.Items.Add("退出", null, (s, e) => Close());
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => ToggleHide();
            tray.Visible = true;
        }

        Icon MakeIcon()
        {
            var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = RoundRect(new RectangleF(3, 8, 26, 16), 5))
                using (var b = new SolidBrush(Color.FromArgb(118, 146, 174)))
                    g.FillPath(b, p);
                using (var p = RoundRect(new RectangleF(7, 13, 13, 6), 3))
                using (var b = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                    g.FillPath(b, p);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        void ToggleHide()
        {
            hidden = !hidden;
            Visible = !hidden;
            if (!hidden) Redraw();
        }

        void InstallAutostart()
        {
            try
            {
                string exe = Application.ExecutablePath;
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    k.SetValue("StatusBar", "\"" + exe + "\"");
                }
                // 清理旧版（Python）遗留的自启脚本，防止开机双开
                string legacy = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup), "状态条.cmd");
                if (File.Exists(legacy))
                {
                    try { File.Delete(legacy); } catch { }
                }
                tray.ShowBalloonTip(1500, "状态条", "已设置开机自启", ToolTipIcon.Info);
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Log.W($"OnShown visible={Visible} bounds={Bounds}");
            Redraw();
            guard.Start();
        }
    }

    static class Program
    {
        static Mutex singleInstance;

        [STAThread]
        static void Main()
        {
            // 单实例锁：开机自启 + 系统重启应用恢复叠加时，第二个实例静默退出
            bool createdNew;
            singleInstance = new Mutex(true, @"Local\StatusBarCs.SingleInstance", out createdNew);
            if (!createdNew) return;

            try { SetProcessDpiAwareness(2); } catch { try { Nat.SetProcessDPIAware(); } catch { } }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BarForm());
        }

        [DllImport("shcore.dll")]
        static extern int SetProcessDpiAwareness(int value);
    }
}
