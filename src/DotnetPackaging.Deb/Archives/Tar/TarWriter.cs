﻿using CSharpFunctionalExtensions;
using MoreLinq;
using System.IO;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarWriter
{
    public static Task<Result> Write(TarFile arFile, Stream stream)
    {
        return WriteEntries(arFile, stream).Bind(() => WritePadding(stream));
    }

    private static Task<Result> WritePadding(Stream stream)
    {
        return Result.Try(async () =>
        {
            var finalSize = stream.Position.RoundUpToNearestMultiple(20 * 512);
            var padding = finalSize - stream.Position;
            await stream.WriteAsync(new byte[padding]);
        });
    }

    private static Task<Result> WriteEntries(TarFile arFile, Stream stream)
    {
        return arFile.Entries
            .Select(entry =>
            {
                return entry switch
                {
                    FileTarEntry fileEntry => WriteFileEntry(fileEntry, stream),
                    DirectoryTarEntry dirEntry => WriteDirectoryEntry(dirEntry, stream),
                    _ => throw new NotSupportedException()
                };
            })
            .CombineInOrder();
    }

    private static Task<Result> WriteFileEntry(FileTarEntry entry, Stream output)
    {
        return entry.File.File.Open().Bind(async fileStream =>
        {
            var header = GetHeader(entry, fileStream.Length);
            var buffer = header.Pad(512).ToArray();
            await output.WriteAsync(buffer);
            await WriteContent(fileStream, output);
            return Result.Success();
        });

        //return Result
        //    .Try(() => output.WriteAsync(entry.File.Name.PadRight(16).GetAsciiBytes()))
        //    .Bind(_ => entry.File.Open()
        //        .Using(stream =>
        //        {
        //            return WriteProperties(entry.TarProperties, stream.Length, output)
        //                .Bind(() => Result.Try(() => output.WriteAsync("`\n".GetAsciiBytes())))
        //                .Bind(_ => Result.Try(() => stream.CopyToAsync(output)));
        //        }));
    }
    
    private static async Task<Result> WriteDirectoryEntry(DirectoryTarEntry entry, Stream output)
    {
        var header = GetHeaderDir(entry);
        var buffer = header.Pad(512).ToArray();
        await output.WriteAsync(buffer);
        return Result.Success();
    }
    
    private static IEnumerable<byte> GetHeaderDir(DirectoryTarEntry entry)
    {
        var headerBytes = GetHeaderDirCore(entry, Maybe<long>.None);
        var checksum = headerBytes.Sum(x => x);
        return GetHeaderDirCore(entry, checksum);
    }
    
    private static IEnumerable<byte> GetHeaderDirCore(DirectoryTarEntry entry, Maybe<long> checksum)
    {
        var filenameBytes = ("./" + entry.Path).Truncate(100).PadRight(100, '\0').GetAsciiBytes();
        var fileModeBytes = ((int)entry.Properties.FileMode).ToOctal().NullTerminatedPaddedField(8).GetAsciiBytes();
        var ownerBytes = entry.Properties.OwnerId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8).GetAsciiBytes();
        var groupBytes = entry.Properties.GroupId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8).GetAsciiBytes();
        var fileSizeBytes = StringManipulationMixin.ToOctalField(0).GetAsciiBytes();
        var lastModificationBytes = entry.Properties.LastModification.ToUnixTimeSeconds().ToOctalField().GetAsciiBytes();
        var checksumBytes = checksum.Match(
            l => (l.ToOctal().PadLeft(6, '0').NullTerminated() + " ").GetAsciiBytes(),
            () => Enumerable.Repeat<byte>(0x20, 8).ToArray()
        );
        var linkIndicatorBytes = 5.ToString().GetAsciiBytes();
        var nameOfLinkedFileBytes = new byte[100];
        var ustarBytes = "ustar".PadRight(6, ' ').GetAsciiBytes();
        var ustarVersionBytes = new byte[] { 0x20, 0x00 };
        var ownerUsernameBytes = entry.Properties.OwnerUsername.Map(s => s.PadRight(32, '\0').GetAsciiBytes()).GetValueOrDefault(() => new byte[32]);
        var groupUsernameBytes = entry.Properties.GroupName.Map(s => s.PadRight(32, '\0').GetAsciiBytes()).GetValueOrDefault(() => new byte[32]);
        
        var concat = new[]
        {
            filenameBytes,
            fileModeBytes,
            ownerBytes,
            groupBytes,
            fileSizeBytes,
            lastModificationBytes,
            checksumBytes,
            linkIndicatorBytes,
            nameOfLinkedFileBytes,
            ustarBytes,
            ustarVersionBytes,
            ownerUsernameBytes,
            groupUsernameBytes
        };

        return concat.SelectMany(x => x);
    }

    private static async Task WriteContent(Stream fileStream, Stream output)
    {
        var multiple = fileStream.Length.RoundUpToNearestMultiple(512);
        var padding = multiple - fileStream.Length;
        await fileStream.CopyToAsync(output);
        await output.WriteAsync(new byte[padding]);
    }

    private static IEnumerable<byte> GetHeader(FileTarEntry entry, long fileSize)
    {
        var headerBytes = GetHeaderCore(entry, Maybe<long>.None, fileSize);
        var checksum = headerBytes.Sum(x => x);
        return GetHeaderCore(entry, checksum, fileSize);
    }
    
    private static IEnumerable<byte> GetHeaderCore(FileTarEntry entry, Maybe<long> checksum, long fileSize)
    {
        var filenameBytes = ("./" + entry.File.FullPath()).Truncate(100).PadRight(100, '\0').GetAsciiBytes();
        var fileModeBytes = ((int)entry.Properties.FileMode).ToOctal().NullTerminatedPaddedField(8).GetAsciiBytes();
        var ownerBytes = entry.Properties.OwnerId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8).GetAsciiBytes();
        var groupBytes = entry.Properties.GroupId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8).GetAsciiBytes();
        var fileSizeBytes = fileSize.ToOctalField().GetAsciiBytes();
        var lastModificationBytes = entry.Properties.LastModification.ToUnixTimeSeconds().ToOctalField().GetAsciiBytes();
        var checksumBytes = checksum.Match(
            l => (l.ToOctal().PadLeft(6, '0').NullTerminated() + " ").GetAsciiBytes(),
            () => Enumerable.Repeat<byte>(0x20, 8).ToArray()
        );
        var linkIndicatorBytes = 0.ToString().GetAsciiBytes();
        var nameOfLinkedFileBytes = new byte[100];
        var ustarBytes = "ustar".PadRight(6, ' ').GetAsciiBytes();
        var ustarVersionBytes = new byte[] { 0x20, 0x00 };
        var ownerUsernameBytes = entry.Properties.OwnerUsername.Map(s => s.PadRight(32, '\0').GetAsciiBytes()).GetValueOrDefault(() => new byte[32]);
        var groupUsernameBytes = entry.Properties.GroupName.Map(s => s.PadRight(32, '\0').GetAsciiBytes()).GetValueOrDefault(() => new byte[32]);
        
        var concat = new[]
        {
            filenameBytes,
            fileModeBytes,
            ownerBytes,
            groupBytes,
            fileSizeBytes,
            lastModificationBytes,
            checksumBytes,
            linkIndicatorBytes,
            nameOfLinkedFileBytes,
            ustarBytes,
            ustarVersionBytes,
            ownerUsernameBytes,
            groupUsernameBytes
        };

        return concat.SelectMany(x => x);
    }
}