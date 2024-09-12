using Zafiro.FileSystem.Core;

namespace DotnetPackaging.Gui.ViewModels;

public class FileSystemNodeViewModel<T>(T value) : INamed where T : INamed
{
    public T Value { get; } = value;
    public string Path => Value is IRooted rooted ? rooted.Path.ToString().Replace("file:///", "") : Value.Name;
    public string Name => Value.Name;
}