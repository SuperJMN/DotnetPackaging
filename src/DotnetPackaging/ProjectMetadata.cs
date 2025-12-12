using System.Xml.Linq;
using Zafiro.Commands;

namespace DotnetPackaging;

public sealed record ProjectMetadata(
    Maybe<string> Product,
    Maybe<string> Company,
    Maybe<string> AssemblyName,
    Maybe<string> AssemblyTitle);

public static class ProjectMetadataReader
{
    private static readonly string[] PropertiesToRead = new[] { "Product", "Company", "AssemblyName", "AssemblyTitle" };

    public static Result<ProjectMetadata> Read(FileInfo projectFile)
    {
        return ReadInternal(projectFile, Maybe<ILogger>.None);
    }

    public static Maybe<ProjectMetadata> TryRead(FileInfo projectFile, ILogger logger)
    {
        var metadataResult = ReadInternal(projectFile, Maybe<ILogger>.From(logger));
        if (metadataResult.IsFailure)
        {
            logger.Warning(
                "Unable to read project metadata from {ProjectFile}: {Error}",
                projectFile.FullName,
                metadataResult.Error);
            return Maybe<ProjectMetadata>.None;
        }

        return Maybe<ProjectMetadata>.From(metadataResult.Value);
    }

    private static Result<ProjectMetadata> ReadInternal(FileInfo projectFile, Maybe<ILogger> logger)
    {
        if (!projectFile.Exists)
        {
            return Result.Failure<ProjectMetadata>($"Project file not found: {projectFile.FullName}");
        }

        var msbuild = ReadWithDotnetMsbuild(projectFile, logger);
        if (msbuild.IsSuccess)
        {
            return msbuild;
        }

        return Result.Try(() =>
            ReadFromXml(projectFile),
            ex => $"Failed to read project metadata from {projectFile.FullName}: {ex.Message}");
    }

    private static Result<ProjectMetadata> ReadWithDotnetMsbuild(FileInfo projectFile, Maybe<ILogger> logger)
    {
        var arguments = $"msbuild \"{projectFile.FullName}\" -nologo -v:q -getProperty:{string.Join(";", PropertiesToRead)}";
        try
        {
            var commandLogger = logger.Map(l => l.ForContext("Module", "COMMAND"));
            var command = new Command(commandLogger);
            var run = command.Execute("dotnet", arguments).GetAwaiter().GetResult();
            if (run.IsFailure)
            {
                logger.Execute(l => l.Debug("dotnet msbuild metadata read failed: {Error}", run.Error));
                return Result.Failure<ProjectMetadata>($"dotnet msbuild metadata read failed: {run.Error}");
            }

            var output = string.Join("\n", run.Value);
            var values = ParseMsbuildOutput(output, PropertiesToRead);

            var product = values.GetValueOrDefault("Product");
            var company = values.GetValueOrDefault("Company");
            var assemblyName = values.GetValueOrDefault("AssemblyName");
            var assemblyTitle = values.GetValueOrDefault("AssemblyTitle");

            return Result.Success(new ProjectMetadata(product, company, assemblyName, assemblyTitle));
        }
        catch (Exception ex)
        {
            logger.Execute(l => l.Debug(ex, "Failed to read project metadata via dotnet msbuild"));
            return Result.Failure<ProjectMetadata>($"Failed to read project metadata via dotnet msbuild: {ex.Message}");
        }
    }

    private static ProjectMetadata ReadFromXml(FileInfo projectFile)
    {
        var document = XDocument.Load(projectFile.FullName);
        var product = ReadProperty(document, "Product");
        var company = ReadProperty(document, "Company");
        var assemblyName = ReadProperty(document, "AssemblyName");
        var assemblyTitle = ReadProperty(document, "AssemblyTitle");
        return new ProjectMetadata(product, company, assemblyName, assemblyTitle);
    }

    private static Dictionary<string, Maybe<string>> ParseMsbuildOutput(string output, IEnumerable<string> propertyNames)
    {
        var values = propertyNames.ToDictionary(p => p, _ => Maybe<string>.None, StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(propertyNames, StringComparer.OrdinalIgnoreCase);
        string? pending = null;

        foreach (var rawLine in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            if (pending != null)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                if (char.IsWhiteSpace(rawLine[0]))
                {
                    var value = rawLine.Trim();
                    values[pending] = string.IsNullOrWhiteSpace(value) ? Maybe<string>.None : Maybe<string>.From(value);
                }

                pending = null;
                continue;
            }

            var trimmed = rawLine.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex > 0)
            {
                var name = trimmed[..colonIndex].Trim();
                if (names.Contains(name))
                {
                    var value = trimmed[(colonIndex + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        pending = name;
                    }
                    else
                    {
                        values[name] = Maybe<string>.From(value);
                    }

                    continue;
                }
            }

            if (names.Contains(trimmed.TrimEnd(':')))
            {
                pending = trimmed.TrimEnd(':');
            }
        }

        return values;
    }

    private static Maybe<string> ReadProperty(XDocument document, string propertyName)
    {
        var element = document
            .Descendants()
            .FirstOrDefault(x => string.Equals(x.Name.LocalName, propertyName, StringComparison.OrdinalIgnoreCase));

        if (element is null)
        {
            return Maybe<string>.None;
        }

        var value = element.Value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? Maybe<string>.None : Maybe<string>.From(value);
    }
}
