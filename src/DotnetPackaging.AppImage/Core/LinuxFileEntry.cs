using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public record LinuxFileEntry(ZafiroPath path, IGetStream data, string owner, string group, UnixFileMode unixFileMode);