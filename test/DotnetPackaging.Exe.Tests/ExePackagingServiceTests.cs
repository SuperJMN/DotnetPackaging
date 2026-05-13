using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe;
using DotnetPackaging.Publish;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Zafiro.DivineBytes;
using Xunit;
using DivinePath = Zafiro.DivineBytes.Path;

namespace DotnetPackaging.Exe.Tests;

public class ExePackagingServiceTests
{
    private static readonly MethodInfo InferExecutableNameMethod =
        typeof(ExePackagingService).GetMethod("InferExecutableName", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Unable to locate InferExecutableName on ExePackagingService.");

    [Fact]
    public void Infers_executable_matching_project_name_from_publish_output()
    {
        var publishOutput = DotnetPublishOutputDouble.Simulate(
            "TestApp.Desktop.exe",
            "TestApp.Desktop.dll",
            "TestApp.Desktop.runtimeconfig.json",
            "runtimes/win-x64/native/createdump.exe",
            "tools/helper.exe");

        var service = CreateService();
        var inferred = InvokeInferExecutableName(service, publishOutput, Maybe<string>.From("TestApp.Desktop"));

        inferred.HasValue.Should().BeTrue();
        inferred.Value.Should().Be("TestApp.Desktop.exe");
    }

    [Fact]
    public void Prefers_single_candidate_when_project_name_is_missing()
    {
        var publishOutput = DotnetPublishOutputDouble.Simulate(
            "win-x64/publish/Application.exe",
            "win-x64/publish/Application.dll");

        var service = CreateService();
        var inferred = InvokeInferExecutableName(service, publishOutput, Maybe<string>.None);

        var expected = new DivinePath("win-x64/publish/Application.exe")
            .MakeRelativeTo(DivinePath.Empty)
            .ToString();

        inferred.HasValue.Should().BeTrue();
        inferred.Value.Should().Be(expected);
    }

    [Fact]
    public void Chooses_shallowest_candidate_when_multiple_remain()
    {
        var publishOutput = DotnetPublishOutputDouble.Simulate(
            "bin/Release/net8.0/win-x64/App.exe",
            "tools/cli/Helper.exe",
            "Helper.exe");

        var service = CreateService();
        var inferred = InvokeInferExecutableName(service, publishOutput, Maybe<string>.None);

        inferred.HasValue.Should().BeTrue();
        inferred.Value.Should().Be("Helper.exe");
    }

    [Fact]
    public void Returns_none_when_only_createdump_is_present()
    {
        var publishOutput = DotnetPublishOutputDouble.Simulate(
            "createdump.exe",
            "runtimes/win-x64/native/createdump.exe");

        var service = CreateService();
        var inferred = InvokeInferExecutableName(service, publishOutput, Maybe<string>.None);

        inferred.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task Discovers_setup_logo_from_publish_output_assets_directory()
    {
        var publishOutput = DotnetPublishOutputDouble.Simulate(
            "App.exe",
            "Assets/logo.png");

        var logo = SetupLogoDiscovery.Discover(
            publishOutput,
            Maybe<FileInfo>.None,
            Serilog.Log.Logger);

        logo.HasValue.Should().BeTrue();
        await using var stream = logo.Value.ToStreamSeekable();
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync()).Should().Be("png:Assets/logo.png");
    }

    [Fact]
    public async Task Discovers_setup_logo_from_repository_assets_directory()
    {
        var repo = Directory.CreateTempSubdirectory("dp-logo-repo-");
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(repo.FullName, ".git"));
            var assets = Directory.CreateDirectory(System.IO.Path.Combine(repo.FullName, "assets"));
            var logoPath = System.IO.Path.Combine(assets.FullName, "logo.png");
            await File.WriteAllTextAsync(logoPath, "repo-logo");

            var projectDirectory = Directory.CreateDirectory(System.IO.Path.Combine(repo.FullName, "src", "App.Desktop"));
            var project = new FileInfo(System.IO.Path.Combine(projectDirectory.FullName, "App.Desktop.csproj"));
            await File.WriteAllTextAsync(project.FullName, "<Project />");

            var publishOutput = DotnetPublishOutputDouble.Simulate("App.exe");
            var logo = SetupLogoDiscovery.Discover(
                publishOutput,
                Maybe<FileInfo>.From(project),
                Serilog.Log.Logger);

            logo.HasValue.Should().BeTrue();
            await using var stream = logo.Value.ToStreamSeekable();
            using var reader = new StreamReader(stream);
            (await reader.ReadToEndAsync()).Should().Be("repo-logo");
        }
        finally
        {
            try { Directory.Delete(repo.FullName, true); } catch { }
        }
    }

    [Fact]
    public async Task From_published_project_discovers_setup_logo_from_repository_assets_directory()
    {
        var repo = Directory.CreateTempSubdirectory("dp-logo-project-");
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(repo.FullName, ".git"));
            var assets = Directory.CreateDirectory(System.IO.Path.Combine(repo.FullName, "assets"));
            await File.WriteAllTextAsync(System.IO.Path.Combine(assets.FullName, "logo.png"), "repo-logo");

            var projectDirectory = Directory.CreateDirectory(System.IO.Path.Combine(repo.FullName, "src", "App.Desktop"));
            var project = new FileInfo(System.IO.Path.Combine(projectDirectory.FullName, "App.Desktop.csproj"));
            await File.WriteAllTextAsync(project.FullName, "<Project />");

            var context = ProjectPackagingContext.FromProject(project.FullName);
            context.IsSuccess.Should().BeTrue(context.IsFailure ? context.Error : string.Empty);

            var publishOutput = DotnetPublishOutputDouble.Simulate("App.Desktop.exe");
            var installer = new ExePackager(logger: Serilog.Log.Logger)
                .FromPublishedProject(
                    publishOutput,
                    context.Value,
                    metadata =>
                    {
                        metadata.Stub = Maybe.From((IByteSource)ByteSource.FromString("stub"));
                        metadata.RuntimeIdentifier = Maybe.From("win-x64");
                        metadata.OutputName = Maybe.From("setup.exe");
                    },
                    Serilog.Log.Logger);

            var output = System.IO.Path.Combine(repo.FullName, "setup.exe");
            var write = await installer.WriteTo(output);
            write.IsSuccess.Should().BeTrue(write.IsFailure ? write.Error : string.Empty);

            using var archive = new ZipArchive(new MemoryStream(ReadPayload(output)), ZipArchiveMode.Read);
            archive.GetEntry("Branding/logo.png").Should().NotBeNull();

            var metadata = archive.GetEntry("metadata.json");
            metadata.Should().NotBeNull();
            using var metadataStream = metadata!.Open();
            using var reader = new StreamReader(metadataStream);
            var json = await reader.ReadToEndAsync();
            json.Should().Contain("\"hasLogo\":true");
        }
        finally
        {
            try { Directory.Delete(repo.FullName, true); } catch { }
        }
    }

    [Fact]
    public async Task From_published_project_prefers_application_info_logo_before_publish_output_logo()
    {
        var repo = Directory.CreateTempSubdirectory("dp-logo-appinfo-");
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(repo.FullName, ".git"));
            var assets = Directory.CreateDirectory(System.IO.Path.Combine(repo.FullName, "assets"));
            await File.WriteAllTextAsync(System.IO.Path.Combine(assets.FullName, "logo.png"), "project-logo");

            var projectDirectory = Directory.CreateDirectory(System.IO.Path.Combine(repo.FullName, "src", "App.Desktop"));
            var project = new FileInfo(System.IO.Path.Combine(projectDirectory.FullName, "App.Desktop.csproj"));
            await File.WriteAllTextAsync(project.FullName, """
                <Project>
                  <PropertyGroup>
                    <ApplicationLogo>..\..\assets\logo.png</ApplicationLogo>
                  </PropertyGroup>
                </Project>
                """);

            var context = ProjectPackagingContext.FromProject(project.FullName);
            context.IsSuccess.Should().BeTrue(context.IsFailure ? context.Error : string.Empty);

            var publishOutput = DotnetPublishOutputDouble.Simulate(
                "App.Desktop.exe",
                "Assets/logo.png");
            var installer = new ExePackager(logger: Serilog.Log.Logger)
                .FromPublishedProject(
                    publishOutput,
                    context.Value,
                    metadata =>
                    {
                        metadata.Stub = Maybe.From((IByteSource)ByteSource.FromString("stub"));
                        metadata.RuntimeIdentifier = Maybe.From("win-x64");
                        metadata.OutputName = Maybe.From("setup.exe");
                    },
                    Serilog.Log.Logger);

            var output = System.IO.Path.Combine(repo.FullName, "setup.exe");
            var write = await installer.WriteTo(output);
            write.IsSuccess.Should().BeTrue(write.IsFailure ? write.Error : string.Empty);

            using var archive = new ZipArchive(new MemoryStream(ReadPayload(output)), ZipArchiveMode.Read);
            var logo = archive.GetEntry("Branding/logo.png");
            logo.Should().NotBeNull();
            using var logoStream = logo!.Open();
            using var reader = new StreamReader(logoStream);
            (await reader.ReadToEndAsync()).Should().Be("project-logo");
        }
        finally
        {
            try { Directory.Delete(repo.FullName, true); } catch { }
        }
    }

    private static Maybe<string> InvokeInferExecutableName(ExePackagingService service, IContainer container, Maybe<string> projectName)
    {
        var result = InferExecutableNameMethod.Invoke(service, new object[] { container, projectName });
        return (Maybe<string>)result!;
    }

    private static byte[] ReadPayload(string installerPath)
    {
        var magic = Encoding.ASCII.GetBytes("DPACKEXE1");
        using var stream = File.OpenRead(installerPath);
        var searchWindow = (int)Math.Min(4096, stream.Length);
        var buffer = new byte[searchWindow];
        stream.Seek(-searchWindow, SeekOrigin.End);
        stream.ReadExactly(buffer);

        var magicPosition = -1;
        for (var i = buffer.Length - magic.Length; i >= 0; i--)
        {
            if (magic.Select((value, index) => buffer[i + index] == value).All(match => match))
            {
                magicPosition = i;
                break;
            }
        }

        magicPosition.Should().BeGreaterThanOrEqualTo(8);

        var absoluteMagicPosition = stream.Length - searchWindow + magicPosition;
        stream.Position = absoluteMagicPosition - 8;
        Span<byte> lengthBytes = stackalloc byte[8];
        stream.ReadExactly(lengthBytes);
        var length = BitConverter.ToInt64(lengthBytes);
        var payload = new byte[(int)length];
        stream.Position = absoluteMagicPosition - 8 - length;
        stream.ReadExactly(payload);
        return payload;
    }

    private abstract class ContainerNode : IContainer
    {
        protected List<INamedContainer> SubcontainerList { get; } = new();
        protected List<INamedByteSource> ResourceList { get; } = new();

        public IEnumerable<INamedContainer> Subcontainers => SubcontainerList;
        public IEnumerable<INamedByteSource> Resources => ResourceList;

        protected ContainerNode EnsureDirectory(IEnumerable<string> fragments)
        {
            var current = this;
            foreach (var fragment in fragments)
            {
                var match = current.SubcontainerList
                    .OfType<DotnetPublishDirectoryDouble>()
                    .FirstOrDefault(d => string.Equals(d.Name, fragment, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    match = new DotnetPublishDirectoryDouble(fragment);
                    current.SubcontainerList.Add(match);
                }

                current = match;
            }

            return current;
        }

        internal void AddResource(INamedByteSource resource)
        {
            ResourceList.Add(resource);
        }
    }

    private sealed class DotnetPublishDirectoryDouble : ContainerNode, INamedContainer
    {
        public DotnetPublishDirectoryDouble(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    private sealed class DotnetPublishOutputDouble : ContainerNode
    {
        public static IContainer Simulate(params string[] relativeFilePaths)
        {
            var root = new DotnetPublishOutputDouble();
            foreach (var relative in relativeFilePaths)
            {
                root.Add(new DivinePath(relative.Replace("\\", "/")));
            }

            return root;
        }

        private void Add(DivinePath filePath)
        {
            if (!filePath.RouteFragments.Any())
            {
                return;
            }

            var parent = EnsureDirectory(filePath.RouteFragments.SkipLast(1));
            var extension = filePath.Extension().GetValueOrDefault("noext");
            var relative = filePath.MakeRelativeTo(DivinePath.Empty).ToString();
            var payload = Encoding.UTF8.GetBytes($"{extension}:{relative}");
            parent.AddResource(new Resource(filePath.Name(), ByteSource.FromBytes(payload)));
        }
    }

    private static ExePackagingService CreateService()
    {
        var logger = Serilog.Log.Logger;
        var publisher = new DotnetPublisher();
        var httpFactory = new FakeHttpClientFactory();
        var stubProvider = new InstallerStubProvider(logger, httpFactory, publisher);
        return new ExePackagingService(publisher, stubProvider, logger);
    }

    private class FakeHttpClientFactory : System.Net.Http.IHttpClientFactory
    {
        public System.Net.Http.HttpClient CreateClient(string name) => new System.Net.Http.HttpClient();
    }
}
