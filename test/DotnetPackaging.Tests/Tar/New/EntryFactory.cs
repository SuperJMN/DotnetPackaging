﻿using CSharpFunctionalExtensions;
using DotnetPackaging.Common;
using DotnetPackaging.NewTar;
using FluentAssertions.Extensions;
using Zafiro.FileSystem;

namespace DotnetPackaging.Tests.Tar.New;

public class EntryFactory
{
    public static Task<Result<Entry>> Create(IFileSystem fs, ZafiroPath path, string name)
    {
        return fs.GetFile(path)
            .Bind(file => file.ToByteStream())
            .Map(byteFlow => new Entry(name, new Properties()
            {
                FileMode = FileMode.Parse("644"),
                GroupId = 1000,
                OwnerId = 1000,
                GroupName = "jmn",
                LastModification = 30.January(1981),
                LinkIndicator = 0,
                OwnerUsername = "jmn"
            }, byteFlow));
    }
}