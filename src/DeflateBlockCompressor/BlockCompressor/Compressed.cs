using System;
using System.Buffers;
using System.Reactive.Linq;
using ZLibDotNet;

namespace BlockCompressor;

public static class Compressed
{
    /// <summary>
    /// Default block size (64KB)
    /// </summary>
    public const int DefaultBlockSize = 64 * 1024; // 64KB
    
    /// <summary>
    /// Compresses an observable stream of byte arrays into blocks using zlib's deflate algorithm
    /// configured in the style of makemsix/makeappx
    /// </summary>
    public static IObservable<DeflateBlock> Blocks(
        IObservable<byte[]> input,
        int compressionLevel = ZLib.Z_BEST_COMPRESSION,
        int uncompressedBlockSize = DefaultBlockSize)
    {
        return Observable.Create<DeflateBlock>(observer =>
        {
            // Use ArrayPool to reduce GC pressure
            byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(uncompressedBlockSize * 2);
            int bytesInBuffer = 0;
            
            var wrapper = new ZStreamWrapper();
            
            var subscription = input.Subscribe(
                onNext: incomingBytes =>
                {
                    try
                    {
                        // Check if we need a larger buffer
                        if (bytesInBuffer + incomingBytes.Length > rentedBuffer.Length)
                        {
                            // Get a new buffer of appropriate size
                            int newSize = Math.Max(rentedBuffer.Length * 2, bytesInBuffer + incomingBytes.Length);
                            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
                            
                            // Copy existing data to new buffer
                            Buffer.BlockCopy(rentedBuffer, 0, newBuffer, 0, bytesInBuffer);
                            
                            // Return old buffer to pool
                            ArrayPool<byte>.Shared.Return(rentedBuffer);
                            
                            // Use new buffer
                            rentedBuffer = newBuffer;
                        }
                        
                        // Copy incoming bytes to our buffer
                        Buffer.BlockCopy(incomingBytes, 0, rentedBuffer, bytesInBuffer, incomingBytes.Length);
                        bytesInBuffer += incomingBytes.Length;
                        
                        // Process complete blocks
                        ProcessCompleteBlocks();
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                },
                onError: ex => 
                {
                    try
                    {
                        ArrayPool<byte>.Shared.Return(rentedBuffer);
                    }
                    finally
                    {
                        observer.OnError(ex);
                    }
                },
                onCompleted: () =>
                {
                    try
                    {
                        // Process any remaining bytes if we have some
                        if (bytesInBuffer > 0)
                        {
                            ProcessBlock(rentedBuffer.AsSpan(0, bytesInBuffer).ToArray());
                        }
                        
                        // Process the final zlib block
                        byte[] finalData = wrapper.DeflateFinish();
                        
                        if (finalData.Length > 0)
                        {
                            var finalBlock = new DeflateBlock
                            {
                                CompressedData = finalData,
                                OriginalData = [],
                            };
                            
                            observer.OnNext(finalBlock);
                        }
                        
                        observer.OnCompleted();
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                    finally
                    {
                        // Return the buffer to the pool
                        ArrayPool<byte>.Shared.Return(rentedBuffer);
                    }
                });
                
            // Local function to process all complete blocks
            void ProcessCompleteBlocks()
            {
                // Process as many complete blocks as we can
                while (bytesInBuffer >= uncompressedBlockSize)
                {
                    // Create a block from the buffer
                    byte[] blockData = new byte[uncompressedBlockSize];
                    Buffer.BlockCopy(rentedBuffer, 0, blockData, 0, uncompressedBlockSize);
                    
                    // Process it
                    ProcessBlock(blockData);
                    
                    // Move remaining data to the start of the buffer
                    if (bytesInBuffer > uncompressedBlockSize)
                    {
                        Buffer.BlockCopy(
                            rentedBuffer, 
                            uncompressedBlockSize, 
                            rentedBuffer, 
                            0, 
                            bytesInBuffer - uncompressedBlockSize);
                    }
                    
                    bytesInBuffer -= uncompressedBlockSize;
                }
            }
            
            // Local function to process a single block
            void ProcessBlock(byte[] blockData)
            {
                try
                {
                    // Compress the block using our wrapper
                    byte[] compressedData = wrapper.Deflate(
                        blockData, 
                        compressionLevel);
                        
                    // Create and emit the compressed block
                    var block = new DeflateBlock
                    {
                        CompressedData = compressedData,
                        OriginalData = blockData,
                    };
                    observer.OnNext(block);
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            }
            
            return subscription;
        });
    }
}