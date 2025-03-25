using ZLibDotNet;
namespace BlockCompressor;

/// <summary>
/// Provides a wrapper around ZLib compression functionality with simplified interface.
/// </summary>
internal class ZStreamWrapper
{
    /// <summary>
    /// Gets the result code of the last compression operation.
    /// </summary>
    public int LastResult { get; private set; }
    
    /// <summary>
    /// Gets the total number of input bytes processed.
    /// </summary>
    public uint TotalIn { get; private set; }
    
    /// <summary>
    /// Gets the total number of output bytes generated.
    /// </summary>
    public uint TotalOut { get; private set; }
    
    /// <summary>
    /// Gets the Adler-32 checksum of the data.
    /// </summary>
    public uint Adler { get; private set; }
    
    // Buffers for input and output
    private byte[] inputBuffer;
    private byte[] outputBuffer;
    
    // ZLib instance
    private readonly ZLib zlib = new ZLib();
    
    /// <summary>
    /// Compresses the specified input bytes using DEFLATE algorithm.
    /// </summary>
    /// <param name="input">The byte array to compress.</param>
    /// <param name="compressionLevel">The compression level to use, defaults to best compression.</param>
    /// <returns>A new byte array containing the compressed data.</returns>
    /// <exception cref="InvalidOperationException">Thrown when ZLib initialization fails.</exception>
    public byte[] Deflate(byte[] input, int compressionLevel = ZLib.Z_BEST_COMPRESSION)
    {
        // Estimation of the maximum possible size for compressed data
        uint compressedSizeEstimate = (uint)input.Length +
                                      ((uint)input.Length >> 12) + ((uint)input.Length >> 14) +
                                      ((uint)input.Length >> 25) + 13;
        
        byte[] output = new byte[compressedSizeEstimate];
        
        // We use a local method that works with ref ZStream
        DeflateInternal(input, output, compressionLevel);
        
        // Trim the array to the actual compressed size
        byte[] result = new byte[TotalOut];
        Array.Copy(output, result, result.Length);
        
        return result;
    }
    
    /// <summary>
    /// Finalizes a deflate stream by flushing any pending output.
    /// </summary>
    /// <returns>A byte array containing any final compressed data, or an empty array if none.</returns>
    public byte[] DeflateFinish()
    {
        byte[] emptyInput = Array.Empty<byte>();
        byte[] finalOutput = new byte[32]; // Enough for the end marker
        
        DeflateFinishInternal(emptyInput, finalOutput);
        
        if (TotalOut > 0)
        {
            byte[] result = new byte[TotalOut];
            Array.Copy(finalOutput, result, result.Length);
            return result;
        }
        
        return Array.Empty<byte>();
    }
    
    /// <summary>
    /// Performs the actual DEFLATE compression operation by interacting with ZStream.
    /// </summary>
    /// <param name="input">The input data to compress.</param>
    /// <param name="output">The buffer to receive compressed output.</param>
    /// <param name="compressionLevel">The compression level to use.</param>
    /// <exception cref="InvalidOperationException">Thrown when ZLib initialization fails.</exception>
    private void DeflateInternal(byte[] input, byte[] output, int compressionLevel)
    {
        inputBuffer = input;
        outputBuffer = output;
        
        // Here we safely use ref struct
        ZStream zStream = new ZStream
        {
            Input = inputBuffer,
            Output = outputBuffer
        };
        
        // Initialize if necessary
        if (LastResult == 0)
        {
            LastResult = zlib.DeflateInit(
                ref zStream,
                compressionLevel,
                ZLib.Z_DEFLATED,
                -15,  // Negative for raw deflate (no header/trailer)
                9,    // MAX_MEM_LEVEL = 9
                ZLib.Z_DEFAULT_STRATEGY);
                
            if (LastResult != ZLib.Z_OK)
                throw new InvalidOperationException($"Could not initialize deflate: {LastResult}");
        }
        
        // Execute compression
        LastResult = zlib.Deflate(ref zStream, ZLib.Z_FULL_FLUSH);
        
        // Save important state
        TotalIn = zStream.TotalIn;
        TotalOut = zStream.TotalOut;
        Adler = zStream.Adler;
    }
    
    /// <summary>
    /// Finalizes the deflate stream and cleans up ZLib resources.
    /// </summary>
    /// <param name="input">The final input (typically empty).</param>
    /// <param name="output">The buffer to receive any final compressed data.</param>
    private void DeflateFinishInternal(byte[] input, byte[] output)
    {
        inputBuffer = input;
        outputBuffer = output;
        
        ZStream zStream = new ZStream
        {
            Input = inputBuffer,
            Output = outputBuffer
        };
        
        // Finalize compression
        LastResult = zlib.Deflate(ref zStream, ZLib.Z_FINISH);
        
        // Save state
        TotalIn = zStream.TotalIn;
        TotalOut = zStream.TotalOut;
        Adler = zStream.Adler;
        
        // Close the stream
        zlib.DeflateEnd(ref zStream);
    }
}
