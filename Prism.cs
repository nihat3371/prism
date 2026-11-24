// Prism - a tiny batch image converter for Windows. UI only; image work lives in Engine.cs.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Prism")]
[assembly: AssemblyProduct("Prism")]
[assembly: AssemblyDescription("Tiny batch image converter")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

static class U
{
    public static GraphicsPath Round(Rectangle r, int rad)
    {
        GraphicsPath p = new GraphicsPath();
        if (rad < 1) { p.AddRectangle(r); return p; }
        int d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

class Box : Panel
{
    public Color Fill = Color.White, Line = Color.Gray, AccentC = Color.Orange, BigC = Color.Black, SmallC = Color.Gray;
    public bool Dashed, Hot, HasFiles;
    public string Big = "", Small = "", Title = "";
    public List<string> Names = new List<string>();
    public Font FBig, FSmall;

    public Box()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x133 || m.Msg == 0x138)
        {
            FlatText ft = Control.FromHandle(m.LParam) as FlatText;
            if (ft != null) { m.Result = ft.Colorize(m.WParam); return; }
        }
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = U.Round(r, 12))
        {
            using (SolidBrush b = new SolidBrush(Fill)) g.FillPath(b, path);
            using (Pen pen = new Pen(Hot ? AccentC : Line, 1f))
            {
                if (Dashed) pen.DashStyle = DashStyle.Dash;
                g.DrawPath(pen, path);
            }
        }
        if (FBig == null) return;
        if (HasFiles)
        {
            TextRenderer.DrawText(g, Title, FBig, new Rectangle(20, 20, Width - 72, 26), BigC, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            int y = 54;
            for (int i = 0; i < Names.Count && i < 3; i++)
            {
                TextRenderer.DrawText(g, Names[i], FSmall, new Rectangle(20, y, Width - 40, 18), SmallC, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                y += 20;
            }
        }
        else
        {
            TextRenderer.DrawText(g, Big, FBig, new Rectangle(20, 40, Width - 40, 30), BigC, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, Small, FSmall, new Rectangle(20, 74, Width - 40, 22), SmallC, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

class FlatCombo : Control
{
    public List<string> Items = new List<string>();
    public event EventHandler SelectedIndexChanged;
    public Color Fill = Color.White, Line = Color.Gray, TextC = Color.Black, Hi = Color.Orange;
    int sel = -1;
    ContextMenuStrip menu;

    public FlatCombo()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        TabStop = false;
        menu = new ContextMenuStrip();
        menu.ShowImageMargin = false;
        menu.Renderer = new FlatMenuRenderer(this);
        menu.Opening += delegate { Rebuild(); };
    }

    public int SelectedIndex
    {
        get { return sel; }
        set
        {
            if (sel == value) return;
            sel = value;
            Invalidate();
            if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
        }
    }

    public void AddRange(string[] a) { for (int i = 0; i < a.Length; i++) Items.Add(a[i]); }

    void Rebuild()
    {
        menu.Items.Clear();
        for (int i = 0; i < Items.Count; i++)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(Items[i]);
            mi.Tag = i;
            mi.Click += delegate(object s, EventArgs e)
            {
                ToolStripMenuItem it = (ToolStripMenuItem)s;
                SelectedIndex = (int)it.Tag;
            };
            menu.Items.Add(mi);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        menu.Show(this, new Point(0, Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = U.Round(r, 6))
        {
            using (SolidBrush b = new SolidBrush(Fill)) g.FillPath(b, p);
            using (Pen pen = new Pen(Line)) g.DrawPath(pen, p);
        }
        if (sel >= 0 && sel < Items.Count)
            TextRenderer.DrawText(g, Items[sel], Font, new Rectangle(6, 1, Width - 24, Height - 2), Enabled ? TextC : ControlPaint.Light(TextC), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using (Pen pen = new Pen(TextC, 1.6f))
        {
            int cx = Width - 13, cy = Height / 2 - 1;
            g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
            g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
        }
    }
}

class FlatMenuRenderer : ToolStripProfessionalRenderer
{
    FlatCombo owner;
    public FlatMenuRenderer(FlatCombo c) { owner = c; }
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using (SolidBrush b = new SolidBrush(owner.Fill)) e.Graphics.FillRectangle(b, e.AffectedBounds);
    }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        using (SolidBrush b = new SolidBrush(e.Item.Selected ? owner.Hi : owner.Fill)) e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Selected ? Color.White : owner.TextC;
        base.OnRenderItemText(e);
    }
}

class FlatText : TextBox
{
    public Color TextC = Color.Black, MuteC = Color.Gray, BkC = Color.White;
    IntPtr brush;

    [DllImport("gdi32.dll")]
    static extern int SetTextColor(IntPtr hdc, int cr);
    [DllImport("gdi32.dll")]
    static extern int SetBkColor(IntPtr hdc, int cr);
    [DllImport("gdi32.dll")]
    static extern IntPtr CreateSolidBrush(int cr);
    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr h);

    public IntPtr Colorize(IntPtr hdc)
    {
        SetTextColor(hdc, ColorTranslator.ToWin32(Enabled ? TextC : MuteC));
        SetBkColor(hdc, ColorTranslator.ToWin32(BkC));
        if (brush != IntPtr.Zero) DeleteObject(brush);
        brush = CreateSolidBrush(ColorTranslator.ToWin32(BkC));
        return brush;
    }
    protected override void Dispose(bool disposing)
    {
        if (brush != IntPtr.Zero) { DeleteObject(brush); brush = IntPtr.Zero; }
        base.Dispose(disposing);
    }
}

class Check : Control
{
    public bool Checked;
    public event EventHandler CheckedChanged;
    public Color Box = Color.Gray, TextC = Color.Black, Hi = Color.Orange;

    public Check()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }
    public Size PrefSize()
    {
        Size s = TextRenderer.MeasureText(Text, Font);
        return new Size(s.Width + 18, Math.Max(24, s.Height));
    }
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
        Invalidate();
        if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int bs = 12, bx = 0, tx = 14, y = (Height - bs) / 2;
        using (Pen p = new Pen(Box, 1.4f)) g.DrawRectangle(p, new Rectangle(bx, y, bs, bs));
        if (Checked)
        {
            using (Pen p = new Pen(Enabled ? Hi : ControlPaint.Light(Hi), 2f))
            {
                g.DrawLine(p, bx + 2, y + 5, bx + 4, y + 8);
                g.DrawLine(p, bx + 4, y + 8, bx + 9, y + 2);
            }
        }
        Color tc = Enabled ? TextC : ControlPaint.Light(TextC);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(tx, 0, Width - tx, Height), tc,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}


class Prism : Form
{
    static readonly Dictionary<string, string> EN = new Dictionary<string, string>();
    static readonly Dictionary<string, string> TRD = new Dictionary<string, string>();
    static bool TR = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";
    static int ForceDark = -1;
    static bool ForceTR;
    static bool shotMode;

    static Prism()
    {
        S("drop", "Drop images or folders here", "Resimleri veya klas\u00f6rleri buraya b\u0131rak");
        S("drop2", "or click to choose \u00b7 Ctrl+V pastes a screenshot", "ya da t\u0131klay\u0131p se\u00e7 \u00b7 Ctrl+V ekran g\u00f6r\u00fcnt\u00fcs\u00fc yap\u0131\u015ft\u0131r\u0131r");
        S("clear", "Clear", "Temizle");
        S("format", "Format", "Bi\u00e7im");
        S("size", "Size", "Boyut");
        S("crop", "Crop", "K\u0131rp");
        S("rotate", "Rotate", "D\u00f6nd\u00fcr");
        S("quality", "JPG quality", "JPG kalitesi");
        S("keep", "Keep under", "\u015eu boyutun alt\u0131nda tut:");
        S("save", "Save to", "Kaydet");
        S("saveDef", "Prism folder next to originals", "Kaynak klas\u00f6rde Prism klas\u00f6r\u00fc");
        S("saveChoose", "Choose folder...", "Klas\u00f6r se\u00e7...");
        S("note", "Photos are auto-rotated. Location and camera data (EXIF) are removed.", "Foto\u011fraflar otomatik d\u00fczeltilir. Konum ve kamera bilgisi (EXIF) silinir.");
        S("convert", "Convert {0} images", "{0} resmi d\u00f6n\u00fc\u015ft\u00fcr");
        S("convert1", "Convert 1 image", "1 resmi d\u00f6n\u00fc\u015ft\u00fcr");
        S("open", "Open folder", "Klas\u00f6r\u00fc a\u00e7");
        S("doneFmt", "Done: {0} files, {1} -> {2} ({3}%)", "Bitti: {0} dosya, {1} -> {2} ({3}%)");
        S("failed", "{0} failed", "{0} ba\u015far\u0131s\u0131z");
        S("sendo", "Add to Send To menu", "G\u00f6nder men\u00fcs\u00fcne ekle");
        S("added", "Added to Send To menu", "G\u00f6nder men\u00fcs\u00fcne eklendi");
        S("images", "{0} images", "{0} resim");
        S("images1", "1 image", "1 resim");
        S("errors", "Errors", "Hatalar");
        S("openTitle", "Choose images", "Resim se\u00e7");
        S("choose", "Choose a folder", "Klas\u00f6r se\u00e7");
        S("filters", "Images", "Resimler");
        S("all", "All files", "T\u00fcm dosyalar");
        S("prog", "{0} / {1} \u00b7 {2}", "{0} / {1} \u00b7 {2}");
        S("px", "px", "px");
        S("f0", "Same as source", "Kaynakla ayn\u0131");
        S("f1", "JPG", "JPG");
        S("f2", "PNG", "PNG");
        S("f3", "PDF (one file)", "PDF (tek dosya)");
        S("f4", "BMP", "BMP");
        S("f5", "GIF", "GIF");
        S("f6", "TIFF", "TIFF");
        S("s0", "Original", "Orijinal");
        S("s1", "75%", "75%");
        S("s2", "50%", "50%");
        S("s3", "25%", "25%");
        S("s4", "Long edge 3840 px", "Uzun kenar 3840 px");
        S("s5", "Long edge 2048 px", "Uzun kenar 2048 px");
        S("s6", "Long edge 1080 px", "Uzun kenar 1080 px");
        S("s7", "Long edge 720 px", "Uzun kenar 720 px");
        S("s8", "Custom", "\u00d6zel");
        S("c0", "None", "Yok");
        S("c1", "Square 1:1", "Kare 1:1");
        S("c2", "Portrait 4:5", "Dikey 4:5");
        S("c3", "Wide 16:9", "Geni\u015f 16:9");
        S("c4", "Story 9:16", "Hikaye 9:16");
        S("r0", "None", "Yok");
        S("r1", "Right 90\u00b0", "Sa\u011fa 90\u00b0");
        S("r2", "Left 90\u00b0", "Sola 90\u00b0");
        S("r3", "180\u00b0", "180\u00b0");
        S("r4", "Mirror", "Ayna");
    }

    static void S(string k, string en, string tr) { EN[k] = en; TRD[k] = tr; }
    static string T(string k)
    {
        string v;
        if (TR && TRD.TryGetValue(k, out v)) return v;
        if (EN.TryGetValue(k, out v)) return v;
        return k;
    }

    Color Win, Card, Border, TextM, Strong, Muted, Accent, AccentHi;

    List<string> files = new List<string>();
    List<string> lastErrors;
    string openFolder;
    Box drop;
    Box opt;
    LinkLabel clearLink, openLink, failLink;
    Button menuBtn, convertBtn;
    Label status, noteLabel;
    FlatCombo fmtBox, sizeBox, cropBox, rotBox, saveBox;
    TextBox customBox, kbBox;
    Label pxLabel, kbLabel, qualVal;
    TrackBar quality;
    Check keepChk;
    ContextMenuStrip ctx;
    bool dark, running;
    bool suppressSaveCombo;
    int fmt, sizeIdx, customPx = 1920, crop, rot, qualityVal = 85, targetKB = 500;
    bool keepUnder, outMode;
    string outDir = "";

    const int Pad = 16;

    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

    [STAThread]
    static int Main(string[] args)
    {
        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length >= 2 && args[0] == "--shot") { Shot(args); return 0; }
        if (args.Length >= 3 && args[0] == "--run") return RunCli(args);
        Application.Run(new Prism(args));
        return 0;
    }

    static void Shot(string[] a)
    {
        string outPath = a[1];
        string theme = a.Length > 2 ? a[2] : "light";
        ForceDark = theme.StartsWith("dark") ? 1 : 0;
        ForceTR = theme.EndsWith("-tr");
        TR = ForceTR;
        shotMode = true;
        string[] rest = new string[Math.Max(0, a.Length - 3)];
        for (int i = 3; i < a.Length; i++) rest[i - 3] = a[i];
        Prism f = new Prism(rest);
        f.StartPosition = FormStartPosition.Manual;
        f.Location = new Point(-32000, -32000);
        f.ShowInTaskbar = false;
        f.Show();
        for (int i = 0; i < 15; i++) { Application.DoEvents(); Thread.Sleep(20); }
        using (Bitmap full = new Bitmap(f.Width, f.Height))
        {
            f.DrawToBitmap(full, new Rectangle(0, 0, f.Width, f.Height));
            Point off = f.PointToScreen(Point.Empty);
            off.X -= f.Location.X;
            off.Y -= f.Location.Y;
            using (Bitmap b = full.Clone(new Rectangle(off, f.ClientSize), full.PixelFormat))
            {
                b.Save(outPath, ImageFormat.Png);
            }
        }
        Environment.Exit(0);
    }

    static int RunCli(string[] a)
    {
        string outdir = a[1];
        Job job = new Job();
        job.OutDir = outdir;
        string[] parts = a[2].Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            int eq = parts[i].IndexOf('=');
            if (eq < 1) continue;
            string k = parts[i].Substring(0, eq).Trim().ToLowerInvariant();
            string v = parts[i].Substring(eq + 1).Trim();
            try
            {
                if (k == "format") job.Format = ParseFormat(v);
                else if (k == "percent") job.ResizePercent = int.Parse(v, CultureInfo.InvariantCulture);
                else if (k == "maxedge") job.MaxEdge = int.Parse(v, CultureInfo.InvariantCulture);
                else if (k == "crop") job.Crop = ParseCrop(v);
                else if (k == "turn") job.Turn = ParseTurn(v);
                else if (k == "quality") job.Quality = int.Parse(v, CultureInfo.InvariantCulture);
                else if (k == "targetkb") job.TargetKB = int.Parse(v, CultureInfo.InvariantCulture);
            }
            catch { }
        }
        List<string> list = new List<string>();
        for (int i = 3; i < a.Length; i++)
        {
            try
            {
                if (Directory.Exists(a[i]))
                {
                    string[] fs = Directory.GetFiles(a[i]);
                    for (int j = 0; j < fs.Length; j++) if (Engine.IsImage(fs[j])) list.Add(fs[j]);
                }
                else if (File.Exists(a[i])) list.Add(a[i]);
            }
            catch { }
        }
        List<string> errors = new List<string>();
        List<string> outs = Engine.Run(list, job, delegate(int i, int n, string cur) { }, errors);
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < outs.Count; i++)
        {
            int w = 0, h = 0;
            try { using (Image im = Image.FromFile(outs[i])) { w = im.Width; h = im.Height; } }
            catch { }
            long len = 0;
            try { len = new FileInfo(outs[i]).Length; } catch { }
            sb.AppendLine(outs[i] + " " + w + " x " + h + " " + len);
        }
        for (int i = 0; i < errors.Count; i++) sb.AppendLine(errors[i]);
        try
        {
            Directory.CreateDirectory(outdir);
            File.WriteAllText(Path.Combine(outdir, "report.txt"), sb.ToString());
        }
        catch { }
        return errors.Count == 0 ? 0 : 1;
    }

    static OutFormat ParseFormat(string v)
    {
        if (v == "jpg") return OutFormat.Jpg;
        if (v == "png") return OutFormat.Png;
        if (v == "bmp") return OutFormat.Bmp;
        if (v == "gif") return OutFormat.Gif;
        if (v == "tiff") return OutFormat.Tiff;
        if (v == "pdf") return OutFormat.Pdf;
        return OutFormat.Same;
    }
    static Crop ParseCrop(string v)
    {
        if (v == "square") return Crop.Square;
        if (v == "portrait") return Crop.Portrait45;
        if (v == "wide") return Crop.Wide169;
        if (v == "tall") return Crop.Tall916;
        return Crop.None;
    }
    static Turn ParseTurn(string v)
    {
        if (v == "right") return Turn.Right90;
        if (v == "left") return Turn.Left90;
        if (v == "half") return Turn.Half180;
        if (v == "flip") return Turn.FlipH;
        return Turn.None;
    }

    Prism(string[] args)
    {
        Text = "Prism";
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { }
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(520, 626);
        Font = new Font("Segoe UI", 9.75f);
        KeyPreview = true;
        if (ForceDark >= 0) dark = ForceDark == 1; else dark = IsDarkTheme();
        LoadSettings();
        BuildPalette();
        BuildUi();
        ApplyTheme();
        if (args != null) for (int i = 0; i < args.Length; i++) AddPath(args[i]);
        UpdateFiles();
        FormClosing += delegate { SaveSettings(); };
    }

    static bool IsDarkTheme()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                if (k != null)
                {
                    object v = k.GetValue("AppsUseLightTheme");
                    if (v is int) return (int)v == 0;
                }
        }
        catch { }
        return false;
    }

    void BuildPalette()
    {
        if (dark)
        {
            Win = Color.FromArgb(31, 30, 29);
            Card = Color.FromArgb(38, 38, 36);
            Border = Color.FromArgb(61, 61, 58);
            TextM = Color.FromArgb(217, 215, 206);
            Strong = Color.FromArgb(236, 235, 228);
            Muted = Color.FromArgb(156, 154, 146);
            Accent = Color.FromArgb(217, 119, 87);
            AccentHi = Color.FromArgb(227, 154, 126);
        }
        else
        {
            Win = Color.FromArgb(240, 238, 230);
            Card = Color.FromArgb(250, 249, 245);
            Border = Color.FromArgb(232, 230, 220);
            TextM = Color.FromArgb(61, 61, 58);
            Strong = Color.FromArgb(20, 20, 19);
            Muted = Color.FromArgb(115, 114, 108);
            Accent = Color.FromArgb(198, 97, 63);
            AccentHi = Color.FromArgb(178, 85, 47);
        }
    }

    void BuildUi()
    {
        Font fBig = new Font("Segoe UI", 12.5f, FontStyle.Bold);
        Font fSmall = new Font("Segoe UI", 9f);

        drop = new Box();
        drop.Bounds = new Rectangle(Pad, Pad, 488, 150);
        drop.Dashed = true;
        drop.FBig = fBig;
        drop.FSmall = fSmall;
        drop.Big = T("drop");
        drop.Small = T("drop2");
        drop.Cursor = Cursors.Hand;
        drop.AllowDrop = true;
        drop.Click += delegate { OpenPicker(); };
        drop.DragEnter += delegate(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Copy; drop.Hot = true; drop.Invalidate(); }
        };
        drop.DragLeave += delegate { drop.Hot = false; drop.Invalidate(); };
        drop.DragDrop += delegate(object s, DragEventArgs e)
        {
            drop.Hot = false;
            drop.Invalidate();
            string[] dd = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (dd != null) { for (int i = 0; i < dd.Length; i++) AddPath(dd[i]); UpdateFiles(); }
        };
        Controls.Add(drop);

        clearLink = new LinkLabel();
        clearLink.Text = T("clear");
        clearLink.AutoSize = false;
        clearLink.TextAlign = ContentAlignment.MiddleRight;
        clearLink.Bounds = new Rectangle(488 - 96, 150 - 30, 84, 20);
        clearLink.LinkBehavior = LinkBehavior.HoverUnderline;
        clearLink.Visible = false;
        clearLink.LinkClicked += delegate { files.Clear(); UpdateFiles(); };
        drop.Controls.Add(clearLink);

        menuBtn = new Button();
        menuBtn.Text = "\u22ef";
        menuBtn.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        menuBtn.Bounds = new Rectangle(488 - 38, 8, 26, 24);
        menuBtn.FlatStyle = FlatStyle.Flat;
        menuBtn.UseVisualStyleBackColor = false;
        menuBtn.FlatAppearance.BorderSize = 0;
        menuBtn.TextAlign = ContentAlignment.MiddleCenter;
        menuBtn.Cursor = Cursors.Hand;
        ctx = new ContextMenuStrip();
        ToolStripMenuItem mi = new ToolStripMenuItem(T("sendo"));
        mi.Click += delegate { AddSendTo(); };
        ctx.Items.Add(mi);
        menuBtn.Click += delegate { ctx.Show(menuBtn, new Point(0, menuBtn.Height)); };
        drop.Controls.Add(menuBtn);

        opt = new Box();
        opt.Bounds = new Rectangle(Pad, 176, 488, 338);
        Controls.Add(opt);

        L(opt, T("format"), 16);
        fmtBox = C(opt, 118, 16, 354, new string[] { T("f0"), T("f1"), T("f2"), T("f3"), T("f4"), T("f5"), T("f6") });
        fmtBox.SelectedIndexChanged += delegate { fmt = fmtBox.SelectedIndex; UpdateTargetState(); };

        L(opt, T("size"), 55);
        sizeBox = C(opt, 118, 55, 354, new string[] { T("s0"), T("s1"), T("s2"), T("s3"), T("s4"), T("s5"), T("s6"), T("s7"), T("s8") });
        sizeBox.SelectedIndexChanged += delegate { sizeIdx = sizeBox.SelectedIndex; UpdateTargetState(); };
        customBox = Tb(opt, 326, 55, 58);
        pxLabel = Lbl(opt, T("px"), 388, 55, 30, 25);

        L(opt, T("crop"), 94);
        cropBox = C(opt, 118, 94, 354, new string[] { T("c0"), T("c1"), T("c2"), T("c3"), T("c4") });
        cropBox.SelectedIndexChanged += delegate { crop = cropBox.SelectedIndex; };

        L(opt, T("rotate"), 133);
        rotBox = C(opt, 118, 133, 354, new string[] { T("r0"), T("r1"), T("r2"), T("r3"), T("r4") });
        rotBox.SelectedIndexChanged += delegate { rot = rotBox.SelectedIndex; };

        Lbl(opt, T("quality"), 16, 160, 96, 40);
        quality = new TrackBar();
        quality.Minimum = 10;
        quality.Maximum = 100;
        quality.TickStyle = TickStyle.None;
        quality.SmallChange = 1;
        quality.LargeChange = 10;
        quality.AutoSize = false;
        quality.Value = 85;
        quality.Bounds = new Rectangle(118, 160, 304, 40);
        quality.ValueChanged += delegate { if (qualVal != null) qualVal.Text = quality.Value.ToString(); };
        opt.Controls.Add(quality);
        qualVal = Lbl(opt, "85", 428, 160, 40, 40);

        keepChk = new Check();
        keepChk.Text = T("keep");
        keepChk.Font = Font;
        Size ks = keepChk.PrefSize();
        keepChk.Bounds = new Rectangle(16, 209, ks.Width, 24);
        keepChk.CheckedChanged += delegate { keepUnder = keepChk.Checked; UpdateTargetState(); };
        opt.Controls.Add(keepChk);
        int kw = Math.Max(118, 16 + ks.Width + 8);
        kbBox = Tb(opt, kw, 209, 62);
        kbBox.Text = "500";
        kbLabel = Lbl(opt, "KB", kw + 68, 209, 30, 24);

        L(opt, T("save"), 250);
        saveBox = C(opt, 118, 250, 354, new string[] { T("saveDef"), T("saveChoose") });
        saveBox.SelectedIndexChanged += OnSaveChanged;

        noteLabel = new Label();
        noteLabel.Text = T("note");
        noteLabel.AutoSize = false;
        noteLabel.Font = fSmall;
        noteLabel.Bounds = new Rectangle(16, 292, 456, 40);
        opt.Controls.Add(noteLabel);

        convertBtn = new Button();
        convertBtn.Bounds = new Rectangle(Pad, 522, 488, 40);
        convertBtn.FlatStyle = FlatStyle.Flat;
        convertBtn.FlatAppearance.BorderSize = 1;
        convertBtn.UseVisualStyleBackColor = false;
        convertBtn.Cursor = Cursors.Hand;
        convertBtn.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        convertBtn.Click += delegate { StartConvert(); };
        Controls.Add(convertBtn);

        status = new Label();
        status.AutoSize = false;
        status.Bounds = new Rectangle(Pad, 570, 488, 22);
        Controls.Add(status);

        openLink = new LinkLabel();
        openLink.Text = T("open");
        openLink.AutoSize = true;
        openLink.Location = new Point(Pad, 594);
        openLink.Visible = false;
        openLink.LinkBehavior = LinkBehavior.HoverUnderline;
        openLink.LinkClicked += delegate { if (openFolder != null) try { Process.Start(openFolder); } catch { } };
        Controls.Add(openLink);

        failLink = new LinkLabel();
        failLink.AutoSize = true;
        failLink.Location = new Point(Pad + 110, 594);
        failLink.Visible = false;
        failLink.LinkBehavior = LinkBehavior.HoverUnderline;
        failLink.LinkClicked += delegate
        {
            if (lastErrors != null) MessageBox.Show(string.Join(Environment.NewLine, lastErrors.ToArray()), T("errors"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        Controls.Add(failLink);

        fmtBox.SelectedIndex = Clamp(fmt, 0, 6);
        sizeBox.SelectedIndex = Clamp(sizeIdx, 0, 8);
        cropBox.SelectedIndex = Clamp(crop, 0, 4);
        rotBox.SelectedIndex = Clamp(rot, 0, 4);
        quality.Value = Clamp(qualityVal, 10, 100);
        keepChk.Checked = keepUnder;
        kbBox.Text = targetKB.ToString(CultureInfo.InvariantCulture);
        customBox.Text = customPx.ToString(CultureInfo.InvariantCulture);

        suppressSaveCombo = true;
        saveBox.SelectedIndex = 0;
        if (outMode && outDir.Length > 0) { saveBox.Items[1] = outDir; saveBox.SelectedIndex = 1; }
        suppressSaveCombo = false;
        UpdateTargetState();
    }

    Label Lbl(Control parent, string txt, int x, int y, int w, int h)
    {
        Label l = new Label();
        l.Text = txt;
        l.AutoSize = false;
        l.TextAlign = ContentAlignment.MiddleLeft;
        l.Bounds = new Rectangle(x, y, w, h);
        parent.Controls.Add(l);
        return l;
    }
    Label L(Control parent, string txt, int y) { return Lbl(parent, txt, 16, y, 96, 25); }
    FlatCombo C(Control parent, int x, int y, int w, string[] items)
    {
        FlatCombo c = new FlatCombo();
        c.Bounds = new Rectangle(x, y, w, 25);
        c.Font = Font;
        c.AddRange(items);
        parent.Controls.Add(c);
        return c;
    }
    TextBox Tb(Control parent, int x, int y, int w)
    {
        TextBox t = new FlatText();
        t.BorderStyle = BorderStyle.FixedSingle;
        t.Bounds = new Rectangle(x, y, w, 24);
        parent.Controls.Add(t);
        return t;
    }
    static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

    void ApplyTheme()
    {
        BackColor = Win;
        drop.Fill = Card; drop.Line = Border; drop.AccentC = Accent; drop.BigC = Strong; drop.SmallC = Muted;
        drop.BackColor = Win; drop.Invalidate();
        opt.Fill = Card; opt.Line = Border; opt.BackColor = Win; opt.Invalidate();
        for (int i = 0; i < opt.Controls.Count; i++)
        {
            Control ctl = opt.Controls[i];
            ctl.ForeColor = TextM;
            ctl.BackColor = Card;
            FlatCombo fc = ctl as FlatCombo;
            if (fc != null) { fc.Fill = Card; fc.Line = Border; fc.TextC = TextM; fc.Hi = Accent; fc.Invalidate(); }
            FlatText ft = ctl as FlatText;
            if (ft != null) { ft.TextC = TextM; ft.MuteC = Muted; ft.BkC = Card; ft.Invalidate(); }
        }
        for (int i = 0; i < drop.Controls.Count; i++) { drop.Controls[i].ForeColor = TextM; drop.Controls[i].BackColor = Card; }
        noteLabel.ForeColor = Muted;
        qualVal.ForeColor = quality.Enabled ? TextM : Muted;
        kbLabel.ForeColor = kbBox.Enabled ? TextM : Muted;
        clearLink.LinkColor = Accent; clearLink.ActiveLinkColor = AccentHi; clearLink.VisitedLinkColor = Accent; clearLink.ForeColor = Muted;
        menuBtn.ForeColor = Muted;
        menuBtn.FlatAppearance.MouseOverBackColor = Border;
        menuBtn.FlatAppearance.MouseDownBackColor = Border;
        keepChk.Box = Muted; keepChk.TextC = TextM; keepChk.Hi = Accent; keepChk.Invalidate();
        convertBtn.BackColor = Accent; convertBtn.ForeColor = Color.White;
        convertBtn.FlatAppearance.BorderColor = Border;
        convertBtn.FlatAppearance.MouseOverBackColor = AccentHi;
        convertBtn.FlatAppearance.MouseDownBackColor = AccentHi;
        status.ForeColor = Muted;
        openLink.LinkColor = Accent; openLink.ActiveLinkColor = AccentHi; openLink.VisitedLinkColor = Accent; openLink.ForeColor = TextM;
        failLink.LinkColor = Accent; failLink.ActiveLinkColor = AccentHi; failLink.VisitedLinkColor = Accent; failLink.ForeColor = TextM;
        ctx.BackColor = Card; ctx.ForeColor = TextM;
    }

    void UpdateTargetState()
    {
        if (fmtBox == null || quality == null || keepChk == null || kbBox == null) return;
        int fi = fmtBox.SelectedIndex;
        bool qualOn = !(fi == 2 || fi == 4 || fi == 5 || fi == 6);
        bool keepOn = fi == 0 || fi == 1;
        keepChk.Enabled = keepOn;
        bool kbOn = keepOn && keepChk.Checked;
        kbBox.Enabled = kbOn;
        kbLabel.ForeColor = kbOn ? TextM : Muted;
        quality.Enabled = qualOn && !kbOn;
        qualVal.ForeColor = quality.Enabled ? TextM : Muted;
        bool cust = sizeBox == null || sizeBox.SelectedIndex == 8;
        if (sizeBox != null) sizeBox.Width = cust ? 200 : 354;
        if (customBox != null) customBox.Visible = cust;
        if (pxLabel != null) pxLabel.Visible = cust;
    }

    void UpdateFiles()
    {
        if (drop == null) return;
        drop.HasFiles = files.Count > 0;
        drop.Title = files.Count == 1 ? T("images1") : string.Format(CultureInfo.CurrentCulture, T("images"), files.Count);
        drop.Names.Clear();
        for (int i = 0; i < files.Count && i < 3; i++) drop.Names.Add(Path.GetFileName(files[i]));
        drop.Invalidate();
        clearLink.Visible = files.Count > 0;
        if (convertBtn != null)
        {
            convertBtn.Enabled = files.Count > 0 && !running;
            convertBtn.Text = files.Count == 1 ? T("convert1") : string.Format(CultureInfo.CurrentCulture, T("convert"), files.Count);
        }
    }

    void AddPath(string p)
    {
        try
        {
            if (File.Exists(p)) AddFile(p);
            else if (Directory.Exists(p))
            {
                string[] fs = Directory.GetFiles(p);
                for (int i = 0; i < fs.Length; i++) if (Engine.IsImage(fs[i])) AddFile(fs[i]);
            }
        }
        catch { }
    }
    void AddFile(string p)
    {
        try
        {
            string full = Path.GetFullPath(p);
            for (int i = 0; i < files.Count; i++) if (string.Equals(files[i], full, StringComparison.OrdinalIgnoreCase)) return;
            if (Engine.IsImage(full)) files.Add(full);
        }
        catch { }
    }

    string FilterString()
    {
        StringBuilder sb = new StringBuilder();
        string[] ex = Engine.InputExt;
        for (int i = 0; i < ex.Length; i++)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append('*').Append(ex[i]);
        }
        return T("filters") + "|" + sb.ToString() + "|" + T("all") + "|*.*";
    }

    void OpenPicker()
    {
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Multiselect = true;
            d.Filter = FilterString();
            d.Title = T("openTitle");
            if (d.ShowDialog(this) == DialogResult.OK)
            {
                for (int i = 0; i < d.FileNames.Length; i++) AddFile(d.FileNames[i]);
                UpdateFiles();
            }
        }
    }

    void Paste()
    {
        try
        {
            if (Clipboard.ContainsImage())
            {
                Image img = Clipboard.GetImage();
                if (img != null)
                {
                    string p = Engine.SaveClipboardImage(img);
                    if (!string.IsNullOrEmpty(p)) { AddFile(p); UpdateFiles(); }
                }
            }
            else if (Clipboard.ContainsFileDropList())
            {
                System.Collections.Specialized.StringCollection sc = Clipboard.GetFileDropList();
                for (int i = 0; i < sc.Count; i++) AddPath(sc[i]);
                UpdateFiles();
            }
        }
        catch { }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.V))
        {
            bool has = false;
            try { has = Clipboard.ContainsImage() || Clipboard.ContainsFileDropList(); } catch { }
            if (has) { Paste(); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void AddSendTo()
    {
        try
        {
            string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\SendTo\Prism.lnk");
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            object sc = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { Application.ExecutablePath });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(Application.ExecutablePath) });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, new object[0]);
            SetStatus(T("added"));
        }
        catch (Exception ex) { SetStatus(ex.Message); }
    }

    void StartConvert()
    {
        if (files.Count == 0 || running) return;
        SaveSettings();
        List<string> list = new List<string>(files);
        Job job = MakeJob();
        long inBytes = 0;
        for (int i = 0; i < list.Count; i++) try { inBytes += new FileInfo(list[i]).Length; } catch { }
        running = true;
        SetStatus("");
        openLink.Visible = false;
        failLink.Visible = false;
        lastErrors = null;
        opt.Enabled = false;
        drop.Enabled = false;
        convertBtn.Enabled = false;
        string outFolder = job.OutDir;
        if (outFolder == null || outFolder.Length == 0)
        {
            try { outFolder = Path.Combine(Path.GetDirectoryName(list[0]), "Prism"); }
            catch { outFolder = ""; }
        }
        Thread t = new Thread(delegate()
        {
            List<string> errors = new List<string>();
            List<string> outs = new List<string>();
            try
            {
                outs = Engine.Run(list, job, delegate(int i, int n, string cur)
                {
                    string line = string.Format(CultureInfo.CurrentCulture, T("prog"), i, n, cur);
                    Post(delegate { SetStatus(line); });
                }, errors);
            }
            catch (Exception ex) { errors.Add(ex.Message); }
            long outBytes = 0;
            for (int i = 0; i < outs.Count; i++) try { outBytes += new FileInfo(outs[i]).Length; } catch { }
            long finalIn = inBytes;
            long finalOut = outBytes;
            string folder = outFolder;
            List<string> errs = new List<string>(errors);
            int written = outs.Count;
            Post(delegate { FinishConvert(written, errs, finalIn, finalOut, folder); });
        });
        t.IsBackground = true;
        t.Start();
    }

    void Post(MethodInvoker m)
    {
        if (IsDisposed) return;
        try { BeginInvoke(m); } catch { }
    }

    void FinishConvert(int written, List<string> errors, long inBytes, long outBytes, string folder)
    {
        running = false;
        opt.Enabled = true;
        drop.Enabled = true;
        UpdateFiles();
        int pct = inBytes > 0 ? (int)Math.Round((outBytes - inBytes) * 100.0 / inBytes) : 0;
        SetStatus(string.Format(CultureInfo.CurrentCulture, T("doneFmt"), written, Fmt(inBytes), Fmt(outBytes), pct));
        openFolder = folder;
        if (!string.IsNullOrEmpty(folder)) openLink.Visible = true;
        if (errors.Count > 0)
        {
            lastErrors = errors;
            failLink.Text = string.Format(CultureInfo.CurrentCulture, T("failed"), errors.Count);
            failLink.Visible = true;
        }
    }

    void SetStatus(string s) { if (status != null) status.Text = s; }

    static string Fmt(long b)
    {
        if (b >= 1048576) return (b / 1048576.0).ToString("0.0", CultureInfo.CurrentCulture) + " MB";
        if (b >= 1024) return (b / 1024.0).ToString("0", CultureInfo.CurrentCulture) + " KB";
        return b.ToString(CultureInfo.CurrentCulture) + " B";
    }

    Job MakeJob()
    {
        Job j = new Job();
        int[] map = new int[] { 0, 1, 2, 6, 3, 4, 5 };
        int fi = Clamp(fmtBox.SelectedIndex, 0, 6);
        j.Format = (OutFormat)map[fi];
        int si = sizeBox.SelectedIndex;
        if (si == 1) j.ResizePercent = 75;
        else if (si == 2) j.ResizePercent = 50;
        else if (si == 3) j.ResizePercent = 25;
        else if (si == 4) j.MaxEdge = 3840;
        else if (si == 5) j.MaxEdge = 2048;
        else if (si == 6) j.MaxEdge = 1080;
        else if (si == 7) j.MaxEdge = 720;
        else if (si == 8)
        {
            int v;
            if (int.TryParse(customBox.Text.Trim(), out v) && v > 0) j.MaxEdge = v;
        }
        j.Crop = (Crop)Clamp(cropBox.SelectedIndex, 0, 4);
        j.Turn = (Turn)Clamp(rotBox.SelectedIndex, 0, 4);
        j.Quality = quality.Value;
        if (keepChk.Checked)
        {
            int kb;
            if (int.TryParse(kbBox.Text.Trim(), out kb) && kb > 0) j.TargetKB = kb;
        }
        if (outMode && outDir.Length > 0) j.OutDir = outDir;
        return j;
    }

    void OnSaveChanged(object s, EventArgs e)
    {
        if (suppressSaveCombo) return;
        if (saveBox.SelectedIndex != 1) { outMode = false; return; }
        using (FolderBrowserDialog d = new FolderBrowserDialog())
        {
            d.Description = T("choose");
            if (d.ShowDialog(this) == DialogResult.OK && d.SelectedPath.Length > 0)
            {
                outDir = d.SelectedPath;
                outMode = true;
                suppressSaveCombo = true;
                saveBox.Items[1] = outDir;
                saveBox.SelectedIndex = 1;
                suppressSaveCombo = false;
            }
            else
            {
                suppressSaveCombo = true;
                saveBox.SelectedIndex = 0;
                suppressSaveCombo = false;
                outMode = false;
            }
        }
    }

    static string SettingsPath
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Prism\settings.ini"); }
    }

    void LoadSettings()
    {
        if (shotMode) return;
        try
        {
            if (!File.Exists(SettingsPath)) return;
            string[] lines = File.ReadAllLines(SettingsPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int eq = line.IndexOf('=');
                if (eq < 1) continue;
                string k = line.Substring(0, eq).Trim();
                string v = line.Substring(eq + 1).Trim();
                try
                {
                    if (k == "format") fmt = int.Parse(v, CultureInfo.InvariantCulture);
                    else if (k == "size") sizeIdx = int.Parse(v, CultureInfo.InvariantCulture);
                    else if (k == "custom") customPx = int.Parse(v, CultureInfo.InvariantCulture);
                    else if (k == "crop") crop = int.Parse(v, CultureInfo.InvariantCulture);
                    else if (k == "rotate") rot = int.Parse(v, CultureInfo.InvariantCulture);
                    else if (k == "quality") qualityVal = int.Parse(v, CultureInfo.InvariantCulture);
                    else if (k == "keepUnder") keepUnder = v == "1";
                    else if (k == "targetKB") targetKB = int.Parse(v, CultureInfo.InvariantCulture);
                    else if (k == "outMode") outMode = v == "1";
                    else if (k == "outDir") outDir = v;
                }
                catch { }
            }
        }
        catch { }
    }

    void SaveSettings()
    {
        try
        {
            int cv;
            if (quality != null) qualityVal = quality.Value;
            if (keepChk != null) keepUnder = keepChk.Checked;
            if (kbBox != null && int.TryParse(kbBox.Text.Trim(), out cv) && cv > 0) targetKB = cv;
            if (customBox != null && int.TryParse(customBox.Text.Trim(), out cv) && cv > 0) customPx = cv;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("format=" + fmt);
            sb.AppendLine("size=" + sizeIdx);
            sb.AppendLine("custom=" + customPx);
            sb.AppendLine("crop=" + crop);
            sb.AppendLine("rotate=" + rot);
            sb.AppendLine("quality=" + qualityVal);
            sb.AppendLine("keepUnder=" + (keepUnder ? "1" : "0"));
            sb.AppendLine("targetKB=" + targetKB);
            sb.AppendLine("outMode=" + (outMode ? "1" : "0"));
            sb.AppendLine("outDir=" + outDir);
            File.WriteAllText(SettingsPath, sb.ToString());
        }
        catch { }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int v = dark ? 1 : 0;
            if (DwmSetWindowAttribute(Handle, 20, ref v, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref v, 4);
        }
        catch { }
    }
}
