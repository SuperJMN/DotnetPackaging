namespace DotnetPackaging.Deb.Archives.Tar;

public static class UnixPermissions
{
    public static int Parse(string octal)
    {
        if (string.IsNullOrWhiteSpace(octal))
        {
            throw new ArgumentException("Mode cannot be null or whitespace", nameof(octal));
        }

        return Convert.ToInt32(octal, 8);
    }
}
