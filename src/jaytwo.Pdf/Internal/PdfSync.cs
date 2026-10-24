using System;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Internal;

// D-55: a synchronous call runs the shared async-shaped path with useAsync false, so every await completes inline.
internal static class PdfSync
{
    public static T Result<T>(ValueTask<T> task)
    {
        if (!task.IsCompleted)
        {
            throw new InvalidOperationException("A synchronous read did not complete synchronously.");
        }

        return task.GetAwaiter().GetResult();
    }

    public static void Result(ValueTask task)
    {
        if (!task.IsCompleted)
        {
            throw new InvalidOperationException("A synchronous read did not complete synchronously.");
        }

        task.GetAwaiter().GetResult();
    }
}
