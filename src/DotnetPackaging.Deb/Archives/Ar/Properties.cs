﻿using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Deb.Archives.Ar;

public record Properties
{
    public required DateTimeOffset LastModification { get; init; }
    public required UnixFilePermissions FileMode { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
}