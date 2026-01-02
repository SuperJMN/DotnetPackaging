using System.Text;
using FluentAssertions;

namespace DotnetPackaging.Flatpak.Tests;

public sealed class GVariantTests
{
    [Fact]
    public void Bool_true_writes_single_byte_one()
    {
        var result = GVariantBuilder.Create().Bool(true).ToArray();

        result.Should().Equal(new byte[] { 1 });
    }

    [Fact]
    public void Bool_false_writes_single_byte_zero()
    {
        var result = GVariantBuilder.Create().Bool(false).ToArray();

        result.Should().Equal(new byte[] { 0 });
    }

    [Fact]
    public void Byte_writes_single_byte()
    {
        var result = GVariantBuilder.Create().Byte(0x42).ToArray();

        result.Should().Equal(new byte[] { 0x42 });
    }

    [Fact]
    public void UInt16_writes_big_endian()
    {
        var result = GVariantBuilder.Create().UInt16(0x1234).ToArray();

        // Big-endian: high byte first
        result.Should().Equal(new byte[] { 0x12, 0x34 });
    }

    [Fact]
    public void UInt32_writes_big_endian()
    {
        var result = GVariantBuilder.Create().UInt32(0x12345678).ToArray();

        // Big-endian: high byte first
        result.Should().Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 });
    }

    [Fact]
    public void UInt64_writes_big_endian()
    {
        var result = GVariantBuilder.Create().UInt64(0x123456789ABCDEF0).ToArray();

        // Big-endian: high byte first
        result.Should().Equal(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 });
    }

    [Fact]
    public void String_writes_utf8_null_terminated()
    {
        var result = GVariantBuilder.Create().String("hello").ToArray();

        var expected = Encoding.UTF8.GetBytes("hello").Concat(new byte[] { 0 }).ToArray();
        result.Should().Equal(expected);
    }

    [Fact]
    public void String_empty_writes_null_terminator_only()
    {
        var result = GVariantBuilder.Create().String("").ToArray();

        result.Should().Equal(new byte[] { 0 });
    }

    [Fact]
    public void ByteArray_writes_raw_bytes()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var result = GVariantBuilder.Create().ByteArray(data).ToArray();

        result.Should().Equal(data);
    }

    [Fact]
    public void Align_pads_to_boundary()
    {
        var builder = GVariantBuilder.Create()
            .Byte(0x01)    // position 1
            .Align(4);     // should pad to position 4

        builder.Position.Should().Be(4);

        var result = builder.ToArray();
        result.Should().Equal(new byte[] { 0x01, 0x00, 0x00, 0x00 });
    }

    [Fact]
    public void Align_no_padding_when_already_aligned()
    {
        var builder = GVariantBuilder.Create()
            .UInt32(0x12345678)  // 4 bytes, already aligned
            .Align(4);           // should be no-op

        builder.Position.Should().Be(4);
    }

    [Fact]
    public void UInt32_auto_aligns_to_4()
    {
        var builder = GVariantBuilder.Create()
            .Byte(0x01)           // position 1
            .UInt32(0xAABBCCDD);  // should auto-align to 4, then write 4 bytes

        builder.Position.Should().Be(8);

        var result = builder.ToArray();
        // Byte at 0, padding at 1-3, then big-endian uint32 at 4-7
        result.Should().Equal(new byte[] { 0x01, 0x00, 0x00, 0x00, 0xAA, 0xBB, 0xCC, 0xDD });
    }

    [Fact]
    public void UInt64_auto_aligns_to_8()
    {
        var builder = GVariantBuilder.Create()
            .Byte(0x01)                       // position 1
            .UInt64(0x1122334455667788);      // should auto-align to 8, then write 8 bytes

        builder.Position.Should().Be(16);

        var result = builder.ToArray();
        // Byte at 0, padding at 1-7, then big-endian uint64 at 8-15
        result.Should().HaveCount(16);
        result[0].Should().Be(0x01);
        result.Skip(8).Should().Equal(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 });
    }

    [Fact]
    public void Multiple_values_builds_correctly()
    {
        var result = GVariantBuilder.Create()
            .String("test")
            .UInt32(123)
            .ByteArray(new byte[] { 0xAB, 0xCD })
            .ToArray();

        // "test\0" = 5 bytes, align to 4 = 8 bytes, uint32 = 4 bytes, 2 bytes array
        result.Length.Should().Be(14);
    }
}
