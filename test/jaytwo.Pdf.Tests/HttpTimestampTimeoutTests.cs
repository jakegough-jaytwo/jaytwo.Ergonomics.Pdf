using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.Signatures;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-121: these tests bound how long HttpTimestampAuthority takes to time out or to honor a cancellation. Run in parallel
// with the rest of the suite, other tests held xUnit's eight test threads and the thread pool long enough to delay the
// library's continuations and the test's own resumption by seconds to minutes, so they run in a collection of their own,
// after the parallel tests. Each takes the elapsed time when the library's task completes, not when the method resumes.
// make unit-test also runs them after the other framework hosts finish (W-123).
[CollectionDefinition(nameof(HttpTimestampTimeoutTests), DisableParallelization = true)]
[Collection(nameof(HttpTimestampTimeoutTests))]
[Trait(Lane.Trait, Lane.Clock)]
public class HttpTimestampTimeoutTests
{
    [Fact]
    public async Task stalled_timestamp_response_body_still_times_out()
    {
        var client = new HttpClient(new StreamHandler(new StallingStream())) { Timeout = TimeSpan.FromMilliseconds(500) };
        var authority = new HttpTimestampAuthority(client, new Uri("https://tsa.example/stamp"));
        var watch = Stopwatch.StartNew();
        Task<byte[]> exchange = authority.GetTimeStampTokenAsync(new byte[] { 1 }, CancellationToken.None).AsTask();
        Task<TimeSpan> ended = Ended(exchange, watch);

        TaskCanceledException canceled = await Assert.ThrowsAsync<TaskCanceledException>(() => exchange);
        Assert.IsType<TimeoutException>(canceled.InnerException);
        Assert.InRange(await ended, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task stalled_timestamp_response_headers_still_time_out()
    {
        var client = new HttpClient(new StallingHandler()) { Timeout = TimeSpan.FromMilliseconds(500) };
        var authority = new HttpTimestampAuthority(client, new Uri("https://tsa.example/stamp"));
        var watch = Stopwatch.StartNew();
        Task<byte[]> exchange = authority.GetTimeStampTokenAsync(new byte[] { 1 }, CancellationToken.None).AsTask();
        Task<TimeSpan> ended = Ended(exchange, watch);

        TaskCanceledException canceled = await Assert.ThrowsAsync<TaskCanceledException>(() => exchange);
        Assert.IsType<TimeoutException>(canceled.InnerException);
        Assert.InRange(await ended, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    // W-98: the library's deadline source never fires, as when a loaded thread pool has not yet run its callback, so
    // HttpClient's own timer cancels the headers first. On .NET Framework that exception has no inner exception. The
    // message is the library's, which shows the library classified the timeout rather than passing HttpClient's on.
    [Fact]
    public async Task header_timeout_is_classified_when_http_client_cancels_before_the_deadline_fires()
    {
        var client = new HttpClient(new StallingHandler()) { Timeout = TimeSpan.FromMilliseconds(500) };
        var authority = new HttpTimestampAuthority(client, new Uri("https://tsa.example/stamp"), _ => new CancellationTokenSource());

        TaskCanceledException canceled = await Assert.ThrowsAsync<TaskCanceledException>(async () => await authority.GetTimeStampTokenAsync(new byte[] { 1 }, CancellationToken.None));
        Assert.IsType<TimeoutException>(canceled.InnerException);
        Assert.StartsWith("The timestamp request was canceled because", canceled.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task cancel_pending_timestamp_requests_is_not_reported_as_a_timeout()
    {
        var handler = new ObservableStallingHandler();
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        var authority = new HttpTimestampAuthority(client, new Uri("https://tsa.example/stamp"));
        Task<Exception> pending = Record.ExceptionAsync(async () => await authority.GetTimeStampTokenAsync(new byte[] { 1 }, CancellationToken.None));
        Assert.True(handler.Started.Wait(TimeSpan.FromSeconds(10)));

        client.CancelPendingRequests();

        Exception canceled = Assert.IsAssignableFrom<OperationCanceledException>(await pending);
        Assert.False(canceled.InnerException is TimeoutException);
    }

    [Fact]
    public async Task stalled_timestamp_body_that_ignores_the_token_is_aborted_by_disposing_the_response()
    {
        var body = new TokenIgnoringStream();
        var client = new HttpClient(new StreamHandler(body)) { Timeout = TimeSpan.FromMilliseconds(500) };
        var authority = new HttpTimestampAuthority(client, new Uri("https://tsa.example/stamp"));
        var watch = Stopwatch.StartNew();
        Task<byte[]> exchange = authority.GetTimeStampTokenAsync(new byte[] { 1 }, CancellationToken.None).AsTask();
        Task<TimeSpan> ended = Ended(exchange, watch);

        TaskCanceledException canceled = await Assert.ThrowsAsync<TaskCanceledException>(() => exchange);
        TimeoutException timeout = Assert.IsType<TimeoutException>(canceled.InnerException);
        Assert.IsType<ObjectDisposedException>(timeout.InnerException);
        Assert.True(body.Disposed);
        Assert.InRange(await ended, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task caller_cancellation_of_timestamp_headers_is_not_reported_as_a_timeout()
    {
        var handler = new ObservableStallingHandler();
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        var authority = new HttpTimestampAuthority(client, new Uri("https://tsa.example/stamp"));
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> exchange = authority.GetTimeStampTokenAsync(new byte[] { 1 }, cancellation.Token).AsTask();
        Assert.True(handler.Started.Wait(TimeSpan.FromSeconds(10)));
        var watch = new Stopwatch();
        Task<TimeSpan> ended = Ended(exchange, watch);
        watch.Start();

        cancellation.Cancel();

        OperationCanceledException canceled = Assert.IsAssignableFrom<OperationCanceledException>(await Record.ExceptionAsync(() => exchange));
        Assert.Equal(cancellation.Token, canceled.CancellationToken);
        Assert.False(canceled is TaskCanceledException);
        Assert.False(canceled.InnerException is TimeoutException);
        Assert.InRange(await ended, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    // The HttpClient deadline is far past the caller's, and the body fails on its own only after 30 seconds, so ending
    // well before either, with the stream's disposal as the cause, shows the caller's cancellation aborted the body.
    [Fact]
    public async Task caller_cancellation_aborts_a_timestamp_body_that_ignores_the_token()
    {
        var body = new TokenIgnoringStream();
        var client = new HttpClient(new StreamHandler(body)) { Timeout = TimeSpan.FromMinutes(5) };
        var authority = new HttpTimestampAuthority(client, new Uri("https://tsa.example/stamp"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var watch = Stopwatch.StartNew();
        Task<byte[]> exchange = authority.GetTimeStampTokenAsync(new byte[] { 1 }, cancellation.Token).AsTask();
        Task<TimeSpan> ended = Ended(exchange, watch);

        OperationCanceledException canceled = await Assert.ThrowsAsync<OperationCanceledException>(() => exchange);

        Assert.Equal(cancellation.Token, canceled.CancellationToken);
        Assert.IsType<ObjectDisposedException>(canceled.InnerException);
        Assert.True(body.Disposed);
        Assert.InRange(await ended, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    private static Task<TimeSpan> Ended(Task exchange, Stopwatch watch)
    {
        return exchange.ContinueWith(_ => watch.Elapsed, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private sealed class StallingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            throw new IOException("The stalled headers were not cancelled.");
        }
    }

    private sealed class ObservableStallingHandler : HttpMessageHandler
    {
        public ManualResetEventSlim Started { get; } = new ManualResetEventSlim();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.Set();
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            throw new IOException("The canceled headers resumed without cancellation.");
        }
    }

    private sealed class StallingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Thread.Sleep(TimeSpan.FromSeconds(30));
            throw new IOException("The stalled body was read synchronously.");
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            throw new IOException("The stalled body was not cancelled.");
        }

#if !NETFRAMEWORK
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            throw new IOException("The stalled body was not cancelled.");
        }
#endif

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // Models a network stream that ignores the read token. Only Dispose ends a pending read early.
    private sealed class TokenIgnoringStream : Stream
    {
        private readonly TaskCompletionSource<int> _pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Disposed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(
                delay => _pending.TrySetException(new IOException("The stalled body was not disposed.")),
                TaskScheduler.Default);
            return _pending.Task;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            _pending.TrySetException(new ObjectDisposedException(nameof(TokenIgnoringStream)));
            base.Dispose(disposing);
        }
    }
}
