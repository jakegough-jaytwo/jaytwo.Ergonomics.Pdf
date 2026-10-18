namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// What the user password may do. An unencrypted document allows every operation.
/// The owner password is not limited by these values, and opening with it does not change them.
/// </summary>
public sealed class PdfPermissions
{
    private const int PrintBit = 3;
    private const int ModifyBit = 4;
    private const int CopyBit = 5;
    private const int AnnotateBit = 6;
    private const int FillFormsBit = 9;
    private const int AccessibilityBit = 10;
    private const int AssembleBit = 11;
    private const int HighQualityPrintBit = 12;
    private const int ExtendedPermissionRevision = 3;

    internal PdfPermissions(
        bool canPrint,
        bool canPrintHighQuality,
        bool canModify,
        bool canCopy,
        bool canAnnotate,
        bool canFillForms,
        bool canAssemble,
        bool canExtractForAccessibility)
    {
        CanPrint = canPrint;
        CanPrintHighQuality = canPrintHighQuality;
        CanModify = canModify;
        CanCopy = canCopy;
        CanAnnotate = canAnnotate;
        CanFillForms = canFillForms;
        CanAssemble = canAssemble;
        CanExtractForAccessibility = canExtractForAccessibility;
    }

    /// <summary>
    /// Gets whether the user password may print.
    /// </summary>
    public bool CanPrint { get; }

    /// <summary>
    /// Gets whether the user password may print a faithful copy.
    /// Revision 2 follows <see cref="CanPrint"/>. Revision 3 and later also require the high-quality bit.
    /// </summary>
    public bool CanPrintHighQuality { get; }

    /// <summary>
    /// Gets whether the user password may change the document, apart from annotations, form fill, and page assembly.
    /// </summary>
    public bool CanModify { get; }

    /// <summary>
    /// Gets whether the user password may copy or extract text and graphics.
    /// </summary>
    public bool CanCopy { get; }

    /// <summary>
    /// Gets whether the user password may add or modify annotations.
    /// On revision 3 and later, filling existing fields can be allowed separately through <see cref="CanFillForms"/>.
    /// </summary>
    public bool CanAnnotate { get; }

    /// <summary>
    /// Gets whether the user password may fill existing form fields.
    /// Revision 2 follows <see cref="CanAnnotate"/>. Revision 3 and later also allow the fill-in bit.
    /// </summary>
    public bool CanFillForms { get; }

    /// <summary>
    /// Gets whether the user password may insert, rotate, or delete pages.
    /// Revision 2 follows <see cref="CanModify"/>. Revision 3 and later use the assemble bit on its own.
    /// </summary>
    public bool CanAssemble { get; }

    /// <summary>
    /// Gets whether the user password may extract text and graphics for accessibility.
    /// Revision 2 follows <see cref="CanCopy"/>. Revision 3 and later also allow the accessibility bit.
    /// </summary>
    public bool CanExtractForAccessibility { get; }

    internal static PdfPermissions AllowAll()
    {
        return new PdfPermissions(true, true, true, true, true, true, true, true);
    }

    internal static PdfPermissions FromStandard(int revision, int permissions)
    {
        bool print = Has(permissions, PrintBit);
        bool modify = Has(permissions, ModifyBit);
        bool copy = Has(permissions, CopyBit);
        bool annotate = Has(permissions, AnnotateBit);
        if (revision < ExtendedPermissionRevision)
        {
            return new PdfPermissions(print, print, modify, copy, annotate, annotate, modify, copy);
        }

        return new PdfPermissions(
            print,
            print && Has(permissions, HighQualityPrintBit),
            modify,
            copy,
            annotate,
            annotate || Has(permissions, FillFormsBit),
            Has(permissions, AssembleBit),
            copy || Has(permissions, AccessibilityBit));
    }

    private static bool Has(int permissions, int bit)
    {
        int mask = 1 << (bit - 1);
        return (permissions & mask) == mask;
    }
}
