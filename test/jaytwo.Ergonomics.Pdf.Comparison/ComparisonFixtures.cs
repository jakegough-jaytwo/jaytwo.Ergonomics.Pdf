using System;
using System.Diagnostics;
using System.IO;

namespace jaytwo.Ergonomics.Pdf.Comparison;

// The G7 fixtures, written once into JAYTWO_COMPARISON_DIR (default: a fixed temp directory) and shared by every
// process. `make compare-itext` rewrites them in a process of its own, so no measured process pays for them. The
// filled and attached fixtures are written by this library, and both libraries then read the same bytes.
internal static class ComparisonFixtures
{
    public const int FormPages = 500;
    public const int FlattenPages = 1000;
    public const long PayloadLength = (1024L * 1024 * 1024) + 4097;
    public const string AttachmentName = "payload.csv";

    // Every date a workload writes.
    public static readonly DateTimeOffset FixedDate = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly string Root = Resolve();

    // A measured process that also wrote a fixture has a peak working set that includes the writing.
    public static bool GeneratedInProcess { get; private set; }

    public static string Dense()
    {
        return Ensure("dense.pdf", G7Fixtures.WriteDense);
    }

    public static string EncryptedDense()
    {
        return Ensure("dense-aes256.pdf", path => Qpdf("--object-streams=generate", "--encrypt", "user-secret", "owner-secret", "256", "--", Dense(), path));
    }

    public static string Form()
    {
        return Ensure("form.pdf", path => ObjectStreamForm(path, FormPages, "Five Hundred"));
    }

    public static string FilledForm()
    {
        return Ensure("form-filled.pdf", path => Fill(Form(), path, FormPages));
    }

    public static string FilledFlattenForm()
    {
        string source = Ensure("flatten.pdf", path => ObjectStreamForm(path, FlattenPages, "One Thousand"));
        return Ensure("flatten-filled.pdf", path => Fill(source, path, FlattenPages));
    }

    public static string Payload()
    {
        return Ensure("payload.csv", path =>
        {
            using var payload = CreateFile(path);
            G7Fixtures.WriteRecords(payload, PayloadLength, 0x5EED0250UL);
        });
    }

    // qpdf --check needs a page with /Resources.
    public static string Carrier()
    {
        return Ensure("carrier.pdf", path => File.WriteAllBytes(path, PdfBuilder.Basic(pageExtra: " /Resources << >>").Finish()));
    }

    public static string AttachedCarrier()
    {
        return Ensure("carrier-attached.pdf", path =>
        {
            using var pdf = OpenRead(Carrier());
            using var content = OpenRead(Payload());
            using var document = PdfDocument.Open(pdf, leaveOpen: true);
            document.Attachments.Add(AttachmentName, content, leaveOpen: true, AttachmentOptions());
            using var destination = CreateFile(path);
            document.SaveIncremental(destination, leaveOpen: true);
        });
    }

    public static PdfAttachmentOptions AttachmentOptions()
    {
        return new PdfAttachmentOptions { Description = "G7 payload", MimeType = "text/csv", Compress = true, CreationDate = FixedDate, ModificationDate = FixedDate };
    }

    // A workload's output path. The run deletes every output before each pass.
    public static string Output(string name)
    {
        return Path.Combine(OutputDirectory(), name);
    }

    public static void ClearOutput()
    {
        string directory = OutputDirectory();
        Directory.Delete(directory, recursive: true);
        Directory.CreateDirectory(directory);
    }

    public static FileStream OpenRead(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public static FileStream CreateFile(string path)
    {
        return new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
    }

    // Writes every fixture afresh, so a run never measures fixtures from an older generator.
    public static void Prepare()
    {
        foreach (string file in Directory.GetFiles(Root))
        {
            File.Delete(file);
        }

        Dense();
        EncryptedDense();
        Form();
        FilledForm();
        FilledFlattenForm();
        Payload();
        Carrier();
        AttachedCarrier();
    }

    private static string Resolve()
    {
        string? configured = Environment.GetEnvironmentVariable("JAYTWO_COMPARISON_DIR");
        string root = string.IsNullOrWhiteSpace(configured) ? Path.Combine(Path.GetTempPath(), "jaytwo-ergonomics-pdf-comparison") : configured!;
        Directory.CreateDirectory(root);
        return root;
    }

    private static string OutputDirectory()
    {
        string directory = Path.Combine(Root, "out");
        Directory.CreateDirectory(directory);
        return directory;
    }

    // A fixture is written under a temporary name and renamed, so an interrupted run never leaves a partial file.
    private static string Ensure(string name, Action<string> write)
    {
        string path = Path.Combine(Root, name);
        if (File.Exists(path))
        {
            return path;
        }

        string partial = path + ".partial";
        File.Delete(partial);
        write(partial);
        File.Move(partial, path);
        GeneratedInProcess = true;
        return path;
    }

    private static void ObjectStreamForm(string path, int pages, string title)
    {
        string classic = path + ".classic";
        File.Delete(classic);
        G7Fixtures.WriteForm(classic, pages, title);
        G7Fixtures.ObjectStreamCopy(classic, path);
        File.Delete(classic);
    }

    // qpdf rewrites the filled file as one object-stream revision. iText 9.8.0 cannot open this library's classic
    // incremental section over a cross-reference stream (W-111 Notes), so both libraries get the rewritten file.
    private static void Fill(string source, string path, int pages)
    {
        string filled = path + ".filled";
        File.Delete(filled);
        using (var file = OpenRead(source))
        using (var document = PdfDocument.Open(file, leaveOpen: true))
        {
            document.AcroForm.Fill(G7Fixtures.TextValues(pages));
            using var destination = CreateFile(filled);
            document.SaveIncremental(destination, leaveOpen: true);
        }

        G7Fixtures.ObjectStreamCopy(filled, path);
        File.Delete(filled);
    }

    private static void Qpdf(params string[] args)
    {
        string qpdf = ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
        using Process process = ExternalTool.Start(qpdf, args);
        ExternalTool.Output(process);
    }
}
