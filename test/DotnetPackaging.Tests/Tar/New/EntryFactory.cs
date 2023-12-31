﻿using CSharpFunctionalExtensions;
using DotnetPackaging.Archives.Tar;
using DotnetPackaging.Common;
using FluentAssertions.Extensions;
using Zafiro.FileSystem;

namespace DotnetPackaging.Tests.Tar.New;

public class EntryFactory
{
    public static Task<Result<Entry>> Create(IFileSystemRoot fs, ZafiroPath path, string name)
    {
        return fs.GetFile(path)
            .Bind(file => file.ToByteFlow())
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

public static class Mixin 
{
    public static TResult Bind<T, TResult>(this T obj, Func<T, TResult> func)
    {
        return func(obj);
    }

    public static TResult Map<T, TResult>(this T obj, Func<T, TResult> func)
    {
        return func(obj);
    }
}