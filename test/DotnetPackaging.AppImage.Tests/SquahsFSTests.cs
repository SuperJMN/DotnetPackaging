using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using DotnetPackaging.AppImage.Core;
using FluentAssertions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage.Tests;

public class SquashFSTests
{
    [Fact]
    public async Task Image_is_padded_so_loop_devices_cover_the_whole_filesystem()
    {
        var root = Directory("", [File("File", "Hola")]);

        var image = await CreateImage(root);

        (image.Length % 4096).Should().Be(0);
        SquashFsReader.BytesUsed(image).Should().BeLessThanOrEqualTo((ulong)image.Length);
    }

    [Fact]
    public async Task Directory_entries_reference_the_inode_numbers_of_their_inodes()
    {
        // Enough entries to spread the inodes over several metadata blocks and directory headers
        var manyFiles = Enumerable.Range(0, 600).Select(i => File($"Assembly.Number{i:000}.dll", $"content {i}")).ToArray();
        var root = Directory("", [Directory("usr", [Directory("bin", manyFiles), Directory("share", [File("readme", "text")])]), File("AppRun", "#!/bin/sh")]);

        var image = await CreateImage(root);

        var entries = SquashFsReader.Walk(image);
        entries.Should().HaveCount(605);
        entries.Should().OnlyContain(entry => entry.EntryInodeNumber == entry.InodeNumber);
        entries.Select(entry => entry.InodeNumber).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Inode_modes_hold_permissions_only()
    {
        var root = Directory("", [Directory("bin", [File("app", "binary")])]);

        var image = await CreateImage(root);

        // The kernel refuses inodes whose mode carries file type bits (S_IFMT)
        SquashFsReader.Walk(image).Should().OnlyContain(entry => (entry.Mode & 0xF000) == 0);
    }

    [Fact]
    public async Task File_contents_round_trip()
    {
        var large = Enumerable.Range(0, 300_000).Select(i => (byte)(i * 7 % 251)).ToArray();
        var root = Directory("", [
            File("empty", ""),
            File("small", "Hola"),
            new UnixFile(new Resource("large.bin", ByteSource.FromBytes(large)), new UnixPermissions(Permission.All), 0),
            File("ñandú.txt", "unicode")
        ]);

        var image = await CreateImage(root);

        var files = SquashFsReader.Walk(image).ToDictionary(entry => entry.Path, entry => entry.Content);
        files["/empty"].Should().BeEmpty();
        Encoding.UTF8.GetString(files["/small"]!).Should().Be("Hola");
        files["/large.bin"].Should().Equal(large);
        Encoding.UTF8.GetString(files["/ñandú.txt"]!).Should().Be("unicode");
    }

    private static async Task<byte[]> CreateImage(UnixDirectory root)
    {
        var result = await SquashFS.Create(root);
        result.Should().Succeed();
        return result.Value.Array();
    }

    private static UnixDirectory Directory(string name, IEnumerable<object> children)
    {
        var list = children.ToList();
        return new UnixDirectory(name, 0, new UnixPermissions(Permission.All), list.OfType<UnixDirectory>(), list.OfType<UnixFile>());
    }

    private static UnixFile File(string name, string content)
    {
        return new UnixFile(new Resource(name, ByteSource.FromString(content, Encoding.UTF8)), new UnixPermissions(Permission.All), 0);
    }

    /// <summary>
    /// Minimal reader that resolves entries the way the Linux kernel does.
    /// </summary>
    private static class SquashFsReader
    {
        public record Entry(string Path, uint EntryInodeNumber, uint InodeNumber, ushort Mode, byte[]? Content);

        public static ulong BytesUsed(byte[] image) => BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(40));

        public static List<Entry> Walk(byte[] image)
        {
            var blockSize = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(12));
            var root = BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(32));
            var inodeTable = (int)BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(64));
            var directoryTable = (int)BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(72));
            var idTable = (int)BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(48));
            var idBlocks = (int)BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(idTable));

            var (inodes, inodeBlocks) = ReadMetadata(image, inodeTable, directoryTable);
            var (directories, directoryBlocks) = ReadMetadata(image, directoryTable, idBlocks);
            var entries = new List<Entry>();

            void Visit(ulong reference, string path, uint entryInodeNumber)
            {
                var position = inodeBlocks[(int)(reference >> 16)] + (int)(reference & 0xFFFF);
                var type = BinaryPrimitives.ReadUInt16LittleEndian(inodes.AsSpan(position));
                var mode = BinaryPrimitives.ReadUInt16LittleEndian(inodes.AsSpan(position + 2));
                var number = BinaryPrimitives.ReadUInt32LittleEndian(inodes.AsSpan(position + 12));
                var body = inodes.AsSpan(position + 16);

                if (type == 2)
                {
                    var start = BinaryPrimitives.ReadUInt32LittleEndian(body);
                    var size = BinaryPrimitives.ReadUInt32LittleEndian(body[12..]);
                    entries.Add(new Entry(path, entryInodeNumber, number, mode, ReadFile(image, start, size, blockSize, body[16..])));
                    return;
                }

                type.Should().Be(1, $"'{path}' should be a basic directory or file");
                if (path != "")
                {
                    entries.Add(new Entry(path, entryInodeNumber, number, mode, null));
                }

                var listingBlock = BinaryPrimitives.ReadUInt32LittleEndian(body);
                var listingSize = BinaryPrimitives.ReadUInt16LittleEndian(body[8..]) - 3;
                var listingOffset = BinaryPrimitives.ReadUInt16LittleEndian(body[10..]);
                var cursor = directoryBlocks[(int)listingBlock] + listingOffset;
                var end = cursor + listingSize;

                while (cursor < end)
                {
                    var count = BinaryPrimitives.ReadUInt32LittleEndian(directories.AsSpan(cursor)) + 1;
                    var start = BinaryPrimitives.ReadUInt32LittleEndian(directories.AsSpan(cursor + 4));
                    var baseNumber = BinaryPrimitives.ReadUInt32LittleEndian(directories.AsSpan(cursor + 8));
                    cursor += 12;

                    for (var i = 0; i < count; i++)
                    {
                        var offset = BinaryPrimitives.ReadUInt16LittleEndian(directories.AsSpan(cursor));
                        var delta = BinaryPrimitives.ReadInt16LittleEndian(directories.AsSpan(cursor + 2));
                        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(directories.AsSpan(cursor + 6)) + 1;
                        var name = Encoding.UTF8.GetString(directories, cursor + 8, nameLength);
                        cursor += 8 + nameLength;
                        Visit(((ulong)start << 16) | offset, path + "/" + name, (uint)(baseNumber + delta));
                    }
                }
            }

            Visit(root, "", 0);
            return entries;
        }

        private static byte[] ReadFile(byte[] image, uint start, uint size, uint blockSize, ReadOnlySpan<byte> blockSizes)
        {
            using var content = new MemoryStream();
            var position = (int)start;
            var blocks = (int)((size + blockSize - 1) / blockSize);

            for (var i = 0; i < blocks; i++)
            {
                var stored = BinaryPrimitives.ReadUInt32LittleEndian(blockSizes[(i * 4)..]);
                var length = (int)(stored & 0xFFFFFF);
                var block = image.AsSpan(position, length).ToArray();
                content.Write((stored & 0x1000000) != 0 ? block : Decompress(block));
                position += length;
            }

            return content.ToArray();
        }

        private static (byte[] Data, Dictionary<int, int> Blocks) ReadMetadata(byte[] image, int start, int end)
        {
            using var data = new MemoryStream();
            var blocks = new Dictionary<int, int>();
            var position = start;

            while (position < end)
            {
                var header = BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(position));
                var length = header & 0x7FFF;
                var block = image.AsSpan(position + 2, length).ToArray();
                blocks[position - start] = (int)data.Length;
                data.Write((header & 0x8000) != 0 ? block : Decompress(block));
                position += 2 + length;
            }

            return (data.ToArray(), blocks);
        }

        private static byte[] Decompress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var zlib = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress))
            {
                zlib.CopyTo(output);
            }

            return output.ToArray();
        }
    }
}
