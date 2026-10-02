namespace DotnetPackaging.AppImage.Core;

internal abstract record SquashFsNode(string Name, ushort Mode, uint OwnerId);

internal sealed record SquashFsFile(string Name, ushort Mode, uint OwnerId, byte[] Content) : SquashFsNode(Name, Mode, OwnerId);

internal sealed record SquashFsDirectory(string Name, ushort Mode, uint OwnerId, IReadOnlyList<SquashFsNode> Children) : SquashFsNode(Name, Mode, OwnerId);
