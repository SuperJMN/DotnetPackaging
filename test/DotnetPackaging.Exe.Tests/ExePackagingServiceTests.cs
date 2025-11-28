using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe;
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

        var service = new ExePackagingService();
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

        var service = new ExePackagingService();
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

        var service = new ExePackagingService();
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

        var service = new ExePackagingService();
        var inferred = InvokeInferExecutableName(service, publishOutput, Maybe<string>.None);

        inferred.HasValue.Should().BeFalse();
    }

    private static Maybe<string> InvokeInferExecutableName(ExePackagingService service, IContainer container, Maybe<string> projectName)
    {
        var result = InferExecutableNameMethod.Invoke(service, new object[] { container, projectName });
        return (Maybe<string>)result!;
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
}
