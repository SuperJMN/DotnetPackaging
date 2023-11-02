using CSharpFunctionalExtensions;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Writers.Tar;

namespace DotnetPackaging;

internal class SharcompressTarFile : ITarFile
{
    private readonly TarArchive tarFile;

    public SharcompressTarFile(TarArchive tarFile)
    {
        this.tarFile = tarFile;
    }

    public Task AddFileEntry(string key, Stream stream) => Task.FromResult(tarFile.AddEntry(key, stream, stream.Length));

    public Task AddDirectoryEntry(string key, Stream stream) => Task.FromResult(tarFile.AddEntry(key, stream, stream.Length));

    public async Task<Result> Build(Stream target) => Result.Try(() => tarFile.SaveTo(target, new TarWriterOptions(CompressionType.None, true)));
}