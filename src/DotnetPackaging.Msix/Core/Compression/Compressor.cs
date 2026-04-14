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

    /// <summary>
    /// Splits the source into 64KB blocks and compresses them using a single
    /// DeflateStream with Flush() (Z_SYNC_FLUSH) between blocks, matching the
    /// MSIX SDK's approach (Z_FULL_FLUSH). The result is a single valid deflate
    /// stream that can be decompressed end-to-end. A final block with empty
    /// OriginalData is emitted for the stream terminator (Z_FINISH).
    /// </summary>
    public static IObservable<MsixBlock> CompressionBlocks(this IObservable<byte[]> bytes)
    {
        return Observable.Create<MsixBlock>(observer =>
        {
            var output = new MemoryStream();
            var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true);

            return bytes.Flatten().Buffer(BlockSize).Subscribe(
                chunk =>
                {
                    try
                    {
                        var original = chunk.ToArray();
                        var startPos = output.Position;

                        deflate.Write(original, 0, original.Length);
                        deflate.Flush();

                        var compressed = output.GetBuffer().AsSpan((int)startPos, (int)(output.Position - startPos)).ToArray();

                        observer.OnNext(new MsixBlock
                        {
                            OriginalData = original,
                            CompressedData = compressed,
                        });
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                },
                ex =>
                {
                    try { deflate.Dispose(); } catch { }
                    observer.OnError(ex);
                },
                () =>
                {
                    try
                    {
                        var finishStart = output.Position;
                        deflate.Dispose();
                        var finishEnd = output.Position;

                        if (finishEnd > finishStart)
                        {
                            var finishBytes = output.GetBuffer().AsSpan((int)finishStart, (int)(finishEnd - finishStart)).ToArray();
                            observer.OnNext(new MsixBlock
                            {
                                OriginalData = Array.Empty<byte>(),
                                CompressedData = finishBytes,
                            });
                        }

                        observer.OnCompleted();
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                });
        });
    }
}
