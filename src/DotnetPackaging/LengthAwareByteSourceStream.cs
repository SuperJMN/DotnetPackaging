using System.Threading.Channels;
using Zafiro.DivineBytes;

namespace DotnetPackaging;

internal sealed class LengthAwareByteSourceStream : Stream
{
    private readonly IByteSource source;
    private readonly Channel<byte[]> chunks;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task producer;
    private byte[]? current;
    private int currentOffset;
    private long position;
    private bool disposed;

    public LengthAwareByteSourceStream(IByteSource source, long length)
    {
        this.source = source;
        Length = length;
        chunks = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(4)
        {
            SingleReader = true,
            SingleWriter = true
        });
        producer = Task.Run(Produce);
    }

    public override bool CanRead => !disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length { get; }

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0)
        {
            return 0;
        }

        ThrowIfDisposed();

        var totalRead = 0;
        while (totalRead < count)
        {
            if (!EnsureCurrentChunk())
            {
                break;
            }

            var available = current!.Length - currentOffset;
            var toCopy = Math.Min(count - totalRead, available);
            Buffer.BlockCopy(current, currentOffset, buffer, offset + totalRead, toCopy);

            currentOffset += toCopy;
            totalRead += toCopy;
            position += toCopy;

            if (currentOffset == current.Length)
            {
                current = null;
                currentOffset = 0;
            }
        }

        return totalRead;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            cancellation.Cancel();
            chunks.Writer.TryComplete();
            try
            {
                producer.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
            }

            cancellation.Dispose();
        }

        base.Dispose(disposing);
    }

    private bool EnsureCurrentChunk()
    {
        while (current is null || currentOffset == current.Length)
        {
            current = null;
            currentOffset = 0;

            try
            {
                if (!chunks.Reader.WaitToReadAsync(cancellation.Token).AsTask().GetAwaiter().GetResult())
                {
                    return false;
                }

                if (chunks.Reader.TryRead(out var next))
                {
                    current = next;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return true;
    }

    private void Produce()
    {
        try
        {
            using var subscription = source.Bytes.Subscribe(
                chunk => chunks.Writer.WriteAsync(chunk, cancellation.Token).AsTask().GetAwaiter().GetResult(),
                ex => chunks.Writer.TryComplete(ex),
                () => chunks.Writer.TryComplete());

            chunks.Reader.Completion.Wait(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            chunks.Writer.TryComplete();
        }
        catch (Exception ex)
        {
            chunks.Writer.TryComplete(ex);
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(LengthAwareByteSourceStream));
        }
    }
}
