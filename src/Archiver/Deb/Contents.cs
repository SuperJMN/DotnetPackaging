using Zafiro.FileSystem;

namespace Archiver.Deb;

public class Contents : Dictionary<ZafiroPath, Func<Stream>>
{
}