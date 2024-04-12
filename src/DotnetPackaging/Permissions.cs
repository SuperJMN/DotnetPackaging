namespace DotnetPackaging;

public class Permissions
{
    private Permissions(int value)
    {
        Value = value;
    }

    public static Permissions None => new(0);
    public static Permissions Execute { get; } = new(1);
    public static Permissions Write { get; } = new(2);
    public static Permissions Read { get; } = new(4);

    private int Value { get; }

    public static implicit operator int(Permissions permission) => permission.Value;

    public static Permissions Combine(Permissions permission1, Permissions permission2)
    {
        var combinedValue = permission1.Value | permission2.Value;
        return new Permissions(combinedValue);
    }

    public static Permissions Combine(Permissions permission1, Permissions permission2, Permissions permission3)
    {
        var combinedValue = permission1.Value | permission2.Value | permission3.Value;
        return new Permissions(combinedValue);
    }

    public static Permissions Get(int value)
    {
        if (value < 0 || value > 7)
        {
            throw new ArgumentException("El valor debe estar entre 0 y 7.", nameof(value));
        }

        switch (value)
        {
            case 0:
                return None;
            case 1:
                return Execute;
            case 2:
                return Write;
            case 3:
                return Combine(Write, Execute);
            case 4:
                return Read;
            case 5:
                return Combine(Read, Execute);
            case 6:
                return Combine(Read, Write);
            case 7:
                return Combine(Read, Write, Execute);
            default:
                throw new ArgumentException("El valor no es válido.", nameof(value));
        }
    }

    public override string ToString() => Value.ToString();
}