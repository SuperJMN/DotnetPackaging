﻿using System.Formats.Tar;
using System.Text;
using CSharpFunctionalExtensions;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Writers;
using Zafiro.FileSystem;

#pragma warning disable AsyncFixer01

namespace Archiver;

public class DebFile
{
    public static async Task<Result> Create(Stream stream, IZafiroDirectory directory, Metadata metadata)
    {
        return await CreateData(directory, metadata)
            .Bind(data => CreateControl(directory, metadata).Map(control => (data, control)))
            .Bind(tuple => ArchiverFile.Write(stream, tuple.control, tuple.data));
    }

    private static Result<FileEntry> CreateControl(IZafiroDirectory zafiroDirectory, Metadata metadata)
    {
        return FileEntry.Create("control.tar", GetControlStream(metadata), DateTimeOffset.Now);
    }

    private static Task<Result<FileEntry>> CreateData(IZafiroDirectory zafiroDirectory, Metadata metadata)
    {
        return GetDataStream(zafiroDirectory, metadata).Bind(stream => FileEntry.Create("data.tar", stream, DateTimeOffset.Now));
    }

    private static async Task<Result<Stream>> GetDataStream(IZafiroDirectory zafiroDirectory, Metadata metadata)
    {
        return await zafiroDirectory.GetFilesInTree()
            .Map(async files =>
            {
                Stream stream = new MemoryStream();
                var tarArchive = TarArchive.Create();

                tarArchive.AddEntry($"./usr/local/bin/{metadata.PackageName.ToLower()}/", Stream.Null);
                
                var adds = await Task.WhenAll(files.Select(file => AddData(zafiroDirectory, file, metadata)));
                adds.Combine()
                    .Bind(entries => Result.Try(() =>
                    {
                        foreach (var fileData in entries)
                        {
                            tarArchive.AddEntry(fileData.Item1, fileData.Item2, fileData.Item2.Length);
                        }

                        tarArchive.SaveTo(stream, new WriterOptions(CompressionType.None));
                    }));

                stream.Seek(0, SeekOrigin.Begin);
                return stream;
            });
    }

    private static Stream GetControlStream(Metadata metadata)
    {
        var stream = new MemoryStream();
        var tarArchive = TarArchive.Create();

        var contents = $"""
                       Package: {metadata.PackageName.ToLower()}
                       Priority: optional
                       Section: utils
                       Maintainer: {metadata.Maintainer}
                       Version: 2.0.4
                       Homepage: {metadata.Homepage}
                       Vcs-Git: git://github.com/zkSNACKs/WalletWasabi.git
                       Vcs-Browser: https://github.com/zkSNACKs/WalletWasabi
                       Architecture: {metadata.Architecture}
                       License: {metadata.License}
                       Installed-Size: 207238
                       Recommends: policykit-1
                       Description: open-source, non-custodial, privacy focused Bitcoin wallet
                         Built-in Tor, coinjoin, payjoin and coin control features.
                       
                       """.Replace("\r\n", "\n");

        var control = new MemoryStream(Encoding.UTF8.GetBytes(contents));
        tarArchive.AddEntry("control", control, control.Length);
        tarArchive.SaveTo(stream, new WriterOptions(CompressionType.None));

        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private static async Task<Result<(string, Stream)>> AddData(IZafiroDirectory origin, IZafiroFile zafiroFile, Metadata metadata)
    {
        return await zafiroFile
            .GetContents()
            .Map(stream =>
            {
                var path = zafiroFile.Path.MakeRelativeTo(origin.Path);
                return ($"./usr/local/bin/{metadata.PackageName.ToLower()}/{path}", stream);
            });
    }

    //private static TarArchiveEntry AddEntryFromStream(string key, TarArchive tarArchive, Stream stream)
    //{
    //    new TarArchiveEntry(tarArchive, )
    //    return tarArchive.AddEntry(key, stream, stream.Length);
    //}
}

public class Metadata
{
    public Metadata(string applicationName, string packageName, string maintainer)
    {
        ApplicationName = applicationName;
        PackageName = packageName;
        Maintainer = maintainer;
    }

    public string Maintainer { get; }

    public string PackageName { get; }

    public string ApplicationName { get; }
    public string Architecture { get; set; }
    public string Homepage { get; set; }
    public string License { get; set; }
}
