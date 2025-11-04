using System.IO.Compression;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Rpm;
using Zafiro.DataModel;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Rpm.Tests;

public class RpmPackageTests
{
    [Fact]
    public void Package_can_be_serialized_to_valid_rpm_stream()
    {
        var buildTime = new DateTimeOffset(2024, 4, 5, 10, 30, 0, TimeSpan.Zero);

        var metadata = new PackageMetadata("Sample App", Architecture.X64, isTerminal: false, "sample-app", "1.0.0")
        {
            Name = "Sample App",
            Architecture = Architecture.X64,
            Package = "sample-app",
            Version = "1.0.0",
            Summary = "Sample application",
            Description = "Sample application used for RPM packaging tests",
            License = "MIT",
            Maintainer = "Maintainer <maintainer@example.com>",
            Homepage = new Uri("https://example.com"),
            Section = "utils",
            ModificationTime = buildTime
        };

        var directoryProperties = UnixFileProperties.RegularDirectoryProperties() with
        {
            OwnerId = Maybe<int>.From(0),
            GroupId = Maybe<int>.From(0),
            OwnerUsername = Maybe<string>.From("root"),
            GroupName = Maybe<string>.From("root"),
            LastModification = buildTime
        };

        var executableProperties = UnixFileProperties.ExecutableFileProperties() with
        {
            OwnerId = Maybe<int>.From(0),
            GroupId = Maybe<int>.From(0),
            OwnerUsername = Maybe<string>.From("root"),
            GroupName = Maybe<string>.From("root"),
            LastModification = buildTime
        };

        var launcherProperties = UnixFileProperties.RegularFileProperties() with
        {
            OwnerId = Maybe<int>.From(0),
            GroupId = Maybe<int>.From(0),
            OwnerUsername = Maybe<string>.From("root"),
            GroupName = Maybe<string>.From("root"),
            LastModification = buildTime
        };

        var entries = new[]
        {
            new RpmEntry("/opt/sample", directoryProperties, null, RpmEntryType.Directory),
            new RpmEntry("/usr/bin", directoryProperties, null, RpmEntryType.Directory),
            new RpmEntry("/opt/sample/sample-app", executableProperties, Data.FromString("#!/bin/sh\necho sample\n", Encoding.ASCII), RpmEntryType.File),
            new RpmEntry("/usr/bin/sample-app", launcherProperties, Data.FromString("#!/bin/sh\nexec /opt/sample/sample-app \"$@\"\n", Encoding.ASCII), RpmEntryType.File)
        };

        var package = new RpmPackage(metadata, entries);
        var bytes = package.ToData().Bytes();

        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(0);

        // Lead magic
        bytes.AsSpan(0, 4).ToArray().Should().Equal(0xED, 0xAB, 0xEE, 0xDB);

        const int leadSize = 96;
        var signatureHeaderLength = ReadHeaderLength(bytes, leadSize);
        var mainHeaderOffset = leadSize + signatureHeaderLength;
        bytes.AsSpan(mainHeaderOffset, 4).ToArray().Should().Equal(0x8E, 0xAD, 0xE8, 0x01);

        var mainHeaderLength = ReadHeaderLength(bytes, mainHeaderOffset);
        var payloadOffset = mainHeaderOffset + mainHeaderLength;

        var cpio = DecompressPayload(bytes, payloadOffset);
        var payloadEntries = ParseCpioEntries(cpio);

        payloadEntries.Should().ContainKey("./opt/sample/sample-app");
        payloadEntries.Should().ContainKey("./usr/bin/sample-app");
        Encoding.ASCII.GetString(payloadEntries["./opt/sample/sample-app"])
            .Should().Contain("echo sample");
    }

    private static Dictionary<string, byte[]> ParseCpioEntries(byte[] payload)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var offset = 0;

        while (offset < payload.Length)
        {
            var magic = Encoding.ASCII.GetString(payload, offset, 6);
            if (!string.Equals(magic, "070701", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Unexpected CPIO header magic.");
            }

            offset += 6;
            // Skip inode
            offset += 8;
            _ = ReadHex(payload, ref offset);
            // uid, gid, nlink, mtime
            offset += 8 * 4;
            var fileSize = ReadHex(payload, ref offset);
            // devmajor, devminor, rdevmajor, rdevminor
            offset += 8 * 4;
            var nameSize = ReadHex(payload, ref offset);
            // checksum
            offset += 8;

            var nameBytes = payload[offset..(offset + nameSize)];
            var name = Encoding.ASCII.GetString(nameBytes.Take(nameSize - 1).ToArray());
            offset += nameSize;
            offset = Align(offset, 4);

            var data = payload[offset..(offset + fileSize)].ToArray();
            offset += fileSize;
            offset = Align(offset, 4);

            if (name == "TRAILER!!!")
            {
                break;
            }

            if (!name.StartsWith("./", StringComparison.Ordinal))
            {
                name = "./" + name.TrimStart('/');
            }

            result[name] = data;
        }

        return result;
    }

    private static int ReadHex(byte[] payload, ref int offset)
    {
        var value = Convert.ToInt32(Encoding.ASCII.GetString(payload, offset, 8), 16);
        offset += 8;
        return value;
    }

    private static int Align(int value, int alignment)
    {
        var remainder = value % alignment;
        return remainder == 0 ? value : value + alignment - remainder;
    }

    private static byte[] DecompressPayload(byte[] source, int offset)
    {
        using var compressed = new MemoryStream(source, offset, source.Length - offset);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static int ReadHeaderLength(byte[] data, int offset)
    {
        var indexCount = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset + 8, 4));
        var dataSize = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset + 12, 4));
        var length = 16 + (indexCount * 16) + dataSize;
        var padding = (8 - (length % 8)) % 8;
        return length + padding;
    }
}
