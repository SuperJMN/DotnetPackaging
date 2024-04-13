namespace DotnetPackaging;

public static class MathMixin
{
    public static int RoundUpToNearestMultiple(this int number, int multiple)
    {
        return (number + multiple-1) / multiple * multiple;
    }

    public static long RoundUpToNearestMultiple(this long number, long multiple)
    {
        return (number + multiple-1) / multiple * multiple;
    }
}