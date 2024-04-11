namespace DotnetPackaging.AppImage.Tests;

public static class StreamGenerator
{
    public static Func<Task<Result<Func<Stream>>>> Generate(Func<Stream, Task<Result>> dump)
    {
        return async () =>
        {
            var ms = new MemoryStream();
            var r = await dump(ms)
                .Tap(() => ms.Position = 0)
                .Map(() => new Func<Stream>(() => ms));
            return r;
        };
    }
}