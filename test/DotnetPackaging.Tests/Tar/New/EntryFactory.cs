using CSharpFunctionalExtensions;
using DotnetPackaging.Common;
using DotnetPackaging.NewTar;
using Zafiro.FileSystem;

namespace DotnetPackaging.Tests.Tar.New;

public class EntryFactory
{
    public static Task<Result<Entry>> Create(IFileSystem fs, ZafiroPath path)
    {
        return fs.GetFile(path)
            .Bind(file => file.ToByteStream())
            .Map(byteFlow => new Entry("Entry", new Properties()
            {
                Length = 0,
                FileMode = FileMode.Parse("555"),
                GroupId = 1,
                OwnerId = 1,
                GroupName = "root",
                LastModification = DateTimeOffset.Now,
                LinkIndicator = 1,
                OwnerUsername = "root"
            }, byteFlow));
    }
}