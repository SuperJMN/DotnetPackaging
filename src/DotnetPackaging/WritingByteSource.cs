using System.Reactive.Disposables;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

public static class WritingByteSource
{
    public static IByteSource FromWriter(Func<Stream, Task<Result>> writer, Maybe<long> length = default)
    {
        var bytes = Observable.Create<byte[]>(observer =>
        {
            var cancellation = new CancellationTokenSource();
            var task = Task.Run(async () =>
            {
                try
                {
                    await using var stream = new ObserverStream(observer, cancellation.Token);
                    var result = await writer(stream).ConfigureAwait(false);
                    if (result.IsFailure)
                    {
                        observer.OnError(new InvalidOperationException(result.Error));
                        return;
                    }

                    observer.OnCompleted();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            }, cancellation.Token);

            return Disposable.Create(() =>
            {
                cancellation.Cancel();
                cancellation.Dispose();
                _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            });
        });

        return ByteSource.FromByteObservable(bytes).WithLength(length);
    }

    private sealed class ObserverStream(IObserver<byte[]> observer, CancellationToken cancellationToken) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        public override Task FlushAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            cancellationToken.ThrowIfCancellationRequested();

            if (count == 0)
            {
                return;
            }

            var chunk = new byte[count];
            Buffer.BlockCopy(buffer, offset, chunk, 0, count);
            observer.OnNext(chunk);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            cancellationToken.ThrowIfCancellationRequested();

            if (buffer.Length > 0)
            {
                observer.OnNext(buffer.ToArray());
            }

            return ValueTask.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
