﻿using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public class RegularContent : Content
{
    public RegularContent(ZafiroPath path, Func<IObservable<byte>> bytes) : base(path, bytes)
    {
    }
}