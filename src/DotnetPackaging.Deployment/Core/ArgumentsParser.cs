using Zafiro.Mixins;

namespace DotnetPackaging.Deployment.Core;

internal static class ArgumentsParser
{
    public static string Parse(IEnumerable<string []> options, IEnumerable<string []> properties)
    {
        var optionsStr = options.Select(strings => $"--{strings[0]} {strings[1]}").JoinWith(" ");
        var propertiesStr = properties.Select(strings => $"-p:{strings[0]}={strings[1]}").JoinWith(" ");
        return optionsStr + " " + propertiesStr;
    }
}