﻿using CSharpFunctionalExtensions;

namespace DotnetPackaging.New.Ar;

public class Properties
{
    public required DateTimeOffset LastModification { get; init; }
    public required FileMode FileMode { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }
}