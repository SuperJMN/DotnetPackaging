using System.CommandLine;
using System.CommandLine.Parsing;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage;
using DotnetPackaging.AppImage.Metadata;
using DotnetPackaging.Deb;
using DotnetPackaging.Msix;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Serilog;
using Zafiro.DataModel;
using Zafiro.FileSystem.Core;

namespace DotnetPackager.Tool;

static class Program
{
    private static readonly System.IO.Abstractions.FileSystem FileSystem = new();

    public static Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3} {Platform}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var root = new RootCommand("Package a .NET project in various formats");

        root.AddCommand(CreateCommand("appimage", ".AppImage", CreateAppImage));
        root.AddCommand(CreateCommand("deb", ".deb", CreateDeb));
        root.AddCommand(CreateCommand("msix", ".msix", CreateMsix));

        return root.InvokeAsync(args);
    }

    private static System.CommandLine.Command CreateCommand(string name, string extension, Func<DirectoryInfo, FileInfo, Options, Task> handler)
    {
        var projectOption = new Option<FileInfo?>("--project", "Path to the project file") { IsRequired = false };
        var outputOption = new Option<FileInfo>("--output", $"Output file ({extension})") { IsRequired = true };
        var appName = new Option<string>("--application-name") { IsRequired = false };
        var startupWmClass = new Option<string>("--wm-class") { IsRequired = false };
        var mainCategory = new Option<MainCategory?>("--main-category") { IsRequired = false, Arity = ArgumentArity.ZeroOrOne };
        var additionalCategories = new Option<IEnumerable<AdditionalCategory>>("--additional-categories") { IsRequired = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var keywords = new Option<IEnumerable<string>>("--keywords") { IsRequired = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        var comment = new Option<string>("--comment") { IsRequired = false };
        var version = new Option<string>("--version") { IsRequired = false };
        var homePage = new Option<Uri>("--homepage") { IsRequired = false };
        var license = new Option<string>("--license") { IsRequired = false };
        var screenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls") { IsRequired = false };
        var summary = new Option<string>("--summary") { IsRequired = false };
        var appId = new Option<string>("--appId") { IsRequired = false };
        var executableName = new Option<string>("--executable-name") { IsRequired = false };
        var isTerminal = new Option<bool>("--is-terminal") { IsRequired = false };
        var iconOption = new Option<IIcon?>("--icon", GetIcon) { IsRequired = false, Description = "Path to the application icon" };

        var command = new System.CommandLine.Command(name);
        command.AddOption(projectOption);
        command.AddOption(outputOption);
        command.AddOption(appName);
        command.AddOption(startupWmClass);
        command.AddOption(mainCategory);
        command.AddOption(keywords);
        command.AddOption(comment);
        command.AddOption(iconOption);
        command.AddOption(additionalCategories);
        command.AddOption(version);
        command.AddOption(homePage);
        command.AddOption(license);
        command.AddOption(screenshotUrls);
        command.AddOption(summary);
        command.AddOption(appId);
        command.AddOption(executableName);
        command.AddOption(isTerminal);

        var options = new OptionsBinder(
            appName,
            startupWmClass,
            keywords,
            comment,
            mainCategory,
            additionalCategories,
            iconOption,
            version,
            homePage,
            license,
            screenshotUrls,
            summary,
            appId,
            executableName,
            isTerminal);

        command.SetHandler(async (FileInfo? project, FileInfo output, Options opts) =>
        {
            var publishDirResult = await PublishProject(project);
            if (publishDirResult.IsFailure)
            {
                Log.Error(publishDirResult.Error);
                return;
            }

            await handler(publishDirResult.Value, output, opts);
        }, projectOption, outputOption, options);

        return command;
    }

    private static async Task<Result<DirectoryInfo>> PublishProject(FileInfo? project)
    {
        var projectResult = project != null
            ? Result.Success(project)
            : DiscoverProject();

        if (projectResult.IsFailure)
        {
            return Result.Failure<DirectoryInfo>(projectResult.Error);
        }

        var temp = Directory.CreateTempSubdirectory();
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"publish \"{projectResult.Value.FullName}\" -c Release -o \"{temp.FullName}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = System.Diagnostics.Process.Start(psi)!;
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            return Result.Failure<DirectoryInfo>($"dotnet publish failed: {error}");
        }

        return Result.Success(new DirectoryInfo(temp.FullName));
    }

    private static Result<FileInfo> DiscoverProject()
    {
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        var projectFile = currentDir.GetFiles("*.csproj").FirstOrDefault();
        return projectFile != null
            ? Result.Success(projectFile)
            : Result.Failure<FileInfo>("No project specified and no project found in current directory");
    }

    private static Task CreateAppImage(DirectoryInfo inputDir, FileInfo outputFile, Options options)
    {
        var container = new Zafiro.DivineBytes.System.IO.DirectoryContainer(FileSystem.DirectoryInfo.New(inputDir.FullName)).AsRoot();

        var metadata = new AppImageMetadata(
            options.Id.GetValueOrDefault("app.id"),
            options.Name.GetValueOrDefault("App"),
            options.Name.GetValueOrDefault("app"))
        {
            Comment = options.Comment,
            Version = options.Version,
            Homepage = options.HomePage.Map(u => u.ToString()),
            ProjectLicense = options.License,
            Keywords = options.Keywords,
            Summary = options.Summary,
            Categories = options.MainCategory.Map(mc =>
                new[] { mc.ToString() }.Concat(options.AdditionalCategories.GetValueOrDefault(Array.Empty<AdditionalCategory>()).Select(ac => ac.ToString())))
        };

        return new AppImageFactory()
            .Create(container.AsContainer(), metadata)
            .Bind(x => x.ToByteSource())
            .Bind(data =>
            {
                var stream = outputFile.Open(FileMode.Create);
                return data.WriteTo(stream)
                    .ToList()
                    .Select(list => list.Combine())
                    .ToTask();
            })
            .WriteResult();
    }

    private static Task CreateDeb(DirectoryInfo inputDir, FileInfo outputFile, Options options)
    {
        return new Zafiro.FileSystem.Local.Directory(FileSystem.DirectoryInfo.New(inputDir.FullName))
            .ToDirectory()
            .Bind(directory => DebFile.From()
                .Directory(directory)
                .Configure(configuration => configuration.From(options))
                .Build()
                .Map(DotnetPackaging.Deb.Archives.Deb.DebMixin.ToData)
                .Bind(async data =>
                {
                    await using var fileSystemStream = outputFile.Open(FileMode.Create);
                    return await data.DumpTo(fileSystemStream);
                }))
            .WriteResult();
    }

    private static Task CreateMsix(DirectoryInfo inputDir, FileInfo outputFile, Options options)
    {
        var container = new Zafiro.DivineBytes.System.IO.DirectoryContainer(FileSystem.DirectoryInfo.New(inputDir.FullName)).AsRoot().AsContainer();
        return Msix.FromDirectory(container, Log.Logger.AsMaybe())
            .Bind(source =>
            {
                var fs = outputFile.Open(FileMode.Create);
                return source.WriteTo(fs)
                    .ToList()
                    .Select(list => list.Combine())
                    .ToTask();
            })
            .WriteResult();
    }

    private static IIcon? GetIcon(ArgumentResult result)
    {
        var iconPath = result.Tokens[0].Value;
        var icon = Data.FromFileInfo(FileSystem.FileInfo.New(iconPath));
        icon.Map(Icon.FromData);
        if (icon.IsFailure)
        {
            result.ErrorMessage = $"Invalid icon '{iconPath}': {icon.Error}";
        }

        return null;
    }
}
