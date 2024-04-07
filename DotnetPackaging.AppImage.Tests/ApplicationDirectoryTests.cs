using System.IO.Abstractions;
using System.Security.Policy;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using FluentAssertions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging.AppImage.Tests;

public class ApplicationDirectoryTests
{
    //[Fact]
    //public async Task Test()
    //{
    //    var fs = new FileSystemRoot(new WindowsZafiroFileSystem(new FileSystem()));
    //    var contents = fs.GetDirectory("C:/Users/JMN/Desktop/AvaloniaSyncer.AppDir/usr/bin/AvaloniaSyncer");
    //    var appImage = await new AppImageBuilder()
    //        .WithApplicationExecutableName("AvaloniaSyncer.Desktop")
    //        .Build(contents, new TestRuntime())
    //        .Map(image => ApplicationDirectory.Create());
    //    appImage.Should().Succeed();
    //}
}