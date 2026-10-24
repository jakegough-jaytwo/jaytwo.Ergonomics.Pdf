using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace jaytwo.Pdf.Tests;

// A renderer the G6 checks run against: Poppler (pdftotext and pdftoppm) or MuPDF (mutool draw). Both rasterize to
// binary PPM at 72 dpi from the crop box and report words with top-left coordinates in points.
internal sealed class RenderTool
{
    private readonly string _directory;
    private readonly bool _mupdf;

    private RenderTool(string name, string directory, bool mupdf)
    {
        Name = name;
        _directory = directory;
        _mupdf = mupdf;
    }

    public string Name { get; }

    // How far a channel may differ before a pixel counts as changed. MuPDF paints a widget annotation and page content
    // with slightly different anti-aliasing, mostly at glyph edges: 12 pixels of the FAA 8710-1 page 5 differ by more
    // than 96 in a channel, where a misplaced appearance changes pixels by their full contrast.
    public int PixelTolerance => _mupdf ? 96 : 0;

    public static IReadOnlyList<RenderTool> All()
    {
        return new[] { Poppler(), MuPdf() };
    }

    public static RenderTool Poppler()
    {
        string? directory = Environment.GetEnvironmentVariable("JAYTWO_POPPLER");
        if (!string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, ExternalTool.Executable("pdftotext"))))
        {
            return new RenderTool("poppler", directory!, mupdf: false);
        }

        string located = ExternalTool.Locate("JAYTWO_PDFTOTEXT", ExternalTool.Executable("pdftotext"));
        return new RenderTool("poppler", Path.GetDirectoryName(located) ?? located, mupdf: false);
    }

    public static RenderTool MuPdf()
    {
        string located = ExternalTool.Locate("JAYTWO_MUTOOL", ExternalTool.Executable("mutool"));
        return new RenderTool("mupdf", Path.GetDirectoryName(located) ?? located, mupdf: true);
    }

    public string Text(string pdf)
    {
        using Process process = _mupdf
            ? Start("mutool", "draw", "-q", "-F", "txt", "-o", "-", pdf)
            : Start("pdftotext", "-layout", pdf, "-");
        return ExternalTool.Output(process);
    }

    public List<Word> Words(string pdf)
    {
        if (!_mupdf)
        {
            using Process process = Start("pdftotext", "-bbox", pdf, "-");
            return ReadPopplerWords(ExternalTool.Output(process));
        }

        using Process draw = Start("mutool", "draw", "-q", "-F", "stext", "-o", "-", pdf);
        return ReadMuPdfWords(ExternalTool.Output(draw));
    }

    // Writes name-<page>.ppm for every page; PageRaster names the files.
    public void RasterPages(string directory, string name, string pdf, bool hideAnnotations = false)
    {
        string prefix = Path.Combine(directory, name);
        if (_mupdf)
        {
            // mutool draw has no option to hide annotations. A flattened page has no widgets left to hide.
            using Process draw = Start("mutool", "draw", "-q", "-r", "72", "-o", prefix + "-%d.ppm", pdf);
            ExternalTool.Output(draw);
            return;
        }

        var args = new List<string> { "-cropbox", "-r", "72" };
        if (hideAnnotations)
        {
            args.Add("-hide-annotations");
        }

        args.Add(pdf);
        args.Add(prefix);
        using Process process = Start("pdftoppm", args.ToArray());
        ExternalTool.Output(process);
    }

    // pdftoppm pads the page number to the width of the page count; mutool does not.
    public string PageRaster(string directory, string name, int page, int pages)
    {
        string number = page.ToString(CultureInfo.InvariantCulture);
        if (!_mupdf)
        {
            number = number.PadLeft(pages.ToString(CultureInfo.InvariantCulture).Length, '0');
        }

        return Path.Combine(directory, name + "-" + number + ".ppm");
    }

    public byte[] ReadRaster(string directory, string name, int page, int pages)
    {
        return File.ReadAllBytes(PageRaster(directory, name, page, pages));
    }

    public byte[] FirstPage(string directory, string name, string pdf)
    {
        RasterPages(directory, name, pdf);
        return ReadRaster(directory, name, 1, 1);
    }

    private static List<Word> ReadPopplerWords(string bbox)
    {
        var words = new List<Word>();
        foreach (Match match in Regex.Matches(bbox, "<word xMin=\"([^\"]+)\" yMin=\"([^\"]+)\" xMax=\"([^\"]+)\" yMax=\"([^\"]+)\">([^<]*)</word>"))
        {
            words.Add(new Word(
                WebUtility.HtmlDecode(match.Groups[5].Value),
                double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture)));
        }

        return words;
    }

    // Each <line> holds <char c="..." quad="x0 y0 x1 y1 x2 y2 x3 y3"/>; a word runs between spaces.
    private static List<Word> ReadMuPdfWords(string stext)
    {
        var words = new List<Word>();
        foreach (Match line in Regex.Matches(stext, "<line [^>]*>(.*?)</line>", RegexOptions.Singleline))
        {
            var text = new StringBuilder();
            double xMin = double.MaxValue;
            double yMin = double.MaxValue;
            double xMax = double.MinValue;
            double yMax = double.MinValue;
            foreach (Match character in Regex.Matches(line.Groups[1].Value, "<char c=\"([^\"]*)\" quad=\"([^\"]+)\""))
            {
                string value = WebUtility.HtmlDecode(character.Groups[1].Value);
                if (value == " ")
                {
                    Flush(words, text, ref xMin, ref yMin, ref xMax, ref yMax);
                    continue;
                }

                double[] quad = character.Groups[2].Value.Split(' ').Select(part => double.Parse(part, CultureInfo.InvariantCulture)).ToArray();
                for (int i = 0; i + 1 < quad.Length; i += 2)
                {
                    xMin = Math.Min(xMin, quad[i]);
                    xMax = Math.Max(xMax, quad[i]);
                    yMin = Math.Min(yMin, quad[i + 1]);
                    yMax = Math.Max(yMax, quad[i + 1]);
                }

                text.Append(value);
            }

            Flush(words, text, ref xMin, ref yMin, ref xMax, ref yMax);
        }

        return words;
    }

    private static void Flush(List<Word> words, StringBuilder text, ref double xMin, ref double yMin, ref double xMax, ref double yMax)
    {
        if (text.Length > 0)
        {
            words.Add(new Word(text.ToString(), xMin, yMin, xMax, yMax));
            text.Clear();
        }

        xMin = double.MaxValue;
        yMin = double.MaxValue;
        xMax = double.MinValue;
        yMax = double.MinValue;
    }

    private Process Start(string tool, params string[] args)
    {
        var info = new ProcessStartInfo
        {
            FileName = Path.Combine(_directory, ExternalTool.Executable(tool)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.Environment["PATH"] = _directory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        for (int i = 0; i < args.Length; i++)
        {
            TestCompat.AddArgument(info, args[i]);
        }

        return ExternalTool.Start(info);
    }

    internal readonly struct Word
    {
        public Word(string text, double xMin, double yMin, double xMax, double yMax)
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
