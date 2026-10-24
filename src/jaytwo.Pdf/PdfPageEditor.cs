namespace jaytwo.Pdf;

/// <summary>
/// Changes a page's rotation. Content streams are not read.
/// </summary>
public sealed class PdfPageEditor
{
    private readonly PdfDocumentEditor _editor;
    private readonly int _objectId;
    private int _rotation;

    internal PdfPageEditor(PdfDocumentEditor editor, int objectId, int number, int rotation)
    {
        _editor = editor;
        _objectId = objectId;
        Number = number;
        _rotation = rotation;
    }

    /// <summary>
    /// Gets the 1-based page number.
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Gets or sets the page rotation in degrees. Positive and negative multiples of 90 are normalized to 0, 90, 180, or 270.
    /// </summary>
    /// <remarks>
    /// Setting writes /Rotate on this page object, overriding an inherited value; boxes and field rectangles stay in the
    /// same user-space coordinates. Setting the value the page already holds directly changes nothing. On an unsigned
    /// document the change removes /UR3 usage rights. A refused change does not change the document. The change is written
    /// by the next save or signature.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">The value is not a multiple of 90.</exception>
    /// <exception cref="PdfCertificationLockedException">The document is certified, at any DocMDP level.</exception>
    /// <exception cref="PdfSignatureConflictException">The document has an approval signature or a document timestamp and <see cref="PdfDocumentEditor.AllowBreakingSignatures"/> is not set.</exception>
    /// <exception cref="PdfEncryptionRestrictedException">The user password does not allow modifying the document.</exception>
    public int Rotation
    {
        get => _rotation;
        set => _rotation = _editor.Engine.SetPageRotation(_objectId, value);
    }
}
