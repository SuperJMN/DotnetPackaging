using System.Buffers.Binary;
using System.Reactive.Linq;
using DotnetPackaging.Dmg.Hfs.Builder;
using FluentAssertions;
using Xunit;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Hfs.Tests;

public class HfsVolumeTests
{
    [Fact]
    public void HfsVolumeBuilder_ShouldBuildEmptyVolume()
    {
        var volume = HfsVolumeBuilder.Create("TestVolume")
            .Build();
        
        // Volume name is sanitized (uppercase, alphanumeric only)
        volume.VolumeName.Should().Be("TestVolume");
        volume.BlockSize.Should().Be(4096);
    }

    [Fact]
    public void HfsVolumeBuilder_ShouldAddFiles()
    {
        var volume = HfsVolumeBuilder.Create("TestVolume")
            .AddFile("test.txt", new byte[] { 1, 2, 3 })
            .Build();
        
        var (files, folders) = volume.CountEntries();
        files.Should().Be(1);
    }

    [Fact]
    public void HfsVolumeBuilder_ShouldAddDirectories()
    {
        var builder = HfsVolumeBuilder.Create("TestVolume");
        var subDir = builder.AddDirectory("subdir");
        subDir.AddFile("inner.txt", new byte[] { 1, 2, 3 });
        var volume = builder.Build();
        
        var (files, folders) = volume.CountEntries();
        files.Should().Be(1);
        folders.Should().Be(1);
    }

    [Fact]
    public void HfsVolumeBuilder_ShouldAddSymlinks()
    {
        var volume = HfsVolumeBuilder.Create("TestVolume")
            .AddSymlink("Applications", "/Applications")
            .Build();
        
        var (files, _) = volume.CountEntries();
        files.Should().Be(1); // Symlinks count as files
    }

