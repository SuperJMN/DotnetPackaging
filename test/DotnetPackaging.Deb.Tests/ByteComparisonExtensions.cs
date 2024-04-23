using FluentAssertions;
using FluentAssertions.Execution;
using System;
using System.Text;

public static class ByteComparisonExtensions
{
    public static void ShouldBeEquivalentToWithBinaryFormat(this byte[] actual, byte[] expected, string because = "", params object[] becauseArgs)
    {
        int maxLength = Math.Max(actual.Length, expected.Length);

        var report = new StringBuilder();
        report.AppendLine("Offset\tArray1\t\t\t\t\t\t\t\t\t\tArray2");

        for (int i = 0; i < maxLength; i += 16)
        {
            report.Append($"{i:X2}\t");
            AppendFormattedBytes(report, actual, i, 16);
            report.Append("\t");
            AppendFormattedBytes(report, expected, i, 16);
            report.AppendLine();
        }

        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .FailWith("Expected arrays to be equivalent{reason}. Differences:\n" + report.ToString());
    }


    private static void AppendFormattedBytes(StringBuilder report, byte[] bytes, int offset, int length)
    {
        int end = Math.Min(offset + length, bytes.Length);
        for (int i = offset; i < end; i++)
        {
            // Use a monospace font representation for characters and hexadecimal values
            if (bytes[i] < 32 || bytes[i] > 126) // Non-printable ASCII
                report.AppendFormat("{0:X2} ", bytes[i]);
            else
                report.AppendFormat("{0}  ", (char)bytes[i]); // Ensure two characters for alignment
        }
    
        // Padding for missing bytes in the line
        int missingBytes = length - (end - offset);
        report.Append(new string(' ', missingBytes * 3));
    }


}