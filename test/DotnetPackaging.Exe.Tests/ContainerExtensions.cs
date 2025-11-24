using Zafiro.DivineBytes;
using Zafiro.Reactive;
using Path = System.IO.Path;

namespace DotnetPackaging.Exe.E2E.Tests;

public static class ContainerExtensions
{
    public static async Task WriteTo(this IContainer container, string path)
    {
        foreach (var resource in container.ResourcesWithPathsRecursive())
        {
            var fullPath = Path.Combine(path, resource.FullPath().ToString());
            var directoryName = Path.GetDirectoryName(fullPath);
            if (directoryName != null)
            {
                Directory.CreateDirectory(directoryName);
            }
            await using var stream = File.Create(fullPath);
            await resource.Bytes.ToStream().CopyToAsync(stream);
        }
    }
}