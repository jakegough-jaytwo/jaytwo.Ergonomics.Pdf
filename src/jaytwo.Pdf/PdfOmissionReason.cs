namespace jaytwo.Pdf;

/// <summary>Reasons an annotation was omitted during assembly.</summary>
public enum PdfOmissionReason
{
    /// <summary>A signature widget was removed.</summary>
    SignatureWidget,

    /// <summary>The annotation subtype is unsupported.</summary>
    UnsupportedAnnotation,

    /// <summary>The link action or URI is unsupported.</summary>
    UnsupportedLink,

    /// <summary>The destination is missing or outside the selection.</summary>
    MissingDestination,

    /// <summary>The caller requested removal of external links.</summary>
    ExternalLinkDropped,

    /// <summary>The appearance is unsupported or exceeds a local budget.</summary>
    UnsupportedAppearance,
}
