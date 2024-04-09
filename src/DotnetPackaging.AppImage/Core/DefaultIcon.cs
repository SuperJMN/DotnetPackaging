using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;

namespace DotnetPackaging.AppImage.Core;

public class DefaultIcon : IIcon
{
    public Func<Task<Result<Stream>>> StreamFactory => async () => Result.Try(() => (Stream)File.OpenRead("Default.png"));
}