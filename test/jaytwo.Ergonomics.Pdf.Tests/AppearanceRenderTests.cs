using System;
using System.Diagnostics;
using System.IO;
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
}
