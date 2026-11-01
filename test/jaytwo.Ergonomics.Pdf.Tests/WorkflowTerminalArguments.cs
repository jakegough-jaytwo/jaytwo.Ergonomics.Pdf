using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Argument cases shared by the WORKFLOW-BEGIN-01 tests of every terminal.
internal static class WorkflowTerminalArguments
{
    public static Stream? Ineligible(string kind)
    {
        switch (kind)
        {
            case "null":
                return null;
            case "read-only":
                return new MemoryStream(new byte[0], writable: false);
            case "non-seekable":
                return new NonSeekableStream();
            case "non-empty":
                return new MemoryStream(new byte[] { 1 });
            default:
                return new MemoryStream { Position = 5 };
        }
    }

    public static string? BadPath(string kind)
    {
        return kind switch
        {
            "null" => null,
            "empty" => string.Empty,
            _ => "bad\0name.pdf",
        };
    }

    public static void AssertRejected(Action terminal)
    {
        Exception? thrown = Record.Exception(terminal);
        Assert.True(thrown is ArgumentException, "Expected an argument error, got " + thrown);
    }

    public static async Task AssertRejectedAsync(Func<Task> terminal)
    {
        Exception? thrown = await Record.ExceptionAsync(terminal);
        Assert.True(thrown is ArgumentException, "Expected an argument error, got " + thrown);
    }

    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
