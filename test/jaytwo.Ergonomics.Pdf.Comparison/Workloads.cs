namespace jaytwo.Ergonomics.Pdf.Comparison;

// The workload names both libraries report under, and how many passes each runs: one cold pass, then warm passes.
internal static class Workloads
{
    public const string DenseInspect = "Dense 1 GiB: inspect";
    public const string DenseEdit = "Dense 1 GiB: metadata edit";
    public const string DenseSign = "Dense 1 GiB: sign";
    public const string AttachmentAdd = "1 GiB attachment: add and save";
    public const string AttachmentExtract = "1 GiB attachment: extract";
    public const string FormExtraction = "500-page form: extraction";
    public const string FormEdit = "500-page form: metadata edit";
    public const string FormFill = "500-page form: bulk fill of 500 fields";
    public const string FormSign = "500-page form: sign";
    public const string Flatten = "1,000-page form: flatten of 2,000 fields";
    public const string EncryptedUser = "Encrypted 1 GiB: inspect, user password";
    public const string EncryptedOwner = "Encrypted 1 GiB: inspect, owner password";

    public const int GibibytePasses = 3;
    public const int AttachmentPasses = 2;
    public const int FormPasses = 5;
}
