namespace DotnetPackaging.Tests.Tar.New;

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