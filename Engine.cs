using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public enum OutFormat { Same, Jpg, Png, Bmp, Gif, Tiff, Pdf }
public enum Crop { None, Square, Portrait45, Wide169, Tall916 }
public enum Turn { None, Right90, Left90, Half180, FlipH }

public class Job
{
    public OutFormat Format = OutFormat.Same;
    public int ResizePercent = 100;
    public int MaxEdge = 0;
    public Crop Crop = Crop.None;
    public Turn Turn = Turn.None;
    public int Quality = 85;
    public int TargetKB = 0;
    public string OutDir = null;
}

public static class Engine
{
    public static readonly string[] InputExt = new string[] {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff",
        ".webp", ".heic", ".heif", ".ico", ".jfif"
    };

    private sealed class PdfEntry
    {
        public byte[] Jpeg;
        public int PixelW;
        public int PixelH;
    }

    public static bool IsImage(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string e = Path.GetExtension(path);
        if (e == null) return false;
        e = e.ToLowerInvariant();
        for (int i = 0; i < InputExt.Length; i++)
        {
            if (InputExt[i] == e) return true;
        }
        return false;
    }

    public static List<string> Run(List<string> files, Job job, Action<int, int, string> progress, List<string> errors)
    {
        List<string> written = new List<string>();
        if (errors == null) errors = new List<string>();
        if (files == null || files.Count == 0) return written;
        if (job == null) job = new Job();
        try
        {
            if (job.Format == OutFormat.Pdf)
            {
                RunPdf(files, job, progress, errors, written);
            }
            else
            {
                int total = files.Count;
                for (int i = 0; i < total; i++)
                {
                    string f = files[i];
                    CallProgress(progress, i + 1, total, f);
                    try
                    {
                        string outp = ProcessOne(f, job);
                        if (outp != null && outp.Length > 0) written.Add(outp);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(Path.GetFileName(f) + ": " + ex.Message);
                    }
                }
            }
        }
        catch (Exception ex2)
        {
            errors.Add(": " + ex2.Message);
        }
        return written;
    }

    public static string SaveClipboardImage(System.Drawing.Image img)
    {
        if (img == null) return null;
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Prism");
        Directory.CreateDirectory(dir);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string p = GetUniquePath(dir, "clip-" + stamp, ".png");
        img.Save(p, System.Drawing.Imaging.ImageFormat.Png);
        return p;
    }

    private static void CallProgress(Action<int, int, string> progress, int i, int total, string file)
    {
        if (progress == null) return;
        try { progress(i, total, Path.GetFileName(file)); }
        catch { }
    }

    private static string ProcessOne(string path, Job job)
    {
        string srcExt = Path.GetExtension(path);
        if (srcExt == null) srcExt = "";
        srcExt = srcExt.ToLowerInvariant();

        BitmapSource src = Decode(path);
        bool srcAlpha = HasAlpha(src.Format);
        BitmapSource img = ApplyOrientation(src, GetOrientation(src));
        img = ApplyTurn(img, job.Turn);
        img = ApplyCrop(img, job.Crop);

        string ext = ResolveExt(srcExt, job.Format, srcAlpha);
        int tw, th;
        TargetSize(img.PixelWidth, img.PixelHeight, job, out tw, out th);

        bool jpeg = ext == ".jpg";
        BitmapSource final = RenderFinal(img, tw, th, jpeg);
        PixelFormat dst = jpeg ? PixelFormats.Bgr24 : PixelFormats.Bgra32;
        FormatConvertedBitmap pixels = new FormatConvertedBitmap(final, dst, null, 0);
        pixels.Freeze();

        byte[] data;
        if (jpeg)
        {
            if (job.TargetKB > 0) data = EncodeJpegTarget(pixels, job.TargetKB);
            else data = EncodeJpeg(pixels, Clamp(job.Quality, 1, 100));
        }
        else if (ext == ".png") data = Encode(new PngBitmapEncoder(), pixels);
        else if (ext == ".bmp") data = Encode(new BmpBitmapEncoder(), pixels);
        else if (ext == ".gif") data = Encode(new GifBitmapEncoder(), pixels);
        else
        {
            TiffBitmapEncoder te = new TiffBitmapEncoder();
            te.Compression = TiffCompressOption.Lzw;
            data = Encode(te, pixels);
        }

        string dir = job.OutDir != null ? job.OutDir : DefaultOutDir(path);
        Directory.CreateDirectory(dir);
        string outp = GetUniquePath(dir, Path.GetFileNameWithoutExtension(path), ext);
        File.WriteAllBytes(outp, data);
        return outp;
    }

