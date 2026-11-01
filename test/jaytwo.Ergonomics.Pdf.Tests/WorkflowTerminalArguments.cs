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

    // A writable stream that cannot seek and cannot report a length or position, like a response body or a pipe.
    public sealed class ForwardOnlyStream : Stream
    {
        private readonly MemoryStream _written = new MemoryStream();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public byte[] Written => _written.ToArray();

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _written.Write(buffer, offset, count);
        }
    }

    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
