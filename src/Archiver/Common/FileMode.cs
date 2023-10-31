namespace Archiver.Common;

public record FileMode()
{
    public required Permissions User { get; init; }
    public required Permissions Group { get; init; }
    public required Permissions Others { get; init; }

    public override string ToString() => $"{User}{Group}{Others}";

    public static FileMode Parse(string octalString)
    {
        if (octalString.Length != 3)
            throw new ArgumentException("La cadena octal debe tener exactamente 3 caracteres.", nameof(octalString));

        Permissions user = Permissions.None;
        Permissions group = Permissions.None;
        Permissions others = Permissions.None;

        if (int.TryParse(octalString[0].ToString(), out int userValue))
        {
            user = Permissions.Get(userValue);
        }

        if (int.TryParse(octalString[1].ToString(), out int groupValue))
        {
            group = Permissions.Get(groupValue);
        }

        if (int.TryParse(octalString[2].ToString(), out int othersValue))
        {
            others = Permissions.Get(othersValue);
        }

        return new FileMode { User = user, Group = group, Others = others };
    }
}