    private static void RunPdf(List<string> files, Job job, Action<int, int, string> progress, List<string> errors, List<string> written)
    {
        List<PdfEntry> entries = new List<PdfEntry>();
        int total = files.Count;
        for (int i = 0; i < total; i++)
        {
            string f = files[i];
            CallProgress(progress, i + 1, total, f);
            try
            {
                entries.Add(BuildPdfEntry(f, job));
            }
            catch (Exception ex)
            {
                errors.Add(Path.GetFileName(f) + ": " + ex.Message);
            }
        }
        if (entries.Count == 0) return;
        try
        {
            string dir = job.OutDir != null ? job.OutDir : DefaultOutDir(files[0]);
            Directory.CreateDirectory(dir);
            string baseName;
            if (total == 1) baseName = Path.GetFileNameWithoutExtension(files[0]);
            else baseName = "images-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string outp = GetUniquePath(dir, baseName, ".pdf");
            WritePdf(outp, entries);
            written.Add(outp);
        }
        catch (Exception ex)
        {
            errors.Add("PDF: " + ex.Message);
        }
    }

    private static PdfEntry BuildPdfEntry(string path, Job job)
    {
        BitmapSource src = Decode(path);
        BitmapSource img = ApplyOrientation(src, GetOrientation(src));
        img = ApplyTurn(img, job.Turn);
        img = ApplyCrop(img, job.Crop);
        int tw, th;
        TargetSize(img.PixelWidth, img.PixelHeight, job, out tw, out th);
        BitmapSource final = RenderFinal(img, tw, th, true);
        FormatConvertedBitmap rgb = new FormatConvertedBitmap(final, PixelFormats.Bgr24, null, 0);
        rgb.Freeze();
        PdfEntry e = new PdfEntry();
        e.Jpeg = EncodeJpeg(rgb, Clamp(job.Quality, 1, 100));
        e.PixelW = tw;
        e.PixelH = th;
        return e;
    }

