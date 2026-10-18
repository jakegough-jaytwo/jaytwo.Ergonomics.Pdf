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

    private static byte[] Fill(byte[] pdf)
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Name", "Ada"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetCheckBox("Agree", true));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetChoice("Color", "Blue"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetRadio("Station", "Yes"));
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
            string candidate = Path.Combine(directory, "pdftotext.exe");
            if (File.Exists(candidate))
            {
                return directory;
            }
        }

        string located = ExternalTool.Locate("JAYTWO_PDFTOTEXT", "pdftotext.exe");
        return Path.GetDirectoryName(located) ?? located;
    }

    private static string PdfToText(string poppler, string pdf)
    {
        using Process process = Start(poppler, Path.Combine(poppler, "pdftotext.exe"), "-layout", pdf, "-");
        return ExternalTool.Output(process);
    }

    private static byte[] Raster(string poppler, string directory, string name, string pdf)
    {
        string prefix = Path.Combine(directory, name);
        using Process process = Start(poppler, Path.Combine(poppler, "pdftoppm.exe"), "-r", "72", pdf, prefix);
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
            info.ArgumentList.Add(args[i]);
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

    private static byte[] FillMatrix(byte[] pdf)
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Seen", "Seen"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Cafe", "Caf\u00e9"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Mark", "A\u2022B"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Left", "Align"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Center", "Align"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Right", "Align"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Long", "ABCDEFGHIJKLMNOPQRSTUVWXYZ"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Clip", "WIDEMARK"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Small", "Small"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Large", "Large"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Wide", "Wide"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Tall", "One\nTwo\nThree"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Turn", "Rotated"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Crop", "Cropped"));
            Assert.False(document.AcroForm.NeedAppearances);
            document.SaveIncremental(stream);
        }

        return stream.ToArray();
    }

    private static string PdfToBBox(string poppler, string pdf)
    {
        using Process process = Start(poppler, Path.Combine(poppler, "pdftotext.exe"), "-bbox", pdf, "-");
        return ExternalTool.Output(process);
    }

    private static void RasterPages(string poppler, string directory, string name, string pdf)
    {
        string prefix = Path.Combine(directory, name);
        using Process process = Start(poppler, Path.Combine(poppler, "pdftoppm.exe"), "-cropbox", "-r", "72", pdf, prefix);
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
