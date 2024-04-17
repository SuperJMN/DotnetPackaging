using CSharpFunctionalExtensions;

namespace DotnetPackaging;

public static class MaybeMixin
{
    public static Maybe<T> From<T>(T? value) where T : struct
    {
        return value.HasValue ? Maybe<T>.From(value.Value) : new Maybe<T>(); // Suponiendo que hay un constructor por defecto que maneje la ausencia de valor
    }
}