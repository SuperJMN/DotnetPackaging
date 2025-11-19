using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CSharpFunctionalExtensions;

namespace DotnetPackaging;

public sealed record ProjectMetadata(
    Maybe<string> Product,
    Maybe<string> Company,
    Maybe<string> AssemblyName,
    Maybe<string> AssemblyTitle);

public static class ProjectMetadataReader
{
    public static Result<ProjectMetadata> Read(FileInfo projectFile)
    {
        if (!projectFile.Exists)
        {
            return Result.Failure<ProjectMetadata>($"Project file not found: {projectFile.FullName}");
        }

        return Result.Try(() =>
        {
            var document = XDocument.Load(projectFile.FullName);
            var product = ReadProperty(document, "Product");
            var company = ReadProperty(document, "Company");
            var assemblyName = ReadProperty(document, "AssemblyName");
            var assemblyTitle = ReadProperty(document, "AssemblyTitle");

            return new ProjectMetadata(product, company, assemblyName, assemblyTitle);
        }, ex => $"Failed to read project metadata from {projectFile.FullName}: {ex.Message}");
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
