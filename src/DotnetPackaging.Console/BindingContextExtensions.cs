using System.CommandLine;
using System.CommandLine.Binding;

namespace DotnetPackaging.Console;

public static class BindingContextExtensions
{
    public static bool AllValuesAreNull(this BindingContext context, List<Option> options)
    {
        foreach (var option in options)
        {
            if (context.ParseResult.GetValueForOption(option) == null)
            {
                return false;
            }
        }
        return true;
    }
}