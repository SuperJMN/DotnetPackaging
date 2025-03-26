using System.IO.Hashing;
using System.Text;
using Xunit;

namespace MsixPackaging.Tests;



public class CrcTests
{
    [Fact]
    public async Task Test()
    {
        var test = await File.ReadAllBytesAsync("TestFiles/FullAvaloniaApp/Contents/Microsoft.Extensions.Logging.Abstractions.dll");
        var hex = Microsoft(test).ToString("X8");
    }

    static uint Microsoft(byte[] bytes)
    {
        return Crc32.HashToUInt32(bytes);
    }
}