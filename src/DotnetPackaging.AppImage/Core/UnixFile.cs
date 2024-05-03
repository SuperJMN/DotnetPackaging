using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public record UnixFile(ZafiroPath path, IData data, string owner, string group, UnixFilePermissions UnixFilePermissions);