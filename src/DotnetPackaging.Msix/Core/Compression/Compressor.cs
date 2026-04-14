using System.IO.Pipelines;
using System.Reactive.Disposables;
using Zafiro.Reactive;

namespace DotnetPackaging.Msix.Core.Compression;

internal static class Compressor
{
    private const int BlockSize = 64 * 1024;

    public static async Task<byte[]> Uncompress(byte[] compressedData)
    {
        using (var ms = new MemoryStream(compressedData))
        {
            await using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
            {
                using (var output = new MemoryStream())
                {
                    await deflate.CopyToAsync(output);
                    return output.ToArray();
                }
            }
        }
    }

    public static IObservable<byte[]> Compressed(this IObservable<byte[]> source, CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        return Observable.Create<byte[]>(observer =>
        {
            var pipeOptions = new PipeOptions(pauseWriterThreshold: 1024 * 1024);
            var pipe = new Pipe(pipeOptions);

            var readSubscription = pipe.Reader.AsStream().ToObservable().Subscribe(observer);

            var deflateStream = new DeflateStream(pipe.Writer.AsStream(), compressionLevel, leaveOpen: true);

            var subscription = source.Subscribe(
                block =>
                {
                    try
                    {
                        var array = block.ToArray();
                        deflateStream.Write(array, 0, array.Length);
                        deflateStream.Flush();
                        pipe.Writer.FlushAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            deflateStream.Dispose();
                        }
                        catch
                        {
                        }

                        pipe.Writer.Complete(ex);
                    }
                },
                ex =>
                {
                    try
                    {
                        deflateStream.Dispose();
                    }
                    catch
                    {
                    }

                    pipe.Writer.Complete(ex);
                },
                () =>
                {
                    try
                    {
                        deflateStream.Close();
                        pipe.Writer.Complete();
                    }
                    catch (Exception ex)
                    {
                        pipe.Writer.Complete(ex);
                    }
                });

            return new CompositeDisposable(subscription, readSubscription, Disposable.Create(() =>
            {
                try
                {
                    deflateStream.Dispose();
                }
                catch
                {
                }

                pipe.Writer.Complete();
            }));
        });
    }

    public static IObservable<MsixBlock> CompressionBlocks(this IObservable<byte[]> bytes)
    {
        return bytes.Flatten().Buffer(BlockSize).Select(chunk =>
        {
            var original = chunk.ToArray();
            var compressed = DeflateBlock(original);
            return new MsixBlock
            {
                OriginalData = original,
                CompressedData = compressed,
            };
        });
    }

    private static byte[] DeflateBlock(byte[] data)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }
}
