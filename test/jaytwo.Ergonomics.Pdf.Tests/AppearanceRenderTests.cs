using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using jaytwo.Ergonomics.Pdf.AcroForm;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class AppearanceRenderTests
{
    [Fact]
    public void poppler_renders_generated_text_choice_checkbox_and_radio_appearances()
    {
        string poppler = LocatePoppler();
        byte[] original = File.ReadAllBytes(ExternalTool.Fixture("pypdf-acroform.pdf"));
        byte[] filled = Fill(original);
        string directory = ExternalTool.TempDirectory();
        try
        {
            string beforePath = Path.Combine(directory, "before.pdf");
            string afterPath = Path.Combine(directory, "after.pdf");
            File.WriteAllBytes(beforePath, original);
            File.WriteAllBytes(afterPath, filled);
            string beforeText = PdfToText(poppler, beforePath);
            string afterText = PdfToText(poppler, afterPath);
            Assert.DoesNotContain("Ada", beforeText, StringComparison.Ordinal);
            Assert.Contains("Ada", afterText, StringComparison.Ordinal);
            Assert.Contains("Blue", afterText, StringComparison.Ordinal);

            byte[] beforeRaster = Raster(poppler, directory, "before", beforePath);
            byte[] afterRaster = Raster(poppler, directory, "after", afterPath);
            Assert.True(ChangedPixels(beforeRaster, afterRaster, 72, 300, 700, 724) > 20);
            Assert.True(ChangedPixels(beforeRaster, afterRaster, 72, 220, 620, 644) > 20);
            Assert.True(ChangedPixels(beforeRaster, afterRaster, 72, 90, 660, 678) > 20);
            Assert.True(ChangedPixels(beforeRaster, afterRaster, 72, 98, 580, 598) > 10);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void poppler_fits_auto_sized_text_and_paints_widget_colors()
    {
        string poppler = LocatePoppler();
        byte[] original = BuildAutoSizeAndColorForm();
        byte[] filled = FillAutoSizeAndColorForm(original);
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "auto-size-and-color.pdf");
            File.WriteAllBytes(path, filled);
            List<PlacedWord> words = ReadWords(PdfToBBox(poppler, path));
            PlacedWord fitted = Assert.Single(words, word => word.Text == "ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            Assert.InRange(fitted.XMin, 40, 44);
            Assert.InRange(fitted.XMax, 130, 180);

            RasterPages(poppler, directory, "auto-size-and-color", path);
            byte[] raster = ReadRaster(directory, "auto-size-and-color", 1);
            Assert.True(CountDominantColor(raster, 320, 350, 74, 88, 0) > 200, "red background");
            Assert.True(CountDominantColor(raster, 220, 360, 68, 72, 2) > 100, "blue border");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void poppler_checks_visibility_encoding_alignment_clipping_rotation_and_crop()
    {
        string poppler = LocatePoppler();
        byte[] blank = BuildMatrix();
        byte[] filled = FillMatrix(blank);
        string directory = ExternalTool.TempDirectory();
        try
        {
            string beforePath = Path.Combine(directory, "matrix-before.pdf");
            string afterPath = Path.Combine(directory, "matrix-after.pdf");
            File.WriteAllBytes(beforePath, blank);
            File.WriteAllBytes(afterPath, filled);
            string beforeText = PdfToText(poppler, beforePath);
            string afterText = PdfToText(poppler, afterPath);
            Assert.DoesNotContain("Seen", beforeText, StringComparison.Ordinal);
            Assert.Contains("Seen", afterText, StringComparison.Ordinal);
            Assert.Contains("Caf\u00e9", afterText, StringComparison.Ordinal);
            Assert.Contains("A\u2022B", afterText, StringComparison.Ordinal);
            Assert.Contains("ABCDEFGHIJKLMNOPQRSTUVWXYZ", afterText, StringComparison.Ordinal);
            Assert.Contains("One", afterText, StringComparison.Ordinal);
            Assert.Contains("Two", afterText, StringComparison.Ordinal);
            Assert.Contains("Three", afterText, StringComparison.Ordinal);
            Assert.Contains("Rotated", afterText, StringComparison.Ordinal);
            Assert.Contains("Cropped", afterText, StringComparison.Ordinal);

            string bbox = PdfToBBox(poppler, afterPath);
            List<PlacedWord> words = ReadWords(bbox);
            PlacedWord[] aligned = words.Where(word => word.Text == "Align").OrderBy(word => word.XMin).ToArray();
            Assert.Equal(3, aligned.Length);
            Assert.InRange(aligned[0].XMin, 30, 60);
            Assert.InRange(aligned[1].XMin, 174, 180);
            Assert.InRange(aligned[2].XMin, 280, 340);
            PlacedWord small = Assert.Single(words, word => word.Text == "Small");
            PlacedWord large = Assert.Single(words, word => word.Text == "Large");
            double smallHeight = Math.Abs(small.YMax - small.YMin);
            double largeHeight = Math.Abs(large.YMax - large.YMin);
            Assert.True(largeHeight > smallHeight * 1.5, "font box " + smallHeight.ToString(CultureInfo.InvariantCulture) + " " + largeHeight.ToString(CultureInfo.InvariantCulture));
            PlacedWord wide = Assert.Single(words, word => word.Text == "Wide");
            Assert.True(wide.XMax - wide.XMin < 80, "wide word span");

            RasterPages(poppler, directory, "before", beforePath);
            RasterPages(poppler, directory, "after", afterPath);
            byte[] beforePage = ReadRaster(directory, "before", 1);
            byte[] afterPage = ReadRaster(directory, "after", 1);
            Assert.True(ChangedPixels(beforePage, afterPage, 72, 280, 700, 724) > 10);
            Assert.True(DarkPixels(afterPage, 44, 86, 412, 428) > 10, "clipped field has no ink");
            Assert.Equal(0, DarkPixels(afterPage, 94, 140, 410, 430));

            (int turnWidth, int turnHeight, _) = ReadPpm(ReadRaster(directory, "after", 2));
            Assert.Equal(792, turnWidth);
            Assert.Equal(612, turnHeight);
            Assert.True(CountDark(ReadRaster(directory, "before", 2)) < 5);
            Assert.True(CountDark(ReadRaster(directory, "after", 2)) > 20);

            byte[] crop = ReadRaster(directory, "after", 3);
            (int cropWidth, int cropHeight, _) = ReadPpm(crop);
            Assert.Equal(400, cropWidth);
            Assert.Equal(540, cropHeight);
            int cropInk = LeftmostDark(crop);
            Assert.InRange(cropInk, 30, 70);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Spec W4 (W-76): after a fill of a hybrid XFA corpus form removes /XFA, the generated appearance is drawn on the page.
    // Poppler ignores XFA either way, so this shows the AcroForm value that XFA-aware viewers now fall back to.
    [Theory]
    [InlineData("irs-fw9-2024-03", "topmostSubform[0].Page1[0].f1_01[0]")]
    [InlineData("va-21-4138", "form1[0].#subform[0].International_Phone_Number[0]")]
    public void poppler_renders_the_fill_of_a_hybrid_xfa_corpus_form(string id, string field)
    {
        string poppler = LocatePoppler();
        byte[] original = File.ReadAllBytes(CorpusManifest.Get(id).FullPath);
        using var stream = new MemoryStream();
        stream.Write(original, 0, original.Length);
        PdfField widget;
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.True(document.AcroForm.IsHybridXfa);
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill(field, "Zephyr"));
            Assert.False(document.AcroForm.IsHybridXfa);
            widget = Assert.Single(document.AcroForm.GetFields(field));
            document.SaveIncremental(stream);
        }

        Assert.Equal(1, widget.PageNumber);
        string directory = ExternalTool.TempDirectory();
        try
        {
            string beforePath = Path.Combine(directory, "before.pdf");
            string afterPath = Path.Combine(directory, "after.pdf");
            File.WriteAllBytes(beforePath, original);
            File.WriteAllBytes(afterPath, stream.ToArray());
            Assert.DoesNotContain("Zephyr", PdfToText(poppler, beforePath), StringComparison.Ordinal);
            Assert.Contains("Zephyr", PdfToText(poppler, afterPath), StringComparison.Ordinal);

            byte[] beforeRaster = Raster(poppler, directory, "before", beforePath);
            byte[] afterRaster = Raster(poppler, directory, "after", afterPath);
            PdfRect rect = widget.Rect;
            Assert.True(ChangedPixels(beforeRaster, afterRaster, (int)rect.Llx, (int)Math.Ceiling(rect.Urx), (int)rect.Lly, (int)Math.Ceiling(rect.Ury)) > 20);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] Fill(byte[] pdf)
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", "Ada"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Agree", true));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Color", "Blue"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Station", "Yes"));
            Assert.False(document.AcroForm.NeedAppearances);
            document.SaveIncremental(stream);
        }

        return stream.ToArray();
    }

    private static string LocatePoppler()
    {
        string? directory = Environment.GetEnvironmentVariable("JAYTWO_POPPLER");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            string candidate = Path.Combine(directory, ExternalTool.Executable("pdftotext"));
            if (File.Exists(candidate))
            {
                return directory;
            }
        }

        string located = ExternalTool.Locate("JAYTWO_PDFTOTEXT", ExternalTool.Executable("pdftotext"));
        return Path.GetDirectoryName(located) ?? located;
    }

    private static string PdfToText(string poppler, string pdf)
    {
        using Process process = Start(poppler, Path.Combine(poppler, ExternalTool.Executable("pdftotext")), "-layout", pdf, "-");
        return ExternalTool.Output(process);
    }

    private static byte[] Raster(string poppler, string directory, string name, string pdf)
    {
        string prefix = Path.Combine(directory, name);
        using Process process = Start(poppler, Path.Combine(poppler, ExternalTool.Executable("pdftoppm")), "-r", "72", pdf, prefix);
        ExternalTool.Output(process);
        return File.ReadAllBytes(prefix + "-1.ppm");
    }

    private static int ChangedPixels(byte[] before, byte[] after, int x0, int x1, int pdfY0, int pdfY1)
    {
        (int width, int height, int offset) = ReadPpm(before);
        int y0 = height - pdfY1;
        int y1 = height - pdfY0;
        int changed = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                int pixel = offset + (((y * width) + x) * 3);
                if (before[pixel] != after[pixel] || before[pixel + 1] != after[pixel + 1] || before[pixel + 2] != after[pixel + 2])
                {
                    changed++;
                }
            }
        }

        return changed;
    }

    private static (int Width, int Height, int Offset) ReadPpm(byte[] ppm)
    {
        Assert.Equal((byte)'P', ppm[0]);
        Assert.Equal((byte)'6', ppm[1]);
        int index = 2;
        int width = ReadNumber(ppm, ref index);
        int height = ReadNumber(ppm, ref index);
        ReadNumber(ppm, ref index);
        return (width, height, index);
    }

    private static int ReadNumber(byte[] ppm, ref int index)
    {
        while (index < ppm.Length && (ppm[index] == ' ' || ppm[index] == '\n' || ppm[index] == '\r' || ppm[index] == '\t' || ppm[index] == '#'))
        {
            if (ppm[index] == '#')
            {
                while (index < ppm.Length && ppm[index] != '\n')
                {
                    index++;
                }
            }
            else
            {
                index++;
            }
        }

        int value = 0;
        while (index < ppm.Length && ppm[index] >= '0' && ppm[index] <= '9')
        {
            value = (value * 10) + (ppm[index] - '0');
            index++;
        }

        if (index < ppm.Length && (ppm[index] == ' ' || ppm[index] == '\n' || ppm[index] == '\r' || ppm[index] == '\t'))
        {
            index++;
        }

        return value;
    }

    private static Process Start(string poppler, string fileName, params string[] args)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.Environment["PATH"] = poppler + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        for (int i = 0; i < args.Length; i++)
        {
            TestCompat.AddArgument(info, args[i]);
        }

        return ExternalTool.Start(info);
    }

    private static byte[] BuildMatrix()
    {
        string[] page1 =
        {
            "<< /Subtype /Widget /FT /Tx /T (Seen) /Rect [72 700 280 724] /DA (/Helv 12 Tf 0 g) /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Cafe) /Rect [72 660 280 684] /DA (/Helv 12 Tf 0 g) /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Mark) /Rect [72 620 280 644] /DA (/Helv 12 Tf 0 g) /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Left) /Rect [40 560 340 584] /Q 0 /DA (/Helv 12 Tf 0 g) /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Center) /Rect [40 520 340 544] /Q 1 /DA (/Helv 12 Tf 0 g) /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Right) /Rect [40 480 340 504] /Q 2 /DA (/Helv 12 Tf 0 g) /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Long) /Rect [40 430 560 454] /DA (/Helv 12 Tf 0 g) /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Clip) /Rect [40 360 90 384] /DA (/Helv 12 Tf 0 g) /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Small) /Rect [40 310 220 326] /DA (/Helv 8 Tf 0 g) /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Large) /Rect [40 250 400 290] /DA (/Helv 18 Tf 0 g) /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Wide) /Rect [40 200 520 224] /DA (/Helv 12 Tf 0 g) /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Tall) /Rect [40 40 220 160] /Ff 4096 /DA (/Helv 12 Tf 0 g) /P 3 0 R >>",
        };
        string[] page2 =
        {
            "<< /Subtype /Widget /FT /Tx /T (Turn) /Rect [100 400 320 428] /DA (/Helv 14 Tf 0 g) /P 4 0 R >>",
        };
        string[] page3 =
        {
            "<< /Subtype /Widget /FT /Tx /T (Crop) /Rect [120 300 320 328] /DA (/Helv 12 Tf 0 g) /P 5 0 R >>",
        };
        string[] widgets = page1.Concat(page2).Concat(page3).ToArray();
        var fields = new StringBuilder();
        var annots = new[] { new StringBuilder(), new StringBuilder(), new StringBuilder() };
        int[] counts = { page1.Length, page2.Length, page3.Length };
        int pageIndex = 0;
        int onPage = 0;
        for (int i = 0; i < widgets.Length; i++)
        {
            if (onPage == counts[pageIndex])
            {
                pageIndex++;
                onPage = 0;
            }

            if (fields.Length > 0)
            {
                fields.Append(' ');
            }

            if (annots[pageIndex].Length > 0)
            {
                annots[pageIndex].Append(' ');
            }

            int id = i + 6;
            fields.Append(id.ToString(CultureInfo.InvariantCulture)).Append(" 0 R");
            annots[pageIndex].Append(id.ToString(CultureInfo.InvariantCulture)).Append(" 0 R");
            onPage++;
        }

        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [" + fields.ToString() + "] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 3 /Kids [3 0 R 4 0 R 5 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [" + annots[0].ToString() + "] >>");
        builder.Indirect(4, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Rotate 90 /Annots [" + annots[1].ToString() + "] >>");
        builder.Indirect(5, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /CropBox [80 100 480 640] /Annots [" + annots[2].ToString() + "] >>");
        for (int i = 0; i < widgets.Length; i++)
        {
            builder.Indirect(i + 6, widgets[i]);
        }

        return builder.Finish();
    }

    private static byte[] BuildAutoSizeAndColorForm()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Fit) /Rect [40 700 180 724] /DA (/Helv 0 Tf 0 g) /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T (Color) /Rect [220 700 360 724] /DA (/Helv 12 Tf 0 g) /MK << /BG [1 0 0] /BC [0 0 1] >> /BS << /W 2 /S /S >> /P 3 0 R >>");
        return builder.Finish();
    }

    private static byte[] FillAutoSizeAndColorForm(byte[] pdf)
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Fit", "ABCDEFGHIJKLMNOPQRSTUVWXYZ"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Color", "Color"));
            document.SaveIncremental(stream);
        }

        return stream.ToArray();
    }

    private static byte[] FillMatrix(byte[] pdf)
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Seen", "Seen"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Cafe", "Caf\u00e9"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Mark", "A\u2022B"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Left", "Align"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Center", "Align"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Right", "Align"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Long", "ABCDEFGHIJKLMNOPQRSTUVWXYZ"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Clip", "WIDEMARK"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Small", "Small"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Large", "Large"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Wide", "Wide"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Tall", "One\nTwo\nThree"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Turn", "Rotated"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Crop", "Cropped"));
            Assert.False(document.AcroForm.NeedAppearances);
            document.SaveIncremental(stream);
        }

        return stream.ToArray();
    }

    private static string PdfToBBox(string poppler, string pdf)
    {
        using Process process = Start(poppler, Path.Combine(poppler, ExternalTool.Executable("pdftotext")), "-bbox", pdf, "-");
        return ExternalTool.Output(process);
    }

    private static void RasterPages(string poppler, string directory, string name, string pdf)
    {
        string prefix = Path.Combine(directory, name);
        using Process process = Start(poppler, Path.Combine(poppler, ExternalTool.Executable("pdftoppm")), "-cropbox", "-r", "72", pdf, prefix);
        ExternalTool.Output(process);
    }

    private static byte[] ReadRaster(string directory, string name, int page)
    {
        return File.ReadAllBytes(Path.Combine(directory, name + "-" + page.ToString(CultureInfo.InvariantCulture) + ".ppm"));
    }

    private static List<PlacedWord> ReadWords(string bbox)
    {
        var words = new List<PlacedWord>();
        foreach (Match match in Regex.Matches(bbox, "<word xMin=\"([^\"]+)\" yMin=\"([^\"]+)\" xMax=\"([^\"]+)\" yMax=\"([^\"]+)\">([^<]*)</word>"))
        {
            words.Add(new PlacedWord(
                match.Groups[5].Value,
                double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture)));
        }

        return words;
    }

    private static int DarkPixels(byte[] ppm, int x0, int x1, int y0, int y1)
    {
        (int width, int height, int offset) = ReadPpm(ppm);
        int count = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                int pixel = offset + (((y * width) + x) * 3);
                if (ppm[pixel] < 250 || ppm[pixel + 1] < 250 || ppm[pixel + 2] < 250)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int CountDark(byte[] ppm)
    {
        (int width, int height, _) = ReadPpm(ppm);
        return DarkPixels(ppm, 0, width, 0, height);
    }

    private static int CountDominantColor(byte[] ppm, int x0, int x1, int y0, int y1, int channel)
    {
        (int width, _, int offset) = ReadPpm(ppm);
        int count = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                int pixel = offset + (((y * width) + x) * 3);
                int firstOther = (channel + 1) % 3;
                int secondOther = (channel + 2) % 3;
                if (ppm[pixel + channel] > 150 && ppm[pixel + firstOther] < 100 && ppm[pixel + secondOther] < 100)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int LeftmostDark(byte[] ppm)
    {
        (int width, int height, int offset) = ReadPpm(ppm);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int pixel = offset + (((y * width) + x) * 3);
                if (ppm[pixel] < 250 || ppm[pixel + 1] < 250 || ppm[pixel + 2] < 250)
                {
                    return x;
                }
            }
        }

        return -1;
    }

    private readonly struct PlacedWord
    {
        public PlacedWord(string text, double xMin, double yMin, double xMax, double yMax)
        {
            Text = text;
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
        }

        public string Text { get; }

        public double XMin { get; }

        public double YMin { get; }

        public double XMax { get; }

        public double YMax { get; }
    }
}
