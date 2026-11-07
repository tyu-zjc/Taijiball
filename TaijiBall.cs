// TaijiBall —— 太极悬浮球：八卦环表示内存占用，空闲时收成黑白胶囊贴到屏幕边缘
// 编译: csc /target:winexe /optimize+ /codepage:65001 /win32manifest:TaijiBall.manifest /out:TaijiBall.exe TaijiBall.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace TaijiBall
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);



        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length >= 1 && args[0] == "--opt")
            {
                MemoryOptimizer.RunChild(args.Length >= 2 ? args[1] : null);
                return;
            }
            if (args.Length >= 1 && args[0] == "--opt-test")
            {
                MemoryOptimizer.RunTest(args.Length >= 2 ? args[1] : "opt-test.log");
                return;
            }
            if (args.Length >= 1 && args[0] == "--shot")
            {
                SetProcessDPIAware();
                int sw = GetSystemMetrics(0);
                int sh = GetSystemMetrics(1);
                using (Bitmap b = new Bitmap(sw, sh))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, new Size(sw, sh));
                    }
                    b.Save(args.Length >= 2 ? args[1] : "shot.png", ImageFormat.Png);
                }
                return;
            }
            if (args.Length >= 1 && args[0] == "--settings")
            {
                Application.EnableVisualStyles();
                Application.Run(new SettingsForm(AppConfig.Load()));
                return;
            }
            if (args.Length >= 3 && args[0] == "--fx")
            {
                float t = float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
                string path = args[2];
                int w = BallForm.PanelW;
                int h = BallForm.PanelH;
                using (Bitmap inner = BallRenderer.Render(w, h, 0f, 62f, 1f, "62%", true, 0f, null, -1, 0f,
                                                          BallRenderer.Smooth(t, 0f, 0.35f), true))
                using (Bitmap canvas = new Bitmap(340, 260, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics g = Graphics.FromImage(canvas))
                    {
                        g.Clear(Color.FromArgb(255, 42, 46, 54));
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(inner, 8, 8);
                        float ccx = 8 + w / 2f;
                        float ccy = 8 + h / 2f - 6f;
                        BallRenderer.DrawFlow(g, ccx, ccy, BallForm.BaguaMid, 0f, new PointF(300f, 190f), t,
                                              1f - BallRenderer.Smooth(t, 0.7f, 1f));
                    }
                    canvas.Save(path, ImageFormat.Png);
                }
                return;
            }
            if (args.Length >= 1 && args[0] == "--render")
            {
                float a = args.Length >= 2 ? float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 0f;
                string path = args.Length >= 3 ? args[2] : "taiji.png";
                int usage = args.Length >= 4 ? int.Parse(args[3]) : 58;
                float exp = args.Length >= 5 ? float.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 1f;
                int w = (int)Math.Round(BallForm.CapsW + (BallForm.PanelW - BallForm.CapsW) * exp);
                int h = (int)Math.Round(BallForm.CapsH + (BallForm.PanelH - BallForm.CapsH) * exp);
                int hi = args.Length >= 6 ? int.Parse(args[5]) : -1;
                float gold = args.Length >= 7 ? float.Parse(args[6], System.Globalization.CultureInfo.InvariantCulture) : 0f;
                float[] hov = new float[8];
                if (hi >= 0 && hi < 8) hov[hi] = 1f;
                using (Bitmap b = BallRenderer.Render(w, h, a, usage, exp, usage + "%", true, 0f, hov, hi, gold, 0f, true))
                {
                    b.Save(path, ImageFormat.Png);
                }
                return;
            }
            try { SetProcessDPIAware(); } catch { }


            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TaijiBall-error.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + e.Exception + Environment.NewLine + Environment.NewLine);
                }
                catch { }
            };            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BallForm());
        }
    }

    // ───────────────────────────── 配置 ─────────────────────────────
    internal sealed class AppConfig
    {
        public bool ShowUsage = true;   // 是否绘制八卦环
        public bool SpinCw = true;      // 空闲时顺时针（优化中反向）
        public bool FillUp = true;      // 胶囊从下往上填充
        public readonly List<string> Exclusions = new List<string>();

        public static string ConfigPath
        {
            get
            {
                string custom = Environment.GetEnvironmentVariable("TAJIBALL_CONFIG");
                if (!string.IsNullOrEmpty(custom)) return custom;
                return Path.Combine(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaijiBall"),
                    "config.ini");
            }
        }

        public static AppConfig Load()
        {
            AppConfig cfg = new AppConfig();
            try
            {
                if (!File.Exists(ConfigPath)) return cfg;
                foreach (string raw in File.ReadAllLines(ConfigPath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();
                    if (key == "showusage") cfg.ShowUsage = val != "0";
                    else if (key == "spincw") cfg.SpinCw = val != "0";
                    else if (key == "fillup") cfg.FillUp = val != "0";
                    else if (key == "exclude" && val.Length > 0) cfg.AddExclusion(val);
                }
            }
            catch { }
            return cfg;
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 太极内存球配置");
                sb.AppendLine("ShowUsage=" + (ShowUsage ? "1" : "0"));
                sb.AppendLine("SpinCw=" + (SpinCw ? "1" : "0"));
                sb.AppendLine("FillUp=" + (FillUp ? "1" : "0"));
                foreach (string name in Exclusions) sb.AppendLine("Exclude=" + name);
                File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string n = name.Trim().ToLowerInvariant();
            if (!n.EndsWith(".exe")) n += ".exe";
            return n;
        }

        public void AddExclusion(string name)
        {
            string n = Normalize(name);
            if (n.Length == 0) return;
            if (!Exclusions.Contains(n)) Exclusions.Add(n);
        }

        public bool IsExcluded(string processName)
        {
            string n = Normalize(processName);
            if (n == "taijiball.exe") return true;
            return Exclusions.Contains(n);
        }
    }

    // ───────────────────────────── 太极图案 ─────────────────────────────
    internal static class Taiji
    {
        /// <summary>方形透明太极位图（内部 4 倍超采样）。</summary>
        public static Bitmap Render(int size, float angle)
        {
            const int ss = 4;
            int big = size * ss;
            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (Bitmap hi = new Bitmap(big, big, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(hi))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    g.TranslateTransform(big / 2f, big / 2f);
                    g.RotateTransform(angle);
                    g.TranslateTransform(-big / 2f, -big / 2f);
                    Draw(g, new RectangleF(big * 0.03f, big * 0.03f, big * 0.94f, big * 0.94f));
                }
                using (Graphics g2 = Graphics.FromImage(result))
                {
                    g2.Clear(Color.Transparent);
                    g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g2.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g2.CompositingQuality = CompositingQuality.HighQuality;
                    g2.DrawImage(hi, new Rectangle(0, 0, size, size));
                }
            }
            return result;
        }

        public static void Draw(Graphics g, RectangleF rect)
        {
            float d = Math.Min(rect.Width, rect.Height);
            float r = d / 2f;
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;
            float half = r / 2f;

            Color light = Color.FromArgb(255, 250, 250, 250);
            Color dark = Color.FromArgb(255, 26, 27, 31);

            using (SolidBrush bg = new SolidBrush(light))
            {
                g.FillEllipse(bg, cx - r, cy - r, d, d);
            }
            using (SolidBrush black = new SolidBrush(dark))
            {
                g.FillPie(black, cx - r, cy - r, d, d, -90f, 180f);
                g.FillEllipse(black, cx - half, cy - r, r, r);
            }
            using (SolidBrush white = new SolidBrush(light))
            {
                g.FillEllipse(white, cx - half, cy, r, r);
            }
            float eye = r / 3.4f;
            using (SolidBrush white = new SolidBrush(light))
            {
                g.FillEllipse(white, cx - eye / 2f, cy - half - eye / 2f, eye, eye);
            }
            using (SolidBrush black = new SolidBrush(dark))
            {
                g.FillEllipse(black, cx - eye / 2f, cy + half - eye / 2f, eye, eye);
            }
            using (Pen rim = new Pen(Color.FromArgb(120, 0, 0, 0), Math.Max(1f, d * 0.018f)))
            {
                g.DrawEllipse(rim, cx - r, cy - r, d, d);
            }
        }

        public static Color UsageColor(float percent)
        {
            if (percent < 70f) return Color.FromArgb(255, 86, 196, 120);
            if (percent < 86f) return Color.FromArgb(255, 240, 186, 74);
            return Color.FromArgb(255, 232, 92, 88);
        }
    }

    // ───────────────────────────── 先天八卦 ─────────────────────────────
    internal static class Bagua
    {
        internal sealed class Gua
        {
            public string Name;    // 乾
            public string Symbol;  // ☰
            public string Image;   // 天
            public string Dir;     // 南（按"上南下北"的先天方位）
            public int[] Yao;      // [0]=上爻 [1]=中爻 [2]=初爻，1=阳 0=阴
            public string Omen;    // 乾为天 · 刚健中正
        }

        internal static Color Gold = Color.FromArgb(255, 226, 190, 110);

        // 先天（伏羲）八卦：乾南居上、坤北居下；自正上方顺时针为 乾→巽→坎→艮→坤→震→离→兑
        internal static readonly Gua[] Order =
        {
            new Gua { Name = "乾", Symbol = "☰", Image = "天", Dir = "南",   Yao = new int[] { 1, 1, 1 }, Omen = "乾为天 · 刚健中正" },
            new Gua { Name = "巽", Symbol = "☴", Image = "风", Dir = "西南", Yao = new int[] { 1, 1, 0 }, Omen = "巽为风 · 顺时而入" },
            new Gua { Name = "坎", Symbol = "☵", Image = "水", Dir = "西",   Yao = new int[] { 0, 1, 0 }, Omen = "坎为水 · 行险不失" },
            new Gua { Name = "艮", Symbol = "☶", Image = "山", Dir = "西北", Yao = new int[] { 1, 0, 0 }, Omen = "艮为山 · 止其所止" },
            new Gua { Name = "坤", Symbol = "☷", Image = "地", Dir = "北",   Yao = new int[] { 0, 0, 0 }, Omen = "坤为地 · 厚德载物" },
            new Gua { Name = "震", Symbol = "☳", Image = "雷", Dir = "东北", Yao = new int[] { 0, 0, 1 }, Omen = "震为雷 · 动而奋起" },
            new Gua { Name = "离", Symbol = "☲", Image = "火", Dir = "东",   Yao = new int[] { 1, 0, 1 }, Omen = "离为火 · 明而附丽" },
            new Gua { Name = "兑", Symbol = "☱", Image = "泽", Dir = "东南", Yao = new int[] { 0, 1, 1 }, Omen = "兑为泽 · 悦而相说" }
        };

        /// <summary>画八卦环。ringRadius 为卦象中心半径，spin 为环自转角（负值即与太极反向）。</summary>
        public static void Draw(Graphics g, float cx, float cy, float ringRadius, float usage, float alpha,
                                float spin, float[] hover, int hoverIndex, float goldMix)
        {
            if (alpha <= 0.02f) return;

            float scaleBase = ringRadius / BallForm.BaguaMid;
            float thick = Math.Max(1.4f, ringRadius * 0.082f);
            float barLen = ringRadius * 0.50f;
            float gap = ringRadius * 0.152f;
            float litUnits = usage / 12.5f;

            for (int i = 0; i < 8; i++)
            {
                float lit = litUnits - i;
                if (lit < 0f) lit = 0f;
                if (lit > 1f) lit = 1f;

                float hov = hover == null ? 0f : hover[i];
                if (hov < 0f) hov = 0f;
                if (hov > 1f) hov = 1f;

                Color color;
                int a;
                if (lit > 0.02f)
                {
                    color = Taiji.UsageColor(usage);
                    a = (int)(70 + 185 * lit);
                }
                else
                {
                    color = Color.FromArgb(120, 132, 142);
                    a = 92;
                }
                float gm = hov > goldMix ? hov : goldMix;
                if (gm > 0.01f)
                {
                    color = Blend(color, Gold, gm);
                    a = (int)(a + (255 - a) * gm);
                }
                a = (int)(a * alpha);
                if (a <= 2) continue;

                float scale = 1f + 0.34f * hov;

                GraphicsState st = g.Save();
                g.TranslateTransform(cx, cy);
                g.RotateTransform(i * 45f + spin);
                g.TranslateTransform(0f, -ringRadius);
                g.ScaleTransform(scale, scale);

                using (Pen pen = new Pen(Color.FromArgb(a, color.R, color.G, color.B), thick / scale))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    for (int k = 0; k < 3; k++)
                    {
                        float y = (k - 1) * gap;
                        if (Order[i].Yao[k] == 1)
                        {
                            g.DrawLine(pen, -barLen / 2f, y, barLen / 2f, y);
                        }
                        else
                        {
                            g.DrawLine(pen, -barLen / 2f, y, -thick * 0.85f, y);
                            g.DrawLine(pen, thick * 0.85f, y, barLen / 2f, y);
                        }
                    }
                }
                g.Restore(st);
            }
        }

        internal static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }
    // ───────────────────────── 画面合成 ─────────────────────────
    internal static class BallRenderer
    {
        public static Bitmap Render(int w, int h, float angle, float usage, float expand, string text,
                                    bool showBagua, float ringSpin, float[] hover, int hoverIndex,
                                    float goldMix, float baguaOut, bool fillUp)
        {
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                float e = expand;
                if (e < 0f) e = 0f;
                if (e > 1f) e = 1f;

                float cx = w / 2f;
                float cy = h / 2f - 6f * e;

                // 圆形 <-> 竖向胶囊：非等比拉伸太极位图完成形变
                float rxCol = (w - 1f) / 2f;
                float ryCol = (h - 1f) / 2f;
                float rx = rxCol + (BallForm.TaijiR - rxCol) * e;
                float ry = ryCol + (BallForm.TaijiR - ryCol) * e;

                int src = (int)Math.Ceiling(2f * Math.Max(rx, ry));
                if (src < 12) src = 12;
                using (Bitmap tj = Taiji.Render(src, angle))
                {
                    g.DrawImage(tj, new RectangleF(cx - rx, cy - ry, rx * 2f, ry * 2f));
                }

                // 收起方向：胶囊以“盖一层”的方式淡入；黑白占比 = 内存占用
                float capAlpha = 1f - Smooth(e, 0.02f, 0.55f);
                if (capAlpha > 0.01f) DrawCapsule(g, w, h, cy, capAlpha, usage, fillUp);

                float baguaAlpha = showBagua ? Smooth(e, 0.45f, 0.9f) : 0f;
                if (baguaOut > 0f) baguaAlpha *= (1f - baguaOut);
                if (baguaAlpha > 0f)
                {
                    float radius = BallForm.BaguaMid * (0.72f + 0.28f * e);
                    Bagua.Draw(g, cx, cy, radius, usage, baguaAlpha, ringSpin, hover, hoverIndex, goldMix);
                }

                // 优化时八卦全部点亮成金色，占用改由中间临时出现的细环表示
                if (goldMix > 0.01f)
                {
                    float ringR = (BallForm.TaijiR + BallForm.BaguaMid - 5f) / 2f;
                    DrawUsageRing(g, cx, cy, ringR, usage, goldMix * (1f - baguaOut));
                }

                float textAlpha = Smooth(e, 0.55f, 0.95f);
                DrawInfo(g, text, cx, cy + BallForm.BaguaR + 7f * e, textAlpha);
            }
            return bmp;
        }

        private static void DrawCapsule(Graphics g, int w, int h, float cy, float alpha, float usage, bool fillUp)
        {
            float inset = 0.5f;
            RectangleF r = new RectangleF(inset, cy - (h - 1f) / 2f, w - inset * 2f, h - 1f);
            float radius = Math.Min(r.Width, r.Height) / 2f;
            if (usage < 0f) usage = 0f;
            if (usage > 100f) usage = 100f;
            float darkH = r.Height * (usage / 100f);

            using (GraphicsPath path = Round(r, radius))
            {
                using (SolidBrush white = new SolidBrush(Color.FromArgb((int)(255 * alpha), 250, 250, 250)))
                {
                    g.FillPath(white, path);
                }
                if (darkH > 0.6f)
                {
                    GraphicsState st = g.Save();
                    g.SetClip(path);
                    using (SolidBrush dark = new SolidBrush(Color.FromArgb((int)(255 * alpha), 26, 27, 31)))
                    {
                        float fy = fillUp ? r.Bottom - darkH : r.Y;
                        g.FillRectangle(dark, r.X, fy, r.Width, darkH);
                    }
                    g.Restore(st);
                }
            }
        }

        private static void DrawUsageRing(Graphics g, float cx, float cy, float radius, float usage, float alpha)
        {
            if (alpha <= 0.02f) return;
            float thick = 2.2f;
            using (Pen track = new Pen(Color.FromArgb((int)(60 * alpha), 214, 220, 232), thick))
            {
                g.DrawEllipse(track, cx - radius, cy - radius, radius * 2f, radius * 2f);
            }
            float sweep = 3.6f * usage;
            if (sweep > 359f) sweep = 359f;
            if (sweep > 0.5f)
            {
                Color col = Taiji.UsageColor(usage);
                using (Pen arc = new Pen(Color.FromArgb((int)(240 * alpha), col.R, col.G, col.B), thick))
                {
                    arc.StartCap = LineCap.Round;
                    arc.EndCap = LineCap.Round;
                    g.DrawArc(arc, cx - radius, cy - radius, radius * 2f, radius * 2f, -90f, sweep);
                }
                double head = (-90.0 + sweep) * Math.PI / 180.0;
                float hx = cx + (float)Math.Cos(head) * radius;
                float hy = cy + (float)Math.Sin(head) * radius;
                using (SolidBrush glow = new SolidBrush(Color.FromArgb((int)(95 * alpha), col.R, col.G, col.B)))
                {
                    g.FillEllipse(glow, hx - 4.6f, hy - 4.6f, 9.2f, 9.2f);
                }
                using (SolidBrush core = new SolidBrush(Color.FromArgb((int)(235 * alpha), 255, 255, 255)))
                {
                    g.FillEllipse(core, hx - 1.6f, hy - 1.6f, 3.2f, 3.2f);
                }
            }
        }

        /// <summary>把八卦位置放射出的金色流光汇聚到 target（局部坐标）。</summary>
        public static void DrawFlow(Graphics g, float cx, float cy, float radius, float spin,
                                    PointF target, float progress, float alpha)
        {
            if (alpha <= 0.01f || progress <= 0f) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            for (int i = 0; i < 8; i++)
            {
                float p = (progress - i * 0.035f) / 0.75f;
                if (p <= 0f) continue;
                if (p > 1f) p = 1f;

                double ang = (-90.0 + i * 45.0 + spin) * Math.PI / 180.0;
                PointF s = new PointF(cx + (float)Math.Cos(ang) * radius,
                                      cy + (float)Math.Sin(ang) * radius);

                float mx = (s.X + target.X) / 2f;
                float my = (s.Y + target.Y) / 2f;
                float dx = target.X - s.X;
                float dy = target.Y - s.Y;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 1f) len = 1f;
                PointF ctrl = new PointF(mx - dy / len * 50f, my + dx / len * 50f);

                const int seg = 12;
                const float tail = 0.45f;
                for (int k = 0; k < seg; k++)
                {
                    float t1 = p - tail * k / seg;
                    float t0 = p - tail * (k + 1) / seg;
                    if (t1 <= 0f) break;
                    if (t0 < 0f) t0 = 0f;
                    PointF a = Bez(s, ctrl, target, t0);
                    PointF b = Bez(s, ctrl, target, t1);
                    int al = (int)(215 * (1f - (float)k / seg) * (1f - p * 0.45f) * alpha);
                    if (al < 3) continue;
                    float wdt = 2.8f * (1f - (float)k / seg) + 0.7f;
                    using (Pen pen = new Pen(Color.FromArgb(al, 238, 208, 134), wdt))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, a, b);
                    }
                }
                if (p < 1f)
                {
                    PointF head = Bez(s, ctrl, target, p);
                    int ga = (int)(90 * (1f - p) * alpha);
                    int ca = (int)(245 * (1f - p * 0.6f) * alpha);
                    using (SolidBrush glow = new SolidBrush(Color.FromArgb(ga, 238, 208, 134)))
                    {
                        g.FillEllipse(glow, head.X - 6f, head.Y - 6f, 12f, 12f);
                    }
                    using (SolidBrush core = new SolidBrush(Color.FromArgb(ca, 255, 246, 216)))
                    {
                        g.FillEllipse(core, head.X - 2.1f, head.Y - 2.1f, 4.2f, 4.2f);
                    }
                }
            }
        }

        private static PointF Bez(PointF p0, PointF p1, PointF p2, float t)
        {
            float u = 1f - t;
            return new PointF(u * u * p0.X + 2f * u * t * p1.X + t * t * p2.X,
                              u * u * p0.Y + 2f * u * t * p1.Y + t * t * p2.Y);
        }

        private static void DrawInfo(Graphics g, string text, float cx, float y, float alpha)
        {
            if (alpha <= 0.02f || string.IsNullOrEmpty(text)) return;

            using (Font f = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold, GraphicsUnit.Point))
            {
                SizeF sz = g.MeasureString(text, f);
                float bw = sz.Width + 14f;
                float bh = 18f;
                RectangleF r = new RectangleF(cx - bw / 2f, y, bw, bh);

                using (GraphicsPath path = Round(r, 9f))
                using (SolidBrush back = new SolidBrush(Color.FromArgb((int)(118 * alpha), 16, 17, 20)))
                {
                    g.FillPath(back, path);
                }
                using (StringFormat sf = new StringFormat())
                using (SolidBrush brush = new SolidBrush(Color.FromArgb((int)(240 * alpha), 246, 247, 250)))
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(text, f, brush, r, sf);
                }
            }
        }

        private static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        internal static float Smooth(float v, float a, float b)
        {
            if (v <= a) return 0f;
            if (v >= b) return 1f;
            float t = (v - a) / (b - a);
            return t * t * (3f - 2f * t);
        }
    }

    // ───────────────────────── 分层窗口（逐像素透明） ─────────────────────────
    internal static class LayeredWindow
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors;
        }

        private const int ULW_ALPHA = 0x02;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr h);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi,
            uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, IntPtr count);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        public static int LastError;

        /// <summary>贴图，同时按位图尺寸移动/改变窗口（形变动画不必改 Bounds）。</summary>
        public static bool Update(IntPtr hwnd, Bitmap bitmap, int x, int y)
        {
            if (hwnd == IntPtr.Zero) return false;
            int w = bitmap.Width;
            int h = bitmap.Height;
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero;
            IntPtr old = IntPtr.Zero;
            try
            {
                BITMAPINFO bi = new BITMAPINFO();
                bi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bi.bmiHeader.biWidth = w;
                bi.bmiHeader.biHeight = -h;
                bi.bmiHeader.biPlanes = 1;
                bi.bmiHeader.biBitCount = 32;
                bi.bmiHeader.biCompression = 0;

                IntPtr bits;
                hBmp = CreateDIBSection(memDc, ref bi, 0, out bits, IntPtr.Zero, 0);
                if (hBmp == IntPtr.Zero) return false;

                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, w, h),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    for (int row = 0; row < h; row++)
                    {
                        CopyMemory(new IntPtr(bits.ToInt64() + (long)row * w * 4),
                            new IntPtr(data.Scan0.ToInt64() + (long)row * data.Stride),
                            new IntPtr(w * 4));
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                old = SelectObject(memDc, hBmp);
                SIZE size = new SIZE();
                size.cx = w;
                size.cy = h;
                POINT src = new POINT();
                POINT dst = new POINT();
                dst.X = x;
                dst.Y = y;
                BLENDFUNCTION bf = new BLENDFUNCTION();
                bf.BlendOp = AC_SRC_OVER;
                bf.BlendFlags = 0;
                bf.SourceConstantAlpha = 255;
                bf.AlphaFormat = AC_SRC_ALPHA;
                bool ok = UpdateLayeredWindow(hwnd, screenDc, ref dst, ref size,
                    memDc, ref src, 0, ref bf, ULW_ALPHA);
                LastError = Marshal.GetLastWin32Error();
                return ok;
            }
            finally
            {
                if (old != IntPtr.Zero) SelectObject(memDc, old);
                if (hBmp != IntPtr.Zero) DeleteObject(hBmp);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }

    // ───────────────────────────── 悬浮球窗口 ─────────────────────────────
    internal sealed class BallForm : Form
    {
        internal const int CapsW = 12;
        internal const int CapsH = 48;
        internal const int PanelW = 96;
        internal const int PanelH = 120;
        internal const float TaijiR = 19f;
        internal const float BaguaR = 38f;      // 卦象最外圈
        internal const float BaguaMid = 33f;    // 卦象中心半径

        private readonly ContextMenuStrip _menu;
        private readonly Timer _menuWatch;
        private readonly Timer _tick;
        private readonly Timer _usageTimer;
        private readonly Timer _leaveTimer;
        private readonly AppConfig _config;

        private float _expand;
        private bool _wantExpand;
        private float _angle;
        private float _speed;
        private float _usage;
        private int _usageTarget = -1;
        private bool _busy;
        private bool _edgeRight = true;
        private int _anchorY;
        private string _text = "";
        private string _resultText = "";
        private DateTime _resultUntil = DateTime.MinValue;
        private DateTime _lastFrame = DateTime.MinValue;

        private bool _dragging;
        private bool _moved;
        private Point _dragCursor;
        private int _dragAnchorY;

        private int _winX;
        private int _winY;
        private int _winW = CapsW;
        private int _winH = CapsH;

        private float _baguaAngle;
        private readonly float[] _hover = new float[8];
        private int _hoverIndex = -1;
        private bool _mouseDown;
        private int _pressIndex = -1;
        private DateTime _pressAt;
        private bool _longPressFired;
        private AlmanacForm _almanac;


        private bool _usageReady;
        private bool _menuOpen;
        private float _goldMix;
        private float _fx = -1f;
        private Point _fxTarget;
        private Action _fxAfter;

        public BallForm()
        {
            _config = AppConfig.Load();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(CapsW, CapsH);
            BackColor = Color.Black;
            Text = "太极内存球";

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            _anchorY = wa.Bottom - 150;
            Location = new Point(wa.Right - CapsW - 2, _anchorY - CapsH / 2);
            _winX = Location.X;
            _winY = Location.Y;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("立即优化内存", null, delegate { StartOptimize(); });
            menu.Items.Add("今日黄历", null, delegate { ShowAlmanac(_hoverIndex >= 0 ? _hoverIndex : 0); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("设置…", null, delegate { OpenSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Close(); });
            _menu = menu;
            menu.Renderer = new InkMenuRenderer();
            menu.BackColor = Ink.Bg;
            menu.ForeColor = Ink.Ivory;
            menu.Font = Ink.F(9.5f, FontStyle.Regular);
            menu.ShowImageMargin = false;
            menu.DropShadowEnabled = false;
            menu.Padding = new Padding(2, 4, 2, 4);
            menu.Opened += delegate
            {
                _wantExpand = true;
                _leaveTimer.Stop();
                _menuOpen = true;





            };

            _usageTimer = new Timer();
            _usageTimer.Interval = 1000;
            _usageTimer.Tick += delegate { RefreshUsage(); };
            _usageTimer.Start();

            _leaveTimer = new Timer();
            _leaveTimer.Interval = 700;
            _leaveTimer.Tick += delegate
            {
                _leaveTimer.Stop();
                if (_busy) return;
                if (_menuOpen) return;
                if (IsCursorInside()) return;
                _wantExpand = false;
            };

            menu.Closed += delegate { _menuOpen = false; };

            // 菜单关闭后恢复动画（轮询兜底，防止 Closed 事件不触发）
            _menuWatch = new Timer();
            _menuWatch.Interval = 250;
            _menuWatch.Tick += delegate
            {
                if (_menu != null && _menu.Visible) return;
                _menuWatch.Stop();
                _menuOpen = false;
                if (!_tick.Enabled) _tick.Start();
            };

            _tick = new Timer();
            _tick.Interval = 16;
            _tick.Tick += delegate { OnFrame(); };
            _tick.Start();

            RefreshUsage();
            _text = ((int)Math.Round(_usage)) + "%";


        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }


        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter,
            int x, int y, int cx, int cy, uint flags);

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Render();
            SetWindowPos(Handle, new IntPtr(-1), _winX, _winY, _winW, _winH, 0x0010);


        }

        // ── 状态 ──────────────────────────────────────────────
        private void RefreshUsage()
        {
            long total, avail;
            int pct;
            MemoryOptimizer.GetStatus(out total, out avail, out pct);
            if (pct == _usageTarget) return;
            _usageTarget = pct;
            if (!_usageReady)
            {
                _usage = pct;   // 只在启动时对齐一次，之后一律缓动
                _usageReady = true;
            }
        }

        private void UpdateText(DateTime now)
        {
            if (_busy) _text = "优化中";
            else if (now < _resultUntil) _text = _resultText;
            else _text = ((int)Math.Round(_usage)) + "%";
        }

        // ── 每帧 ──────────────────────────────────────────────
        private void OnFrame()
        {
            DateTime now = DateTime.Now;
            float dt = _lastFrame == DateTime.MinValue ? 0.016f : (float)(now - _lastFrame).TotalSeconds;
            _lastFrame = now;
            if (dt <= 0f) return;
            if (dt > 0.25f) dt = 0.25f;

            // 流光：先把八卦化成光汇聚过去，结束再打开页面
            if (_fx >= 0f)
            {
                _fx += dt / 0.26f;
                if (_fx >= 1f)
                {
                    _fx = -1f;
                    Action act = _fxAfter;
                    _fxAfter = null;
                    if (act != null)
                    {
                        try { act(); }
                        catch { }
                    }
                }
            }

            // 右键菜单打开期间不重绘/不改窗口尺寸，否则会把菜单挤掉
            if (_menuOpen) return;

            // 展开 / 收起
            float target = _wantExpand ? 1f : 0f;
            if (Math.Abs(_expand - target) > 0.001f)
            {
                float k = 1f - (float)Math.Exp(-dt * 11f);
                _expand += (target - _expand) * k;
                if (Math.Abs(_expand - target) < 0.004f) _expand = target;
            }

            // 旋转：优化中正向快转；空闲反向慢转，转速随占用上升
            float spinBase = _busy ? 300f : (3f + _usage * 0.25f);
            float spinSign = _config.SpinCw ? 1f : -1f;
            float wanted = _busy ? -spinSign * spinBase : spinSign * spinBase;
            _speed += (wanted - _speed) * (1f - (float)Math.Exp(-dt * 5f));
            _angle += _speed * dt;
            while (_angle >= 360f) _angle -= 360f;
            while (_angle < 0f) _angle += 360f;

            // 八卦环与太极反向自转：独立积分，避免 _angle 归零时整环跳回
            _baguaAngle += (-_speed * 0.45f) * dt;
            while (_baguaAngle >= 360f) _baguaAngle -= 360f;
            while (_baguaAngle < 0f) _baguaAngle += 360f;
            UpdateHover(dt);
            UpdateLongPress(now);

            // 优化中：八卦整体转金，占用改由中间的临时圆环表示
            float goldTarget = _busy ? 1f : 0f;
            _goldMix += (goldTarget - _goldMix) * (1f - (float)Math.Exp(-dt * 7f));
            if (_goldMix < 0.002f) _goldMix = 0f;


            // 占用缓动
            if (_usageTarget >= 0)
            {
                float diff = _usageTarget - _usage;
                if (Math.Abs(diff) > 0.03f) _usage += diff * (1f - (float)Math.Exp(-dt * 4.2f));
                else _usage = _usageTarget;
            }

            UpdateText(now);
            Render();
        }

        private void Render()
        {
            int w = (int)Math.Round(CapsW + (PanelW - CapsW) * _expand);
            int h = (int)Math.Round(CapsH + (PanelH - CapsH) * _expand);
            if (w < 4) w = 4;
            if (h < 8) h = 8;

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            // 贴边距固定，保证展开后鼠标仍落在窗口内（否则光标在边缘时展开会立刻收起）
            float inset = 2f;
            int x = _edgeRight ? (int)Math.Round(wa.Right - w - inset)
                               : (int)Math.Round(wa.Left + inset);
            int y = _anchorY - h / 2;
            if (y < wa.Top + 2) y = wa.Top + 2;
            if (y + h > wa.Bottom - 2) y = wa.Bottom - 2 - h;

            if (_fx >= 0f)
            {
                _winX = x;
                _winY = y;
                _winW = w;
                _winH = h;
                RenderFlowFrame(w, h, x, y);
                return;
            }

            _winX = x;
            _winY = y;
            _winW = w;
            _winH = h;

            using (Bitmap bmp = BallRenderer.Render(w, h, _angle, _usage, _expand, _text, _config.ShowUsage,
                                                    _baguaAngle, _hover, _hoverIndex, _goldMix, 0f, _config.FillUp))
            {
                LayeredWindow.Update(Handle, bmp, x, y);
            }
        }

        /// <summary>根据光标相对球心的角度算出悬停在哪个卦上，并缓动放大系数。</summary>
        private void UpdateHover(float dt)
        {
            int idx = -1;
            if (_expand > 0.55f)
            {
                Point p = Cursor.Position;
                float cx = _winX + _winW / 2f;
                float cy = _winY + _winH / 2f - 6f * _expand;
                float dx = p.X - cx;
                float dy = p.Y - cy;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist > TaijiR * 0.8f && dist < BaguaR + 12f)
                {
                    double deg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    double rel = deg + 90.0 - _baguaAngle;
                    while (rel < 0.0) rel += 360.0;
                    while (rel >= 360.0) rel -= 360.0;
                    idx = (int)Math.Round(rel / 45.0) % 8;
                }
            }
            _hoverIndex = idx;

            float k = 1f - (float)Math.Exp(-dt * 14f);
            for (int i = 0; i < 8; i++)
            {
                float target = (i == _hoverIndex) ? 1f : 0f;
                _hover[i] += (target - _hover[i]) * k;
                if (Math.Abs(_hover[i] - target) < 0.004f) _hover[i] = target;
            }
        }

        private void UpdateLongPress(DateTime now)
        {
            if (!_mouseDown || _longPressFired) return;
            if (_moved) return;
            if ((now - _pressAt).TotalMilliseconds < 650) return;
            if (!IsCursorInside()) return;
            _longPressFired = true;
            ShowAlmanac(_hoverIndex >= 0 ? _hoverIndex : 0);
        }

        private void ShowAlmanac(int guaIndex)
        {
            if (_almanac != null && !_almanac.IsDisposed) _almanac.Close();
            _almanac = new AlmanacForm(guaIndex, new Rectangle(_winX, _winY, _winW, _winH));
            Point at = new Point(_almanac.Left + 32, _almanac.Top + 26);
            BeginFx(at, delegate { if (_almanac != null && !_almanac.IsDisposed) _almanac.Show(); });
        }

        /// <summary>流光只在球自己的窗口里画，不放大窗口，避免惊动正在弹出的菜单。</summary>
        private void RenderFlowFrame(int w, int h, int x, int y)
        {
            float fade = BallRenderer.Smooth(_fx, 0f, 0.4f);
            float glow = 1f - BallRenderer.Smooth(_fx, 0.72f, 1f);

            using (Bitmap bmp = BallRenderer.Render(w, h, _angle, _usage, _expand, _text, _config.ShowUsage,
                                                    _baguaAngle, _hover, _hoverIndex, _goldMix, fade, _config.FillUp))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;

                    float ccx = w / 2f;
                    float ccy = h / 2f - 6f * _expand;

                    float tx = _fxTarget.X - x;
                    float ty = _fxTarget.Y - y;
                    if (tx < 6f) tx = 6f;
                    if (ty < 6f) ty = 6f;
                    if (tx > w - 6f) tx = w - 6f;
                    if (ty > h - 6f) ty = h - 6f;

                    BallRenderer.DrawFlow(g, ccx, ccy, BaguaMid * (0.72f + 0.28f * _expand), _baguaAngle,
                                          new PointF(tx, ty), _fx, glow);
                }
                LayeredWindow.Update(Handle, bmp, x, y);
            }
        }
        private void BeginFx(Point target, Action after)
        {
            _fxTarget = target;
            _fxAfter = after;
            _fx = 0f;
        }

        private bool IsCursorInside()
        {
            Point p = Cursor.Position;
            return p.X >= _winX && p.X < _winX + _winW && p.Y >= _winY && p.Y < _winY + _winH;
        }

        // ── 鼠标 ──────────────────────────────────────────────
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _wantExpand = true;
            _leaveTimer.Stop();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _leaveTimer.Stop();
            if (!_busy) _leaveTimer.Start();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _moved = false;
                _dragCursor = Cursor.Position;
                _dragAnchorY = _anchorY;
                _mouseDown = true;
                _pressIndex = _hoverIndex;
                _pressAt = DateTime.Now;
                _longPressFired = false;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                Point p = Cursor.Position;
                if (!_moved && Math.Abs(p.Y - _dragCursor.Y) > 3) _moved = true;
                if (_moved)
                {
                    Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                    _anchorY = _dragAnchorY + (p.Y - _dragCursor.Y);
                    if (_anchorY < wa.Top + 40) _anchorY = wa.Top + 40;
                    if (_anchorY > wa.Bottom - 40) _anchorY = wa.Bottom - 40;
                    _edgeRight = p.X > wa.Left + wa.Width / 2;
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _mouseDown = false;
            if (e.Button == MouseButtons.Right)
            {
                ShowMyMenu(Cursor.Position);
                base.OnMouseUp(e);
                return;
            }
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
                if (!_moved && !_longPressFired)
                {
                    if (_expand < 0.9f) _wantExpand = true;
                    else StartOptimize();
                }
            }
            _longPressFired = false;
            base.OnMouseUp(e);
        }

        // ── 动作 ──────────────────────────────────────────────
        private void StartOptimize()
        {
            if (_busy) return;
            _busy = true;
            _wantExpand = true;
            _leaveTimer.Stop();
            _resultUntil = DateTime.MinValue;
            _text = "优化中";

            Thread t = new Thread(OptimizeWorker);
            t.IsBackground = true;
            t.Name = "mem-optimize";
            t.Start();
        }

        private void OptimizeWorker()
        {
            long freed;
            string log;
            int skipped;
            bool ok = MemoryOptimizer.IsElevated()
                ? MemoryOptimizer.OptimizeHere(out freed, out log, out skipped)
                : MemoryOptimizer.RequestElevatedOptimize(out freed, out log, out skipped);

            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    _busy = false;
                    if (!ok) _resultText = "未执行";
                    else if (freed < 1048576L) _resultText = "无变化";
                    else _resultText = "释放 " + MemoryOptimizer.FormatBytes(freed);
                    _resultUntil = DateTime.Now.AddSeconds(4);
                    RefreshUsage();
                    _leaveTimer.Stop();
                    _leaveTimer.Start();
                });
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        /// <summary>右键：先播流光，光汇聚到位置后再弹菜单。</summary>
        private void ShowMyMenu(Point screen)
        {
            if (_menu == null) return;
            Point at = screen;
            BeginFx(at, delegate { OpenMenuAt(at); });
        }

        /// <summary>弹出菜单前先停掉重绘并抢前台，否则菜单位置会被 ULW 不断重置而闪退。</summary>
        private void OpenMenuAt(Point screen)
        {
            _menuOpen = true;
            _tick.Stop();
            _leaveTimer.Stop();
            try
            {
                Activate();
                SetForegroundWindow(Handle);
                _menu.Show(this, PointToClient(screen));
            }
            catch
            {
                _menuOpen = false;
                _tick.Start();
                return;
            }
            _menuWatch.Start();
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private void OpenSettings()
        {
            // 不能在 using 里创建：流光播完才弹窗，那时 using 早已把窗体释放
            SettingsForm dlg = new SettingsForm(_config);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            dlg.StartPosition = FormStartPosition.Manual;
            dlg.Location = new Point(wa.Left + (wa.Width - dlg.Width) / 2,
                                     wa.Top + (wa.Height - dlg.Height) / 2);

            Point at = new Point(dlg.Left + 40, dlg.Top + 26);
            BeginFx(at, delegate
            {
                try
                {
                    if (IsDisposed) return;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _config.ShowUsage = dlg.Result.ShowUsage;
                        _config.Save();
                    }
                }
                finally
                {
                    dlg.Dispose();
                    if (!IsDisposed) Render();
                }
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _tick.Stop();
            _usageTimer.Stop();
            _leaveTimer.Stop();
            base.OnFormClosing(e);
        }
    }

    // ───────────────────────────── 中国风 ─────────────────────────────
    internal static class Ink
    {
        internal static readonly Color Bg = Color.FromArgb(22, 24, 29);
        internal static readonly Color Bg2 = Color.FromArgb(30, 33, 40);
        internal static readonly Color Gold = Color.FromArgb(226, 190, 110);
        internal static readonly Color GoldDim = Color.FromArgb(110, 226, 190, 110);
        internal static readonly Color GoldFaint = Color.FromArgb(46, 226, 190, 110);
        internal static readonly Color Ivory = Color.FromArgb(239, 231, 214);
        internal static readonly Color Muted = Color.FromArgb(150, 156, 168);
        internal static readonly Color Cinnabar = Color.FromArgb(192, 80, 63);

        internal static Font F(float size, FontStyle style)
        {
            try { return new Font("KaiTi", size, style, GraphicsUnit.Point); }
            catch
            {
                try { return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point); }
                catch { return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point); }
            }
        }

        internal static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ───────────────────────── 右键菜单（墨金） ─────────────────────────
    internal sealed class InkMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            try
            {
                int w = e.ToolStrip.Width;
                int h = e.ToolStrip.Height;
                if (w <= 0 || h <= 0) return;
                using (SolidBrush b = new SolidBrush(Ink.Bg))
                {
                    e.Graphics.FillRectangle(b, new Rectangle(0, 0, w, h));
                }
            }
            catch { }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            try
            {
                int w = e.ToolStrip.Width;
                int h = e.ToolStrip.Height;
                if (w <= 1 || h <= 1) return;
                using (Pen p = new Pen(Ink.GoldDim, 1f))
                {
                    e.Graphics.DrawRectangle(p, 0.5f, 0.5f, w - 1f, h - 1f);
                }
            }
            catch { }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            try
            {
                if (!e.Item.Selected) return;
                Rectangle r = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                using (SolidBrush b = new SolidBrush(Ink.GoldFaint))
                {
                    e.Graphics.FillRectangle(b, r);
                }
                using (SolidBrush bar = new SolidBrush(Ink.Gold))
                {
                    e.Graphics.FillRectangle(bar, 2, 1, 3, e.Item.Height - 2);
                }
            }
            catch { }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            try
            {
                e.TextColor = e.Item.Selected ? Ink.Gold : Ink.Ivory;
                base.OnRenderItemText(e);
            }
            catch { }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            try
            {
                int y = e.Item.Height / 2;
                using (Pen p = new Pen(Ink.GoldFaint, 1f))
                {
                    e.Graphics.DrawLine(p, 10, y, e.Item.Width - 10, y);
                }
            }
            catch { }
        }
    }
    // ───────────────────────── 自绘勾选框 ─────────────────────────
    internal sealed class InkCheck : Control
    {
        private bool _checked;

        public InkCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 24;
            Cursor = Cursors.Hand;
            Font = Ink.F(9f, FontStyle.Regular);
            ForeColor = Ink.Ivory;
        }

        public bool Checked
        {
            get { return _checked; }
            set { if (_checked == value) return; _checked = value; Invalidate(); }
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle box = new Rectangle(0, (Height - 16) / 2, 16, 16);
            using (GraphicsPath p = Ink.Round(box, 3f))
            {
                using (SolidBrush fill = new SolidBrush(_checked ? Ink.Gold : Color.FromArgb(38, 42, 50)))
                {
                    g.FillPath(fill, p);
                }
                using (Pen pen = new Pen(_checked ? Ink.Gold : Ink.GoldDim, 1f))
                {
                    g.DrawPath(pen, p);
                }
            }
            if (_checked)
            {
                using (Pen tick = new Pen(Ink.Bg, 2f))
                {
                    tick.StartCap = LineCap.Round;
                    tick.EndCap = LineCap.Round;
                    g.DrawLines(tick, new PointF[]
                    {
                        new PointF(box.X + 4f, box.Y + 8f),
                        new PointF(box.X + 7f, box.Y + 11.5f),
                        new PointF(box.X + 12.5f, box.Y + 4.5f)
                    });
                }
            }
            using (SolidBrush text = new SolidBrush(ForeColor))
            {
                g.DrawString(Text, Font, text, 24f, (Height - Font.Height) / 2f);
            }
        }
    }

    // ───────────────────────── 黄历 / 运气 ─────────────────────────
    internal static class Almanac
    {
        private const string Gan = "甲乙丙丁戊己庚辛壬癸";
        private const string Zhi = "子丑寅卯辰巳午未申酉戌亥";

        private static readonly string[] YiPool =
        {
            "会友", "出行", "沐浴", "扫舍", "读书", "静坐", "品茶", "栽种",
            "纳财", "开市", "祈福", "整理", "散步", "早睡", "写字", "煮茶"
        };

        private static readonly string[] JiPool =
        {
            "争讼", "熬夜", "远行", "动土", "大额支出", "签长约",
            "贪杯", "与人争执", "久坐", "冲动消费", "深夜刷屏", "硬扛"
        };

        /// <summary>以 1949-10-01（甲子日）为锚点推算日干支。</summary>
        public static string DayGanZhi(DateTime day)
        {
            DateTime epoch = new DateTime(1949, 10, 1);
            int days = (int)(day.Date - epoch.Date).TotalDays;
            int i = ((days % 60) + 60) % 60;
            return Gan[i % 10].ToString() + Zhi[i % 12].ToString() + "日";
        }

        public static int Luck(DateTime day, int gua)
        {
            long seed = (long)day.Year * 10000L + day.Month * 100L + day.Day;
            long x = seed * 6364136223846793005L + 1442695040888963407L;
            x += (gua + 1) * 2654435761L;
            x ^= (x >> 33);
            x *= -49064778989728563L;
            x ^= (x >> 29);
            long v = x % 97L;
            if (v < 0) v = -v;
            return (int)v + 2;
        }

        public static string Grade(int luck)
        {
            if (luck >= 90) return "大吉";
            if (luck >= 75) return "吉";
            if (luck >= 55) return "中平";
            if (luck >= 35) return "小凶";
            return "凶";
        }

        public static string[] Pick(string[] pool, int count, long seed)
        {
            string[] result = new string[count];
            long x = seed;
            for (int i = 0; i < count; i++)
            {
                x = x * 1103515245L + 12345L;
                long v = x % pool.Length;
                if (v < 0) v = -v;
                result[i] = pool[v];
            }
            return result;
        }
    }

    internal sealed class AlmanacForm : Form
    {
        private readonly string _ganzhi;
        private readonly int _luck;
        private readonly string _grade;
        private readonly string[] _yi;
        private readonly string[] _ji;
        private readonly Bagua.Gua _gua;
        private readonly Timer _life;

        public AlmanacForm(int guaIndex, Rectangle ball)
        {
            _gua = Bagua.Order[guaIndex];
            DateTime now = DateTime.Now;
            _ganzhi = Almanac.DayGanZhi(now);
            _luck = Almanac.Luck(now, guaIndex);
            _grade = Almanac.Grade(_luck);

            long seed = (long)now.Year * 10000L + now.Month * 100L + now.Day + guaIndex * 7L;
            _yi = Almanac.Pick(new string[] { "会友", "出行", "沐浴", "扫舍", "读书", "静坐", "品茶",
                                              "栽种", "纳财", "开市", "祈福", "整理", "散步", "早睡" }, 3, seed);
            _ji = Almanac.Pick(new string[] { "争讼", "熬夜", "远行", "动土", "大额支出", "签长约",
                                              "贪杯", "与人争执", "久坐", "冲动消费" }, 2, seed + 31);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(288, 238);
            BackColor = Ink.Bg;
            DoubleBuffered = true;

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int x = ball.X - Width - 10;
            int y = ball.Y + ball.Height / 2 - Height / 2;
            if (x < wa.Left + 6) x = ball.X + ball.Width + 10;
            if (y + Height > wa.Bottom - 6) y = wa.Bottom - Height - 6;
            if (y < wa.Top + 6) y = wa.Top + 6;
            Location = new Point(x, y);

            using (GraphicsPath p = Ink.Round(ClientRectangle, 12f))
            {
                Region = new Region(p);
            }

            _life = new Timer();
            _life.Interval = 9000;
            _life.Tick += delegate
            {
                _life.Stop();
                _life.Dispose();
                Close();
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _life.Start();
        }

        protected override void OnClick(EventArgs e)
        {
            Close();
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (Pen outer = new Pen(Ink.GoldDim, 1f))
            {
                g.DrawRectangle(outer, 0.5f, 0.5f, Width - 1f, Height - 1f);
            }
            using (Pen inner = new Pen(Ink.GoldFaint, 1f))
            {
                g.DrawRectangle(inner, 5.5f, 5.5f, Width - 12f, Height - 12f);
            }

            using (Font f1 = Ink.F(10.5f, FontStyle.Bold))
            using (Font f2 = Ink.F(8.5f, FontStyle.Regular))
            using (Font f3 = Ink.F(22f, FontStyle.Bold))
            using (Font f4 = Ink.F(9f, FontStyle.Bold))
            using (Font f5 = Ink.F(7.5f, FontStyle.Regular))
            using (SolidBrush gold = new SolidBrush(Ink.Gold))
            using (SolidBrush ivory = new SolidBrush(Ink.Ivory))
            using (SolidBrush muted = new SolidBrush(Ink.Muted))
            using (SolidBrush cin = new SolidBrush(Ink.Cinnabar))
            {
                g.DrawString("今日 · " + _ganzhi, f1, gold, 18f, 12f);
                g.DrawString(_gua.Name + " · " + _gua.Image + " · " + _gua.Dir, f2, ivory, 18f, 36f);

                using (Pen line = new Pen(Ink.GoldFaint, 1f))
                {
                    g.DrawLine(line, 18f, 58f, Width - 18f, 58f);
                }

                g.DrawString("运气指数", f2, muted, 18f, 66f);
                g.DrawString(_luck.ToString(), f3, gold, 16f, 80f);
                g.DrawString(_grade, f4, gold, 92f, 96f);

                RectangleF track = new RectangleF(20f, 128f, Width - 40f, 5f);
                using (GraphicsPath tp = Ink.Round(track, 2.5f))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(46, 52, 62)))
                {
                    g.FillPath(tb, tp);
                }
                RectangleF fill = new RectangleF(track.X, track.Y, track.Width * (_luck / 100f), track.Height);
                if (fill.Width > 2f)
                {
                    using (GraphicsPath fp = Ink.Round(fill, 2.5f))
                    using (SolidBrush fb = new SolidBrush(Ink.Gold))
                    {
                        g.FillPath(fb, fp);
                    }
                }

                DrawTag(g, "宜", 18f, 146f);
                g.DrawString(string.Join(" · ", _yi), f2, ivory, 48f, 149f);

                DrawTag(g, "忌", 18f, 176f);
                g.DrawString(string.Join(" · ", _ji), f2, cin, 48f, 179f);

                g.DrawString(_gua.Omen, f2, muted, 18f, 206f);
                g.DrawString("长按八卦查看 · 仅供娱乐", f5, muted, Width - 18f - g.MeasureString("长按八卦查看 · 仅供娱乐", f5).Width, 222f);
            }
        }

        private static void DrawTag(Graphics g, string text, float x, float y)
        {
            RectangleF box = new RectangleF(x, y, 20f, 20f);
            using (GraphicsPath p = Ink.Round(box, 4f))
            using (SolidBrush b = new SolidBrush(Ink.Cinnabar))
            {
                g.FillPath(b, p);
            }
            using (Font f = Ink.F(9f, FontStyle.Bold))
            using (SolidBrush w = new SolidBrush(Color.FromArgb(245, 236, 228)))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, f, w, box, sf);
            }
        }
    }
    // ───────────────────────────── 设置窗口 ─────────────────────────────
    internal sealed class SettingsForm : Form
    {
        private sealed class ProcItem
        {
            public string Name;
            public long Memory;
            public override string ToString()
            {
                return Memory > 0 ? Name + "    " + MemoryOptimizer.FormatBytes(Memory) : Name;
            }
        }

        private readonly InkCheck _showUsage;
        private readonly InkCheck _spinCw;
        private readonly InkCheck _fillUp;
        private readonly CheckedListBox _list;
        private readonly TextBox _input;
        private readonly Font _nameFont = Ink.F(9f, FontStyle.Regular);
        private readonly Font _memFont = Ink.F(8.5f, FontStyle.Regular);

        private bool _dragging;
        private Point _dragOffset;

        public AppConfig Result { get; private set; }

        public SettingsForm(AppConfig cfg)
        {
            Result = cfg;
            Text = "太极 · 设置";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(470, 598);
            BackColor = Ink.Bg;
            DoubleBuffered = true;
            Font = Ink.F(9f, FontStyle.Regular);
            KeyPreview = true;

            _showUsage = new InkCheck();
            _showUsage.Text = "显示八卦环（八象依次点亮，表示内存占用）";
            _showUsage.SetBounds(22, 56, 420, 24);
            _showUsage.Checked = cfg.ShowUsage;
            Controls.Add(_showUsage);

            _spinCw = new InkCheck();
            _spinCw.Text = "空闲时顺时针旋转（优化中自动反向）";
            _spinCw.SetBounds(22, 82, 420, 24);
            _spinCw.Checked = cfg.SpinCw;
            Controls.Add(_spinCw);

            _fillUp = new InkCheck();
            _fillUp.Text = "收起胶囊从下往上填充";
            _fillUp.SetBounds(22, 108, 420, 24);
            _fillUp.Checked = cfg.FillUp;
            Controls.Add(_fillUp);

            _list = new CheckedListBox();
            _list.SetBounds(32, 168, 406, 262);
            _list.CheckOnClick = true;
            _list.BorderStyle = BorderStyle.None;
            _list.BackColor = Ink.Bg2;
            _list.ForeColor = Ink.Ivory;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = 22;
            _list.IntegralHeight = false;
            _list.Font = _nameFont;
            _list.DrawItem += DrawListItem;
            Controls.Add(_list);

            _input = new TextBox();
            _input.SetBounds(32, 438, 282, 24);
            _input.BorderStyle = BorderStyle.FixedSingle;
            _input.BackColor = Color.FromArgb(34, 38, 46);
            _input.ForeColor = Ink.Ivory;
            _input.Font = _nameFont;
            Controls.Add(_input);

            Button add = MakeButton("添 加", 324, 436, 100, 26);
            add.Click += delegate { AddTyped(); };
            Controls.Add(add);

            Button refresh = MakeButton("刷新进程列表", 32, 470, 116, 26);
            refresh.Click += delegate { FillList(); };
            Controls.Add(refresh);

            Button clear = MakeButton("清空排除项", 160, 470, 116, 26);
            clear.Click += delegate { UncheckAll(); };
            Controls.Add(clear);

            Button ok = MakeButton("确 定", 250, 554, 90, 30);
            ok.DialogResult = DialogResult.OK;
            ok.Click += delegate { Accept(); };
            Controls.Add(ok);

            Button cancel = MakeButton("取 消", 350, 554, 90, 30);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            FillList();
        }

        protected override bool ShowWithoutActivation { get { return false; } }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Opacity = 1.0;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        private Button MakeButton(string text, int x, int y, int w, int h)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.FromArgb(30, 33, 40);
            b.ForeColor = Ink.Ivory;
            b.Font = Ink.F(9f, FontStyle.Regular);
            b.FlatAppearance.BorderColor = Ink.GoldDim;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 226, 190, 110);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(80, 226, 190, 110);
            b.Cursor = Cursors.Hand;
            return b;
        }

        // ── 自绘外观 ──────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (Pen outer = new Pen(Ink.GoldDim, 1f))
            {
                g.DrawRectangle(outer, 0.5f, 0.5f, Width - 1f, Height - 1f);
            }
            using (Pen inner = new Pen(Ink.GoldFaint, 1f))
            {
                g.DrawRectangle(inner, 5.5f, 5.5f, Width - 12f, Height - 12f);
            }

            // 标题 + 印章
            using (Font title = Ink.F(11.5f, FontStyle.Bold))
            using (SolidBrush gold = new SolidBrush(Ink.Gold))
            {
                g.DrawString("太 极 · 设 置", title, gold, 20f, 12f);
            }
            Rectangle seal = new Rectangle(Width - 92, 12, 22, 22);
            using (GraphicsPath sp = Ink.Round(seal, 3f))
            using (SolidBrush sb = new SolidBrush(Ink.Cinnabar))
            {
                g.FillPath(sb, sp);
            }
            using (Font sf = Ink.F(9.5f, FontStyle.Bold))
            using (SolidBrush sw = new SolidBrush(Color.FromArgb(245, 236, 228)))
            using (StringFormat sc = new StringFormat())
            {
                sc.Alignment = StringAlignment.Center;
                sc.LineAlignment = StringAlignment.Center;
                g.DrawString("設", sf, sw, seal, sc);
            }

            // 关闭钮
            using (Pen xp = new Pen(Ink.Muted, 1.6f))
            {
                xp.StartCap = LineCap.Round;
                xp.EndCap = LineCap.Round;
                g.DrawLine(xp, Width - 36f, 16f, Width - 24f, 28f);
                g.DrawLine(xp, Width - 24f, 16f, Width - 36f, 28f);
            }

            using (Pen line = new Pen(Ink.GoldFaint, 1f))
            {
                g.DrawLine(line, 20f, 44f, Width - 20f, 44f);
            }

            // 分组框
            Rectangle panel = new Rectangle(16, 140, 438, 400);
            using (GraphicsPath pp = Ink.Round(panel, 6f))
            using (Pen pen = new Pen(Ink.GoldDim, 1f))
            {
                g.DrawPath(pen, pp);
            }
            using (Font gf = Ink.F(9f, FontStyle.Bold))
            using (SolidBrush gb = new SolidBrush(Ink.Gold))
            {
                g.DrawString("排除进程 · 优化时保留其工作集", gf, gb, 30f, 146f);
            }
            using (Font hf = Ink.F(8.5f, FontStyle.Regular))
            using (SolidBrush hb = new SolidBrush(Ink.Muted))
            {
                g.DrawString("按进程名匹配（不含路径），改动在下一次优化时生效。", hf, hb, 32f, 502f);
            }
        }

        private void DrawListItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count) return;
            ProcItem it = _list.Items[e.Index] as ProcItem;
            if (it == null) return;

            Graphics g = e.Graphics;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            bool check = _list.GetItemChecked(e.Index);

            using (SolidBrush bg = new SolidBrush(selected ? Color.FromArgb(44, 226, 190, 110) : Ink.Bg2))
            {
                g.FillRectangle(bg, e.Bounds);
            }

            Rectangle box = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + (e.Bounds.Height - 14) / 2, 14, 14);
            using (GraphicsPath p = Ink.Round(box, 3f))
            {
                using (SolidBrush fill = new SolidBrush(check ? Ink.Gold : Color.FromArgb(38, 42, 50)))
                {
                    g.FillPath(fill, p);
                }
                using (Pen pen = new Pen(check ? Ink.Gold : Ink.GoldDim, 1f))
                {
                    g.DrawPath(pen, p);
                }
            }
            if (check)
            {
                using (Pen tick = new Pen(Ink.Bg, 1.8f))
                {
                    tick.StartCap = LineCap.Round;
                    tick.EndCap = LineCap.Round;
                    g.DrawLines(tick, new PointF[]
                    {
                        new PointF(box.X + 3.5f, box.Y + 7f),
                        new PointF(box.X + 6f, box.Y + 10f),
                        new PointF(box.X + 10.5f, box.Y + 4f)
                    });
                }
            }

            using (SolidBrush tb = new SolidBrush(Ink.Ivory))
            {
                g.DrawString(it.Name, _nameFont, tb, e.Bounds.X + 28, e.Bounds.Y + 3);
            }
            if (it.Memory > 0)
            {
                using (StringFormat rf = new StringFormat())
                using (SolidBrush mb = new SolidBrush(Ink.Muted))
                {
                    rf.Alignment = StringAlignment.Far;
                    g.DrawString(MemoryOptimizer.FormatBytes(it.Memory), _memFont, mb,
                        new RectangleF(e.Bounds.Right - 100, e.Bounds.Y + 4, 88, 18), rf);
                }
            }
        }

        // ── 拖动 / 关闭 ───────────────────────────────────────
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Y < 44)
            {
                if (e.X >= Width - 44) { DialogResult = DialogResult.Cancel; Close(); return; }
                _dragging = true;
                _dragOffset = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                Location = new Point(Cursor.Position.X - _dragOffset.X, Cursor.Position.Y - _dragOffset.Y);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            base.OnKeyDown(e);
        }

        // ── 数据 ──────────────────────────────────────────────
        private void FillList()
        {
            HashSet<string> checkedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object o in _list.CheckedItems)
            {
                ProcItem pi = o as ProcItem;
                if (pi != null) checkedNames.Add(pi.Name);
            }
            foreach (string ex in Result.Exclusions) checkedNames.Add(AppConfig.Normalize(ex));

            Dictionary<string, long> map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    string name = p.ProcessName + ".exe";
                    long ws = 0;
                    try { ws = p.WorkingSet64; } catch { }
                    if (!map.ContainsKey(name) || map[name] < ws) map[name] = ws;
                }
                catch { }
                finally { p.Dispose(); }
            }
            foreach (string ex in checkedNames)
            {
                if (!map.ContainsKey(ex)) map[ex] = 0;
            }

            List<ProcItem> items = new List<ProcItem>();
            foreach (KeyValuePair<string, long> kv in map)
            {
                ProcItem it = new ProcItem();
                it.Name = kv.Key;
                it.Memory = kv.Value;
                items.Add(it);
            }
            items.Sort(delegate (ProcItem a, ProcItem b)
            {
                int cmp = b.Memory.CompareTo(a.Memory);
                if (cmp != 0) return cmp;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (ProcItem it in items)
            {
                if (string.Equals(it.Name, "TaijiBall.exe", StringComparison.OrdinalIgnoreCase)) continue;
                _list.Items.Add(it, checkedNames.Contains(it.Name));
            }
            _list.EndUpdate();
        }

        private void AddTyped()
        {
            string raw = _input.Text.Trim();
            if (raw.Length == 0) return;
            string name = AppConfig.Normalize(raw);
            for (int i = 0; i < _list.Items.Count; i++)
            {
                ProcItem pi = _list.Items[i] as ProcItem;
                if (pi == null) continue;
                if (string.Equals(pi.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _list.SetItemChecked(i, true);
                    _input.Clear();
                    return;
                }
            }
            ProcItem added = new ProcItem();
            added.Name = name;
            added.Memory = 0;
            _list.Items.Add(added, true);
            _input.Clear();
        }

        private void UncheckAll()
        {
            for (int i = 0; i < _list.Items.Count; i++) _list.SetItemChecked(i, false);
        }

        private void Accept()
        {
            AppConfig cfg = new AppConfig();
            cfg.ShowUsage = _showUsage.Checked;
            cfg.SpinCw = _spinCw.Checked;
            cfg.FillUp = _fillUp.Checked;
            foreach (object o in _list.CheckedItems)
            {
                ProcItem pi = o as ProcItem;
                if (pi != null) cfg.AddExclusion(pi.Name);
            }
            Result = cfg;
        }
    }
    // ───────────────────── PCL2 同款：NT 内核内存整理 ─────────────────────
    internal static class MemoryOptimizer
    {
        private const int SystemFileCacheInformation = 21;
        private const int SystemMemoryListInformation = 80;
        private const int SystemCombinePhysicalMemoryInformation = 130;

        private const int MemoryEmptyWorkingSets = 2;
        private const int MemoryFlushModifiedList = 3;
        private const int MemoryPurgeStandbyList = 4;
        private const int MemoryPurgeLowPriorityStandbyList = 5;

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x0002;

        private const uint PROCESS_SET_QUOTA = 0x0100;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_ELEVATION { public int TokenIsElevated; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_FILECACHE_INFORMATION
        {
            public UIntPtr CurrentSize;
            public UIntPtr PeakSize;
            public uint PageFaultCount;
            public UIntPtr MinimumWorkingSet;
            public UIntPtr MaximumWorkingSet;
            public UIntPtr CurrentSizeIncludingTransitionInPages;
            public UIntPtr PeakSizeIncludingTransitionInPages;
            public uint TransitionRePurposeCount;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_COMBINE_INFORMATION_EX
        {
            public IntPtr Handle;
            public UIntPtr PagesCombined;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(int infoClass, IntPtr info, int length);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string system, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll,
            ref TOKEN_PRIVILEGES newState, int length, IntPtr previous, IntPtr returnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr token, int infoClass,
            ref TOKEN_ELEVATION info, int length, out int returnLength);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        // ── 状态查询 ──────────────────────────────────────────
        public static void GetStatus(out long total, out long avail, out int percent)
        {
            MEMORYSTATUSEX m = new MEMORYSTATUSEX();
            m.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (!GlobalMemoryStatusEx(ref m))
            {
                total = 0;
                avail = 0;
                percent = 0;
                return;
            }
            total = (long)m.ullTotalPhys;
            avail = (long)m.ullAvailPhys;
            percent = (int)m.dwMemoryLoad;
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
        }

        public static long GetAvailableBytes()
        {
            long total, avail;
            int percent;
            GetStatus(out total, out avail, out percent);
            return avail;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) bytes = 0;
            if (bytes >= 1073741824L) return (bytes / 1073741824.0).ToString("0.00") + " GB";
            if (bytes >= 1048576L) return (bytes / 1048576.0).ToString("0.0") + " MB";
            if (bytes >= 1024L) return (bytes / 1024.0).ToString("0") + " KB";
            return bytes + " B";
        }

        // ── 入口 ──────────────────────────────────────────────
        public static void RunChild(string resultPath)
        {
            long freed;
            string log;
            int skipped;
            Execute(AppConfig.Load(), out freed, out log, out skipped);
            if (!string.IsNullOrEmpty(resultPath))
            {
                try { File.WriteAllText(resultPath, freed.ToString() + "|" + skipped + "|" + log, Encoding.UTF8); }
                catch { }
            }
        }

        public static void RunTest(string logPath)
        {
            long freed;
            string log;
            int skipped;
            AppConfig cfg = AppConfig.Load();
            Execute(cfg, out freed, out log, out skipped);
            try
            {
                File.WriteAllText(logPath,
                    "elevated=" + IsElevated() + Environment.NewLine +
                    "exclusions=" + string.Join(",", cfg.Exclusions.ToArray()) + Environment.NewLine +
                    "skipped=" + skipped + Environment.NewLine +
                    "freed=" + freed + " (" + FormatBytes(freed) + ")" + Environment.NewLine +
                    log, Encoding.UTF8);
            }
            catch { }
        }

        public static bool OptimizeHere(out long freed, out string log, out int skipped)
        {
            Execute(AppConfig.Load(), out freed, out log, out skipped);
            return true;
        }

        public static bool RequestElevatedOptimize(out long freed, out string log, out int skipped)
        {
            freed = 0;
            skipped = 0;
            log = string.Empty;
            string tmp = Path.Combine(Path.GetTempPath(), "taijiball_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, "--opt \"" + tmp + "\"");
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(60000);
                }
                if (!File.Exists(tmp))
                {
                    log = "提权进程未返回结果";
                    return false;
                }
                string[] parts = File.ReadAllText(tmp, Encoding.UTF8).Split(new char[] { '|' }, 3);
                long.TryParse(parts[0].Trim(), out freed);
                if (parts.Length > 1) int.TryParse(parts[1].Trim(), out skipped);
                log = parts.Length > 2 ? parts[2] : string.Empty;
                if (freed < 0) freed = 0;
                return true;
            }
            catch (Exception ex)
            {
                log = ex.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        // ── 具体实现 ──────────────────────────────────────────
        private static void Execute(AppConfig cfg, out long freed, out string log, out int skipped)
        {
            StringBuilder sb = new StringBuilder();
            long total;
            long before;
            int percent;
            GetStatus(out total, out before, out percent);
            sb.AppendLine("可用内存(前): " + FormatBytes(before) + " / " + FormatBytes(total) + "，占用 " + percent + "%");

            if (!IsElevated())
                sb.AppendLine("警告: 当前进程未提权，多数步骤会失败");

            bool p1 = EnablePrivilege("SeProfileSingleProcessPrivilege");
            bool p2 = EnablePrivilege("SeIncreaseQuotaPrivilege");
            bool p3 = EnablePrivilege("SeDebugPrivilege");
            sb.AppendLine("特权 Profile=" + p1 + ", Quota=" + p2 + ", Debug=" + p3);

            skipped = 0;
            if (cfg.Exclusions.Count == 0)
            {
                Append(sb, "EmptyWorkingSets(全局)", SetMemoryList(MemoryEmptyWorkingSets));
            }
            else
            {
                int trimmed;
                int failed;
                TrimWorkingSets(cfg, out trimmed, out skipped, out failed);
                sb.AppendLine(string.Format("EmptyWorkingSets(逐进程) -> 清理 {0}，跳过 {1}，打不开 {2}", trimmed, skipped, failed));
            }

            Append(sb, "FlushFileCache", FlushFileCache());
            Append(sb, "FlushModifiedList", SetMemoryList(MemoryFlushModifiedList));
            Append(sb, "PurgeStandbyList", SetMemoryList(MemoryPurgeStandbyList));
            Append(sb, "PurgeLowPriorityStandbyList", SetMemoryList(MemoryPurgeLowPriorityStandbyList));
            Append(sb, "CombinePhysicalMemory", CombinePhysicalMemory());

            Thread.Sleep(250);
            long after;
            GetStatus(out total, out after, out percent);
            freed = after - before;
            if (freed < 0) freed = 0;
            sb.AppendLine("可用内存(后): " + FormatBytes(after) + "，占用 " + percent + "%");
            sb.AppendLine("合计释放: " + FormatBytes(freed));
            log = sb.ToString();
        }

        private static void TrimWorkingSets(AppConfig cfg, out int trimmed, out int skipped, out int failed)
        {
            trimmed = 0;
            skipped = 0;
            failed = 0;
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (cfg.IsExcluded(p.ProcessName))
                    {
                        skipped++;
                        continue;
                    }
                    IntPtr h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION, false, p.Id);
                    if (h == IntPtr.Zero)
                    {
                        failed++;
                        continue;
                    }
                    try
                    {
                        if (EmptyWorkingSet(h)) trimmed++;
                        else failed++;
                    }
                    finally { CloseHandle(h); }
                }
                catch { failed++; }
                finally { p.Dispose(); }
            }
        }

        private static void Append(StringBuilder sb, string name, int status)
        {
            sb.AppendLine(name + " -> 0x" + status.ToString("X8") +
                (status == 0 ? " (成功)" : " (" + StatusText(status) + ")"));
        }

        private static string StatusText(int status)
        {
            switch ((uint)status)
            {
                case 0xC0000061: return "STATUS_PRIVILEGE_NOT_HELD";
                case 0xC000000D: return "STATUS_INVALID_PARAMETER";
                case 0xC0000003: return "STATUS_INVALID_INFO_CLASS";
                case 0xC0000022: return "STATUS_ACCESS_DENIED";
                default: return "失败";
            }
        }

        private static int SetMemoryList(int command)
        {
            IntPtr p = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(p, command);
                return NtSetSystemInformation(SystemMemoryListInformation, p, sizeof(int));
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        private static int FlushFileCache()
        {
            SYSTEM_FILECACHE_INFORMATION info = new SYSTEM_FILECACHE_INFORMATION();
            int size = Marshal.SizeOf(typeof(SYSTEM_FILECACHE_INFORMATION));
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, p, false);
                info.MinimumWorkingSet = UIntPtr.Size == 8 ? new UIntPtr(ulong.MaxValue) : new UIntPtr(uint.MaxValue);
                info.MaximumWorkingSet = info.MinimumWorkingSet;
                Marshal.StructureToPtr(info, p, false);
                return NtSetSystemInformation(SystemFileCacheInformation, p, size);
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        private static int CombinePhysicalMemory()
        {
            MEMORY_COMBINE_INFORMATION_EX info = new MEMORY_COMBINE_INFORMATION_EX();
            int size = Marshal.SizeOf(typeof(MEMORY_COMBINE_INFORMATION_EX));
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, p, false);
                return NtSetSystemInformation(SystemCombinePhysicalMemoryInformation, p, size);
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        private static bool EnablePrivilege(string name)
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
                    return false;
                LUID luid;
                if (!LookupPrivilegeValue(null, name, out luid)) return false;
                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.PrivilegeCount = 1;
                tp.Privileges.Luid = luid;
                tp.Privileges.Attributes = SE_PRIVILEGE_ENABLED;
                if (!AdjustTokenPrivileges(token, false, ref tp,
                        Marshal.SizeOf(typeof(TOKEN_PRIVILEGES)), IntPtr.Zero, IntPtr.Zero))
                    return false;
                return Marshal.GetLastWin32Error() == 0;
            }
            finally
            {
                if (token != IntPtr.Zero) CloseHandle(token);
            }
        }

        public static bool IsElevated()
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out token)) return false;
                TOKEN_ELEVATION elevation = new TOKEN_ELEVATION();
                int returned;
                if (!GetTokenInformation(token, 20, ref elevation,
                        Marshal.SizeOf(typeof(TOKEN_ELEVATION)), out returned))
                    return false;
                return elevation.TokenIsElevated != 0;
            }
            finally
            {
                if (token != IntPtr.Zero) CloseHandle(token);
            }
        }
    }
}
