using System.Text;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe;

public static class PayloadAppender
{
    public static IByteSource AppendPayload(IByteSource signedStub, IByteSource payload)
    {
        return ByteSource.FromAsyncStreamFactory(async () =>
        {
            var stubStream = signedStub.ToStreamSeekable();
            var payloadStream = payload.ToStreamSeekable();
            var output = new MemoryStream();

            await using (stubStream)
            await using (payloadStream)
            {
                await stubStream.CopyToAsync(output);
                await payloadStream.CopyToAsync(output);

                var lengthBytes = BitConverter.GetBytes(payloadStream.Length);
                var magicBytes = Encoding.ASCII.GetBytes("DPACKEXE1");

                await output.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                await output.WriteAsync(magicBytes, 0, magicBytes.Length);
                output.Position = 0;
                return output;
            }
        });
    }
}
