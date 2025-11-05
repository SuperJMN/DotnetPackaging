using System.Diagnostics;
using CSharpFunctionalExtensions;
using System.IO.Abstractions;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Core;
using Zafiro.FileSystem.Local;
using Zafiro.FileSystem.Readonly;

namespace DotnetPackaging.Publish;

public sealed class DotnetPublisher : IPublisher
{
    public async Task<Result<PublishResult>> Publish(ProjectPublishRequest request)
    {
        try
        {
            var outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dp-publish-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(outputDir);

            var args = BuildArgs(request, outputDir);
            var run = await Run("dotnet", args);
            if (run.IsFailure)
            {
                return Result.Failure<PublishResult>(run.Error);
            }

            // Wrap published directory as Zafiro read-only directory, then convert to a RootContainer
            var fs = new System.IO.Abstractions.FileSystem();
            var localDir = new Zafiro.FileSystem.Local.Directory(fs.DirectoryInfo.New(outputDir));
            var readOnly = await localDir.ToDirectory();
            if (readOnly.IsFailure)
            {
                return Result.Failure<PublishResult>($"Unable to materialize directory: {readOnly.Error}");
            }

            var containerResult = ContainerUtils.BuildContainer(readOnly.Value);
            if (containerResult.IsFailure)
            {
                return Result.Failure<PublishResult>(containerResult.Error);
            }

            var name = DeriveName(request.ProjectPath);
            return Result.Success(new PublishResult(containerResult.Value, name, outputDir));
        }
        catch (Exception ex)
        {
            return Result.Failure<PublishResult>(ex.Message);
        }
    }

    private static Maybe<string> DeriveName(string projectPath)
    {
        try
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(projectPath);
            return string.IsNullOrWhiteSpace(fileName) ? Maybe<string>.None : Maybe<string>.From(fileName);
        }
        catch
        {
            return Maybe<string>.None;
        }
    }

    private static string BuildArgs(ProjectPublishRequest r, string outputDir)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"publish \"{r.ProjectPath}\" ");
        sb.Append($"-c {r.Configuration} ");
        if (r.Rid.HasValue) sb.Append($"-r {r.Rid.Value} ");
        sb.Append(r.SelfContained ? "--self-contained true " : "--self-contained false ");
        if (r.SingleFile) sb.Append("/p:PublishSingleFile=true ");
        if (r.Trimmed) sb.Append("/p:PublishTrimmed=true ");
        sb.Append($"-o \"{outputDir}\"");
        return sb.ToString();
    }

    private static async Task<Result> Run(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi)!;
            await p.WaitForExitAsync();
            if (p.ExitCode != 0)
            {
                var err = await p.StandardError.ReadToEndAsync();
                var outp = await p.StandardOutput.ReadToEndAsync();
                return Result.Failure($"{fileName} {arguments}\nExitCode: {p.ExitCode}\n{outp}\n{err}");
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}