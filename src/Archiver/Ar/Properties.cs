﻿using Archiver.Tar;
using CSharpFunctionalExtensions;

namespace Archiver.Ar;

public class Properties
{
    public required DateTimeOffset LastModification { get; init; }
    public required FileModes FileModes { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
}