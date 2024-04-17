using CSharpFunctionalExtensions;

namespace DotnetPackaging;

public static class Maybe
{
    public static Maybe<T> SafeFrom<T>(T? value) where T : struct
    {
        return value.HasValue ? Maybe<T>.From(value.Value) : new Maybe<T>(); // Suponiendo que hay un constructor por defecto que maneje la ausencia de valor
    }
}