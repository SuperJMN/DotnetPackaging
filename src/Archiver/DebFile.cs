using System.Text;
using Archiver.Deb;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem;

#pragma warning disable AsyncFixer01

namespace Archiver;

public class DebFile
{
    private readonly ITarFactory factory;
    public Metadata Metadata { get; }

    public DebFile(Metadata metadata)
    {
        Metadata = metadata;
        factory = new SharpCompressTarFactory();
    }
    
    public async Task<Result> Write(Stream stream, IZafiroDirectory directory)
    {
        return await CreateData(factory, directory, Metadata)
            .Bind(data => CreateControl(factory, directory, Metadata).Map(control => (data, control)))
            .Bind(tuple => ArchiverFile.Write(stream, tuple.control, tuple.data));
    }

    private static Result<FileEntry> CreateControl(ITarFactory tarFactory, IZafiroDirectory zafiroDirectory, Metadata metadata)
    {
        return FileEntry.Create("control.tar", GetControlStream(tarFactory, metadata), DateTimeOffset.Now);
    }

    private static Task<Result<FileEntry>> CreateData(ITarFactory tarFactory, IZafiroDirectory zafiroDirectory, Metadata metadata)
    {
        return GetDataStream(zafiroDirectory, tarFactory, metadata).Bind(stream => FileEntry.Create("data.tar", stream, DateTimeOffset.Now));
    }

    private static async Task<Result<Stream>> GetDataStream(IZafiroDirectory zafiroDirectory, ITarFactory tarFactory, Metadata metadata)
    {
        return await zafiroDirectory.GetFilesInTree()
            .Map(async files => await WriteData(new MemoryStream(), zafiroDirectory, tarFactory, metadata, files));
    }

    private static async Task<Stream> WriteData(Stream outputStream, IZafiroDirectory zafiroDirectory, ITarFactory tarFactory, Metadata metadata, IEnumerable<IZafiroFile> files)
    {
        var tarFile = tarFactory.Create();

        var adds = await Task.WhenAll(files.Select(file => AddData(zafiroDirectory, file, metadata)));
        adds.Combine()
            .Bind(entries => Result.Try(() =>
            {
                foreach (var fileData in entries)
                {
                    tarFile.AddFileEntry(fileData.Item1, fileData.Item2);
                }

                tarFile.Build(outputStream);
            }));

        outputStream.Seek(0, SeekOrigin.Begin);
        return outputStream;
    }

    private static Stream GetControlStream(ITarFactory tarFactory, Metadata metadata)
    {
        var stream = new MemoryStream();
        var tarArchive = tarFactory.Create();

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
        tarArchive.AddFileEntry("control", control);
        tarArchive.Build(stream);

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
}