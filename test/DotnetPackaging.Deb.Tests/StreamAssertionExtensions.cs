using FluentAssertions.Execution;
using FluentAssertions.Streams;

namespace DotnetPackaging.Deb.Tests;

public static class StreamAssertionExtensions
{
    public static void BeEquivalentTo(this StreamAssertions assertions, Stream expected)
    {
        assertions.Subject.Position = 0;
        expected.Position = 0;

        const int bufferSize = 1024; // Smaller buffer size for more precise reporting
        byte[] actualBuffer = new byte[bufferSize];
        byte[] expectedBuffer = new byte[bufferSize];

        long position = 0; // Track the position in the stream

        while (true)
        {
            int actualReadBytes = assertions.Subject.Read(actualBuffer, 0, actualBuffer.Length);
            int expectedReadBytes = expected.Read(expectedBuffer, 0, expectedBuffer.Length);

            int compareLength = Math.Min(actualReadBytes, expectedReadBytes);

            for (int i = 0; i < compareLength; i++)
            {
                if (actualBuffer[i] != expectedBuffer[i])
                {
                    Execute.Assertion
                        .FailWith($"Streams differ at position {position + i}. Expected byte {expectedBuffer[i]:X2} but found byte {actualBuffer[i]:X2}.");
                }
            }

            if (actualReadBytes != expectedReadBytes)
            {
                Execute.Assertion
                    .FailWith($"Streams differ in length. One stream ended at position {position + compareLength}.");
                break;
            }

            if (actualReadBytes == 0) // End of both streams
            {
                break;
            }

            position += actualReadBytes;
        }

        // Optionally reset the position of the streams if required after comparison
        assertions.Subject.Position = 0;
        expected.Position = 0;
    }
}