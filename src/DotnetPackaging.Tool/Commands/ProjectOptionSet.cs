using System.CommandLine;
using System.Runtime.InteropServices;
using Serilog;

namespace DotnetPackaging.Tool.Commands;

public class ProjectOptionSet
{
    public Option<FileInfo> Project { get; }
    public Option<string?> Arch { get; }
    public Option<bool> SelfContained { get; }
    public Option<string> Configuration { get; }
    public Option<bool> SingleFile { get; }
    public Option<bool> Trimmed { get; }
    public Option<FileInfo> Output { get; }

    public ProjectOptionSet(string extension, bool singleFileDefault = false)
    {
        Project = new Option<FileInfo>("--project") { Description = "Path to the .csproj file", Required = true };
        Arch = new Option<string?>("--arch") { Description = "Target architecture (x64, arm64). Auto-detects from current system if not specified." };
        SelfContained = new Option<bool>("--self-contained") { Description = "Publish self-contained [Deprecated]" };
        SelfContained.DefaultValueFactory = _ => true;
        Configuration = new Option<string>("--configuration") { Description = "Build configuration" };
        Configuration.DefaultValueFactory = _ => "Release";
        SingleFile = new Option<bool>("--single-file") { Description = "Publish single-file" };
        if (singleFileDefault)
        {
            SingleFile.DefaultValueFactory = _ => true;
        }
        Trimmed = new Option<bool>("--trimmed") { Description = "Enable trimming" };
        Output = new Option<FileInfo>("--output") { Description = $"Destination path for the generated {extension}", Required = true };
    }

    public void AddTo(Command command)
    {
        command.Add(Project);
        command.Add(Arch);
        command.Add(SelfContained);
        command.Add(Configuration);
        command.Add(SingleFile);
        command.Add(Trimmed);
        command.Add(Output);
    }

    public static string? AutoDetectArch(ILogger logger)
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.Arm => "arm",
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            _ => null
        };

        if (arch != null)
        {
            logger.Information("Architecture not specified, auto-detected: {Arch}", arch);
        }
        else
        {
            logger.Error("Unable to auto-detect architecture. Please specify --arch explicitly (e.g., --arch x64)");
        }

        return arch;
    }
}
