using System.Reactive.Disposables;
using CSharpFunctionalExtensions;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Writers;
using Zafiro.Mixins;

namespace Archiver;

public class DebFile
{
    public static void Create(Stream stream)
    {
        var control = CreateControl();
        var data = CreateData();

        ArchiverFile.Write(stream, data.Value);
    }

    private static Result<FileEntry> CreateControl()
    {
        return FileEntry.Create("control.tar", GetControlStream(), DateTimeOffset.Now);
    }

    private static Result<FileEntry> CreateData()
    {
        return FileEntry.Create("data.tar", GetDataStream(), DateTimeOffset.Now);
    }

    private static Stream GetDataStream()
    {
        var stream = new MemoryStream();
        var tarArchive = TarArchive.Create();
        using (CompositeDisposable disposable = new())
        {
            AddData(tarArchive, disposable);
            tarArchive.SaveTo(stream, new WriterOptions(CompressionType.None));
            disposable.Dispose();
        }

        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private static Stream GetControlStream()
    {
        var stream = new MemoryStream();
        var tarArchive = TarArchive.Create();
        
        using (CompositeDisposable disposable = new())
        {
            AddEntries(tarArchive, disposable);
            tarArchive.SaveTo(stream, new WriterOptions(CompressionType.GZip));
            //disposable.Dispose();
        }

        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private static void AddEntries(TarArchive tarArchive, CompositeDisposable disposable)
    {
        var stream1 = "asdf".ToStream();
        tarArchive.AddEntry("control", stream1.DisposeWith(disposable), stream1.Length);
        var stream2 = "asdf".ToStream();
        tarArchive.AddEntry("md5sums", stream2.DisposeWith(disposable), stream2.Length);
    }

    private static void AddData(TarArchive tarArchive, CompositeDisposable disposable)
    {
        AddEntryFromStream("./usr/share/pepito1.txt", tarArchive, "pepito1".ToStream().DisposeWith(disposable));
        AddEntryFromStream("./usr/share/pepito2.txt", tarArchive, "pepito2".ToStream().DisposeWith(disposable));
        AddEntryFromStream("./usr/share/pepito3.txt", tarArchive, "pepito3".ToStream().DisposeWith(disposable));
    }

    private static void AddEntryFromStream(string key, TarArchive tarArchive, Stream stream)
    {
        tarArchive.AddEntry(key, stream, stream.Length);
    }
}
