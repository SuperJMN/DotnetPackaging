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
    
    public static bool IsPowerOf2(int n) 
    {
        return n != 0 && (n & (n - 1)) == 0;
    }
    
    public static int NextPowerOfTwo(int value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        value++;
        return value;
    }
}