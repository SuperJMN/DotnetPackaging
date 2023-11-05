using DotnetPackaging.Deb;

namespace DotnetPackaging.Tests.Deb.EndToEnd;

public record ExecutableMetadata(string CommandName, DesktopEntry DesktopEntry);