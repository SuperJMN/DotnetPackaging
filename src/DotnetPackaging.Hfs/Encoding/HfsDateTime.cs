namespace DotnetPackaging.Hfs.Encoding;

/// <summary>
/// HFS+ date and time values are stored as an unsigned 32-bit integer
/// containing the number of seconds since January 1, 1904 at 00:00:00 UTC.
/// Maximum representable date is February 6, 2040 at 06:28:15 UTC.
/// </summary>
public static class HfsDateTime
{
    /// <summary>
    /// The HFS+ epoch: January 1, 1904 at 00:00:00 UTC.
    /// </summary>
    public static readonly DateTime Epoch = new(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Maximum representable HFS+ date: February 6, 2040 at 06:28:15 UTC.
    /// </summary>
    public static readonly DateTime MaxDate = Epoch.AddSeconds(uint.MaxValue);

    /// <summary>
    /// Converts a DateTime to HFS+ timestamp (seconds since 1904-01-01 UTC).
    /// </summary>
    public static uint FromDateTime(DateTime dateTime)
    {
        var utc = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
        
        if (utc < Epoch)
            return 0;
        
        var seconds = (utc - Epoch).TotalSeconds;
        return seconds > uint.MaxValue ? uint.MaxValue : (uint)seconds;
    }

    /// <summary>
    /// Converts an HFS+ timestamp to DateTime (UTC).
    /// </summary>
    public static DateTime ToDateTime(uint hfsTime)
        => Epoch.AddSeconds(hfsTime);

    /// <summary>
    /// Gets the current time as an HFS+ timestamp.
    /// </summary>
    public static uint Now => FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Writes an HFS+ timestamp as big-endian bytes.
    /// </summary>
    public static byte[] WriteTimestamp(DateTime dateTime)
        => BigEndianWriter.WriteUInt32(FromDateTime(dateTime));

    /// <summary>
    /// Writes an HFS+ timestamp to a span.
    /// </summary>
    public static void WriteTimestamp(Span<byte> destination, DateTime dateTime)
        => BigEndianWriter.WriteUInt32(destination, FromDateTime(dateTime));
}