    private static BitmapSource Decode(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            BitmapDecoder dec = BitmapDecoder.Create(ms,
                BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);
            if (dec.Frames == null || dec.Frames.Count == 0) throw new Exception("no image frames");
            BitmapSource src = dec.Frames[0];
            try { src.Freeze(); } catch { }
            return src;
        }
    }

    private static int GetOrientation(BitmapSource src)
    {
        try
        {
            BitmapMetadata md = src.Metadata as BitmapMetadata;
            if (md == null) return 1;
            object v = md.GetQuery("/app1/ifd/{ushort=274}");
            if (v == null) return 1;
            int o = Convert.ToInt32(v, CultureInfo.InvariantCulture);
            if (o < 1 || o > 8) return 1;
            return o;
        }
        catch { return 1; }
    }

    private static BitmapSource ApplyOrientation(BitmapSource src, int o)
    {
        if (o <= 1 || o > 8) return src;
        TransformGroup tg = new TransformGroup();
        switch (o)
        {
            case 2: tg.Children.Add(new ScaleTransform(-1, 1)); break;
            case 3: tg.Children.Add(new RotateTransform(180)); break;
            case 4: tg.Children.Add(new ScaleTransform(1, -1)); break;
            case 5:
                tg.Children.Add(new RotateTransform(90));
                tg.Children.Add(new ScaleTransform(-1, 1));
                break;
            case 6: tg.Children.Add(new RotateTransform(90)); break;
            case 7:
                tg.Children.Add(new ScaleTransform(-1, 1));
                tg.Children.Add(new RotateTransform(90));
                break;
            case 8: tg.Children.Add(new RotateTransform(270)); break;
            default: return src;
        }
        try
        {
            TransformedBitmap tb = new TransformedBitmap(src, tg);
            tb.Freeze();
            return tb;
        }
        catch { return src; }
    }

    private static BitmapSource ApplyTurn(BitmapSource src, Turn t)
    {
        if (t == Turn.None) return src;
        TransformGroup tg = new TransformGroup();
        if (t == Turn.Right90) tg.Children.Add(new RotateTransform(90));
        else if (t == Turn.Left90) tg.Children.Add(new RotateTransform(270));
        else if (t == Turn.Half180) tg.Children.Add(new RotateTransform(180));
        else if (t == Turn.FlipH) tg.Children.Add(new ScaleTransform(-1, 1));
        else return src;
        TransformedBitmap tb = new TransformedBitmap(src, tg);
        tb.Freeze();
        return tb;
    }

    private static BitmapSource ApplyCrop(BitmapSource src, Crop crop)
    {
        double ratio = 0.0;
        if (crop == Crop.Square) ratio = 1.0;
        else if (crop == Crop.Portrait45) ratio = 4.0 / 5.0;
        else if (crop == Crop.Wide169) ratio = 16.0 / 9.0;
        else if (crop == Crop.Tall916) ratio = 9.0 / 16.0;
        else return src;
        int w = src.PixelWidth;
        int h = src.PixelHeight;
        if (w < 1 || h < 1) return src;
        int nw = w;
        int nh = h;
        double cur = (double)w / (double)h;
        if (cur > ratio) nw = (int)Math.Round(h * ratio);
        else if (cur < ratio) nh = (int)Math.Round(w / ratio);
        if (nw > w) nw = w;
        if (nh > h) nh = h;
        if (nw < 1) nw = 1;
        if (nh < 1) nh = 1;
        if (nw == w && nh == h) return src;
        int x = (w - nw) / 2;
        int y = (h - nh) / 2;
        CroppedBitmap cb = new CroppedBitmap(src, new Int32Rect(x, y, nw, nh));
        cb.Freeze();
        return cb;
    }

    private static void TargetSize(int w, int h, Job job, out int tw, out int th)
    {
        double scale = 1.0;
        if (job.MaxEdge > 0)
        {
            int longEdge = w > h ? w : h;
            if (longEdge > job.MaxEdge) scale = (double)job.MaxEdge / (double)longEdge;
        }
        else
        {
            int pct = job.ResizePercent;
            if (pct < 1) pct = 1;
            if (pct > 100) pct = 100;
            if (pct < 100) scale = (double)pct / 100.0;
        }
        tw = (int)Math.Round((double)w * scale);
        th = (int)Math.Round((double)h * scale);
        if (tw < 1) tw = 1;
        if (th < 1) th = 1;
    }

    private static BitmapSource RenderFinal(BitmapSource src, int w, int h, bool whiteBg)
    {
        DrawingVisual dv = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
        using (DrawingContext dc = dv.RenderOpen())
        {
            Rect r = new Rect(0, 0, w, h);
            if (whiteBg) dc.DrawRectangle(Brushes.White, null, r);
            dc.DrawImage(src, r);
        }
        RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    private static BitmapSource ResizeRgb(BitmapSource src, int w, int h)
    {
        DrawingVisual dv = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
        using (DrawingContext dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
            dc.DrawImage(src, new Rect(0, 0, w, h));
        }
        RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        FormatConvertedBitmap fcb = new FormatConvertedBitmap(rtb, PixelFormats.Bgr24, null, 0);
        fcb.Freeze();
        return fcb;
    }

    private static string ResolveExt(string srcExt, OutFormat fmt, bool alpha)
    {
        if (fmt == OutFormat.Jpg) return ".jpg";
        if (fmt == OutFormat.Png) return ".png";
        if (fmt == OutFormat.Bmp) return ".bmp";
        if (fmt == OutFormat.Gif) return ".gif";
        if (fmt == OutFormat.Tiff) return ".tif";
        if (srcExt == ".jpg" || srcExt == ".jpeg" || srcExt == ".jfif") return ".jpg";
        if (srcExt == ".png") return ".png";
        if (srcExt == ".bmp") return ".bmp";
        if (srcExt == ".gif") return ".gif";
        if (srcExt == ".tif" || srcExt == ".tiff") return ".tif";
        if (srcExt == ".webp" || srcExt == ".ico") return alpha ? ".png" : ".jpg";
        return ".jpg";
    }

    private static bool HasAlpha(PixelFormat pf)
    {
        try { return pf.Masks.Count >= 4; }
        catch { return false; }
    }

    private static byte[] Encode(BitmapEncoder enc, BitmapSource src)
    {
        enc.Frames.Add(BitmapFrame.Create(src));
        using (MemoryStream ms = new MemoryStream())
        {
            enc.Save(ms);
            return ms.ToArray();
        }
    }

    private static byte[] EncodeJpeg(BitmapSource src, int quality)
    {
        quality = Clamp(quality, 1, 100);
        JpegBitmapEncoder enc = new JpegBitmapEncoder();
        enc.QualityLevel = quality;
        enc.Frames.Add(BitmapFrame.Create(src));
        using (MemoryStream ms = new MemoryStream())
        {
            enc.Save(ms);
            return ms.ToArray();
        }
    }

    private static bool SearchJpeg(BitmapSource src, long targetBytes, out byte[] result)
    {
        result = null;
        byte[] q10 = EncodeJpeg(src, 10);
        if (q10.Length > targetBytes) return false;
        result = q10;
        int lo = 11;
        int hi = 95;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            byte[] m = EncodeJpeg(src, mid);
            if (m.Length <= targetBytes)
            {
                result = m;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return true;
    }

    private static byte[] EncodeJpegTarget(BitmapSource src, int targetKB)
    {
        long target = (long)targetKB * 1024L;
        BitmapSource cur = src;
        for (int attempt = 0; attempt <= 6; attempt++)
        {
            byte[] best;
            if (SearchJpeg(cur, target, out best)) return best;
            if (attempt == 6) break;
            int nw = (int)Math.Round(cur.PixelWidth * 0.8);
            int nh = (int)Math.Round(cur.PixelHeight * 0.8);
            if (nw < 1) nw = 1;
            if (nh < 1) nh = 1;
            if (nw == cur.PixelWidth && nh == cur.PixelHeight) break;
            cur = ResizeRgb(cur, nw, nh);
        }
        return EncodeJpeg(cur, 10);
    }

    private static int Clamp(int v, int lo, int hi)
    {
        if (v < lo) return lo;
        if (v > hi) return hi;
        return v;
    }

    private static string DefaultOutDir(string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir)) dir = ".";
        return Path.Combine(dir, "Prism");
    }

    private static string GetUniquePath(string dir, string baseName, string ext)
    {
        string p = Path.Combine(dir, baseName + ext);
        if (!File.Exists(p)) return p;
        int n = 2;
        while (true)
        {
            p = Path.Combine(dir, baseName + " (" + n.ToString(CultureInfo.InvariantCulture) + ")" + ext);
            if (!File.Exists(p)) return p;
            n++;
        }
    }

    private static void WritePdf(string outPath, List<PdfEntry> pages)
    {
        int n = pages.Count;
        int objCount = 2 + n * 3;
        using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
        {
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] pre = new byte[] {
                    0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A,
                    0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A
                };
                ms.Write(pre, 0, pre.Length);
                long[] offsets = new long[objCount + 1];

                WriteObjHeader(ms, 1, offsets);
                WriteAscii(ms, "<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

                StringBuilder kids = new StringBuilder();
                for (int k = 0; k < n; k++)
                {
                    int pageObj = 3 + k * 3;
                    kids.Append(pageObj.ToString(CultureInfo.InvariantCulture));
                    kids.Append(" 0 R ");
                }
                WriteObjHeader(ms, 2, offsets);
                WriteAscii(ms, "<< /Type /Pages /Kids [" + kids.ToString() + "] /Count " +
                    n.ToString(CultureInfo.InvariantCulture) + " >>\nendobj\n");

                for (int k = 0; k < n; k++)
                {
                    int pageObj = 3 + k * 3;
                    int contentObj = pageObj + 1;
                    int imageObj = pageObj + 2;
                    PdfEntry e = pages[k];
                    double pw = e.PixelW * 0.75;
                    double ph = e.PixelH * 0.75;

                    WriteObjHeader(ms, pageObj, offsets);
                    WriteAscii(ms, string.Format(CultureInfo.InvariantCulture,
                        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0:0.###} {1:0.###}] /Resources << /XObject << /Im0 {2} 0 R >> >> /Contents {3} 0 R >>\nendobj\n",
                        pw, ph, imageObj, contentObj));

                    string content = string.Format(CultureInfo.InvariantCulture,
                        "q {0:0.###} 0 0 {1:0.###} 0 0 cm /Im0 Do Q", pw, ph);
                    byte[] cb = Encoding.ASCII.GetBytes(content);
                    WriteObjHeader(ms, contentObj, offsets);
                    WriteAscii(ms, "<< /Length " + cb.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
                    ms.Write(cb, 0, cb.Length);
                    WriteAscii(ms, "\nendstream\nendobj\n");

                    WriteObjHeader(ms, imageObj, offsets);
                    WriteAscii(ms, string.Format(CultureInfo.InvariantCulture,
                        "<< /Type /XObject /Subtype /Image /Width {0} /Height {1} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {2} >>\nstream\n",
                        e.PixelW, e.PixelH, e.Jpeg.Length));
                    ms.Write(e.Jpeg, 0, e.Jpeg.Length);
                    WriteAscii(ms, "\nendstream\nendobj\n");
                }

                long xrefStart = ms.Position;
                StringBuilder xref = new StringBuilder();
                xref.Append("xref\n0 ");
                xref.Append((objCount + 1).ToString(CultureInfo.InvariantCulture));
                xref.Append("\n0000000000 65535 f \n");
                for (int i = 1; i <= objCount; i++)
                {
                    xref.Append(offsets[i].ToString("D10", CultureInfo.InvariantCulture));
                    xref.Append(" 00000 n \n");
                }
                xref.Append("trailer\n<< /Size ");
                xref.Append((objCount + 1).ToString(CultureInfo.InvariantCulture));
                xref.Append(" /Root 1 0 R >>\nstartxref\n");
                xref.Append(xrefStart.ToString(CultureInfo.InvariantCulture));
                xref.Append("\n%%EOF\n");
                WriteAscii(ms, xref.ToString());

                ms.Position = 0;
                ms.WriteTo(fs);
            }
        }
    }

    private static void WriteObjHeader(MemoryStream ms, int num, long[] offsets)
    {
        offsets[num] = ms.Position;
        WriteAscii(ms, num.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
    }

    private static void WriteAscii(Stream s, string text)
    {
        byte[] b = Encoding.ASCII.GetBytes(text);
        s.Write(b, 0, b.Length);
    }
}
