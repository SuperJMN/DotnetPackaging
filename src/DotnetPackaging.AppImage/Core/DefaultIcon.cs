using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public class DefaultIcon : IIcon
{
    public Func<Task<Result<Stream>>> Open => async () => Result.Try(() => (Stream)File.OpenRead("Default.png"));
}