    [Fact]
    public void HfsVolumeWriter_ShouldProduceValidHfsVolume()
    {
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("hello.txt", "Hello, World!"u8.ToArray())
            .Build();
        
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        // Should have at least boot blocks + volume header
        bytes.Length.Should().BeGreaterThan(1536);
        
        // Check HFS+ signature at offset 1024
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B); // 'H+'
    }

    [Fact]
    public void HfsVolumeWriter_ShouldWriteAlternateVolumeHeader()
    {
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("test.txt", new byte[100])
            .Build();
        
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        // Alternate header should be at second-to-last sector
        var altHeaderOffset = bytes.Length - 1024;
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(altHeaderOffset, 2));
        signature.Should().Be(0x482B);
    }

    [Fact]
    public void HfsVolumeWriter_ShouldHandleManySmallFiles()
    {
        // Regression test: each small file needs its own block, not total bytes / blockSize
        var builder = HfsVolumeBuilder.Create("Test");
        
        // Add 100 files of 100 bytes each = 10,000 bytes total
        // But each file needs 1 block (4096 bytes), so 100 blocks needed
        for (var i = 0; i < 100; i++)
        {
            builder.AddFile($"file{i}.txt", new byte[100]);
        }
        
        var volume = builder.Build();
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        // Should not throw and should produce valid HFS+
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B);
        
        // Volume should be at least 100 blocks * 4096 bytes = ~400KB
        bytes.Length.Should().BeGreaterThan(400_000);
    }

    [Fact]
    public void HfsVolumeWriter_ShouldHandleEmptyFiles()
    {
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("empty.txt", Array.Empty<byte>())
            .AddFile("nonempty.txt", new byte[10])
            .Build();
        
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B);
    }

    [Fact]
    public void HfsVolumeWriter_ShouldHandleManySymlinks()
    {
        var builder = HfsVolumeBuilder.Create("Test");
        
        // Add multiple symlinks
        for (var i = 0; i < 50; i++)
        {
            builder.AddSymlink($"link{i}", $"/target/path/{i}");
        }
        
        var volume = builder.Build();
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B);
    }

    [Fact]
    public void HfsVolumeWriter_ShouldHandleDeepDirectoryStructure()
    {
        var builder = HfsVolumeBuilder.Create("Test");
        
        // Create nested structure like App.app/Contents/MacOS/...
        var app = builder.AddDirectory("MyApp.app");
        var contents = app.AddDirectory("Contents");
        var macos = contents.AddDirectory("MacOS");
        var resources = contents.AddDirectory("Resources");
        
        macos.AddFile("MyApp", new byte[1000]);
        contents.AddFile("Info.plist", new byte[500]);
        resources.AddFile("icon.icns", new byte[200]);
        
        var volume = builder.Build();
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B);
        
        var (files, folders) = volume.CountEntries();
        files.Should().Be(3);
        folders.Should().Be(4);
    }

    [Fact]
    public void HfsVolumeWriter_ShouldHandleLargeFile()
    {
        // Single large file spanning multiple blocks
        var largeFile = new byte[50_000]; // ~12 blocks
        new Random(42).NextBytes(largeFile);
        
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("large.bin", largeFile)
            .Build();
        
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        var signature = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1024, 2));
        signature.Should().Be(0x482B);
        
        // Should contain at least the file data
        bytes.Length.Should().BeGreaterThan(50_000);
    }

    [Fact]
    public void HfsVolumeWriter_BlockCountShouldBeCorrect()
    {
        // Verify that block count in header matches actual allocation
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("file1.txt", new byte[100])
            .AddFile("file2.txt", new byte[100])
            .AddFile("file3.txt", new byte[100])
            .Build();
        
        var bytes = HfsVolumeWriter.WriteToBytes(volume);
        
        // Read total blocks from volume header (offset 1024 + 40)
        var totalBlocks = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1024 + 40, 4));
        var freeBlocks = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1024 + 44, 4));
        
        // Basic sanity: used blocks should be reasonable
        var usedBlocks = totalBlocks - freeBlocks;
        usedBlocks.Should().BeGreaterThan(0);
        usedBlocks.Should().BeLessThan(totalBlocks);
    }

    [Fact]
    public async Task HfsVolumeWriter_ShouldWriteKnownLengthByteSourcesToSeekableStreamWithoutFullVolumeBuffer()
    {
        var payload = Enumerable.Range(0, 16)
            .Select(index => Enumerable.Repeat((byte)index, 4096).ToArray())
            .ToArray();
        var source = new TrackingByteSource(payload);
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("payload.bin", source, source.Length.Value)
            .Build();
        await using var output = new TrackingSeekableStream();

        await HfsVolumeWriter.WriteToAsync(volume, output);

        output.Length.Should().BeGreaterThan(payload.Sum(chunk => chunk.Length));
        output.MaxWriteSize.Should().BeLessThan(payload.Sum(chunk => chunk.Length), "file payload chunks should be streamed instead of writing one full volume buffer");
        source.Subscriptions.Should().Be(1);
        output.ReadUInt16BigEndian(1024).Should().Be(0x482B);
    }

    [Fact]
    public async Task HfsVolumeWriter_ShouldRejectSourceOverrunBeforeWritingOverflowBytes()
    {
        var overflowMarker = new byte[] { 0xFA, 0xFB, 0xFC, 0xFD };
        var source = new TrackingByteSource(["data"u8.ToArray(), overflowMarker]);
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("payload.bin", source, 4)
            .Build();
        await using var output = new TrackingSeekableStream();

        var act = async () => await HfsVolumeWriter.WriteToAsync(volume, output);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*payload.bin*Declared size: 4 bytes*emitted bytes: 8 bytes*written bytes: 4 bytes*");
        output.ContainsSequence(overflowMarker).Should().BeFalse("overflow bytes must be rejected before they reach the HFS output stream");
    }

    [Fact]
    public async Task HfsVolumeWriter_ShouldRejectSourceUnderrunAfterCompletion()
    {
        var source = new TrackingByteSource(["abc"u8.ToArray()]);
        var volume = HfsVolumeBuilder.Create("Test")
            .AddFile("payload.bin", source, 5)
            .Build();

        var act = async () => await HfsVolumeWriter.WriteToAsync(volume, new MemoryStream());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*payload.bin*Declared size: 5 bytes*emitted/written bytes: 3 bytes*");
    }

    [Fact]
    public void HfsDirectory_ShouldRejectNegativeFileSize()
    {
        var source = ByteSource.FromBytes("payload"u8.ToArray());
        var builder = HfsVolumeBuilder.Create("Test");

        var act = () => builder.AddFile("payload.bin", source, -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*payload.bin*negative size*");
    }

    private sealed class TrackingByteSource(byte[][] chunks) : IByteSource
    {
        public int Subscriptions { get; private set; }

        public IObservable<byte[]> Bytes => Observable.Defer(() =>
        {
            Subscriptions++;
            return chunks.ToObservable();
        });

        public CSharpFunctionalExtensions.Maybe<long> Length { get; } = CSharpFunctionalExtensions.Maybe.From(chunks.Sum(chunk => (long)chunk.Length));

        public IDisposable Subscribe(IObserver<byte[]> observer) => Bytes.Subscribe(observer);
    }

    private sealed class TrackingSeekableStream : Stream
    {
        private readonly Dictionary<long, byte> bytes = new();
        private long position;
        private long length;

        public int MaxWriteSize { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => length;

        public override long Position
        {
            get => position;
            set => position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = 0;
            while (read < count && position < Length)
            {
                buffer[offset + read] = bytes.GetValueOrDefault(position);
                position++;
                read++;
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
            };

            return position;
        }

        public override void SetLength(long value)
        {
            length = value;
            if (position > value)
            {
                position = value;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            MaxWriteSize = Math.Max(MaxWriteSize, count);

            for (var i = 0; i < count; i++)
            {
                var value = buffer[offset + i];
                if (value != 0)
                {
                    bytes[position] = value;
                }

                position++;
            }

            length = Math.Max(length, position);
        }

        public ushort ReadUInt16BigEndian(long offset)
        {
            Span<byte> buffer = stackalloc byte[2];
            buffer[0] = bytes.GetValueOrDefault(offset);
            buffer[1] = bytes.GetValueOrDefault(offset + 1);
            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }

        public bool ContainsSequence(byte[] sequence)
        {
            if (sequence.Length == 0)
            {
                return true;
            }

            for (var offset = 0L; offset <= Length - sequence.Length; offset++)
            {
                var matches = true;
                for (var i = 0; i < sequence.Length; i++)
                {
                    if (bytes.GetValueOrDefault(offset + i) != sequence[i])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
