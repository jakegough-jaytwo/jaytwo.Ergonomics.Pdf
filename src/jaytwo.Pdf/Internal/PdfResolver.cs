using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Internal;

// Resolves an indirect reference for the parser, in the mode of the parse that needs it (D-55).
internal delegate ValueTask<PdfObject> PdfResolver(PdfObject.Ref reference, bool useAsync, CancellationToken cancellationToken);
