namespace DotnetPackaging;

public record Categories(MainCategory Main, params AdditionalCategory[] AdditionalCategories)
{
    public override string ToString() => string.Join(";", new[] { Main.ToString() }.Concat(AdditionalCategories.Select(x => x.ToString())));
}