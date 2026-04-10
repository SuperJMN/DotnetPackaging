using Zafiro.Mixins;

namespace DotnetPackaging;

public static class TextTemplates
{
    public static string DesktopFileContents(string executablePath, PackageMetadata metadata, string? iconNameOverride = null)
    {
        var iconName = iconNameOverride ?? (metadata.IconFiles.Any() ? metadata.Package.ToLowerInvariant() : null);
        var items = new[]
        {
            Maybe.From("[Desktop Entry]"),
            Maybe.From("Type=Application"),
            Maybe.From($"Name={metadata.Name}"),
            metadata.StartupWmClass.Map(n => $"StartupWMClass={n}"),
            metadata.Comment.Map(n => $"Comment={n}"),
            iconName is not null ? Maybe.From($"Icon={iconName}") : Maybe<string>.None,
            Maybe.From($"Terminal={metadata.IsTerminal.ToString().ToLower()}"),
            Maybe.From($"Exec=\"{executablePath}\""),
            metadata.Categories.Map(x => $"Categories={x}"),
            metadata.Keywords.Map(keywords => $"Keywords={keywords.JoinWith(";")}"),
            Maybe.From(metadata.Version).Map(s => $"X-AppImage-Version={s}"),
            metadata.Vendor.Map(v => $"X-Developer-Name={v}"),
        };

        return items.Compose() + "\n";
    }

    public static string RunScript(string executablePath)
    {
        return $"#!/usr/bin/env sh\n\"{executablePath}\" \"$@\"";
    }

    public static string AppStream(PackageMetadata packageMetadata)
    {
        return AppStreamXmlGenerator.GenerateXml(packageMetadata).ToString();
    }

    public static string SystemdUnitFile(string executablePath, string workingDirectory, PackageMetadata metadata)
    {
        var svc = metadata.Service.GetValueOrDefault(new ServiceDefinition());
        var type = svc.Type.GetValueOrDefault(ServiceType.Simple);
        var restart = svc.Restart.GetValueOrDefault(RestartPolicy.OnFailure);
        var restartSec = svc.RestartSec.GetValueOrDefault(10);
        var after = svc.After.GetValueOrDefault("network-online.target");
        var wantedBy = svc.WantedBy.GetValueOrDefault("multi-user.target");
        var description = metadata.Description.GetValueOrDefault(metadata.Name);

        var lines = new List<string>
        {
            "[Unit]",
            $"Description={description}",
            $"After={after}",
            $"Wants={after}",
            "",
            "[Service]",
            $"Type={FormatServiceType(type)}",
            $"ExecStart={executablePath}",
            $"WorkingDirectory={workingDirectory}",
            $"Restart={FormatRestartPolicy(restart)}",
            $"RestartSec={restartSec}",
            $"SyslogIdentifier={metadata.Package}",
        };

        if (svc.User.HasValue)
        {
            lines.Add($"User={svc.User.Value}");
        }

        if (svc.Group.HasValue)
        {
            lines.Add($"Group={svc.Group.Value}");
        }

        if (svc.Environment.HasValue)
        {
            foreach (var env in svc.Environment.Value)
            {
                lines.Add($"Environment={env}");
            }
        }

        lines.Add("");
        lines.Add("[Install]");
        lines.Add($"WantedBy={wantedBy}");
        lines.Add("");

        return string.Join("\n", lines);
    }

    public static string PostInstScript(string package)
    {
        return $"""
               #!/bin/sh
               set -e
               if [ "$1" = "configure" ]; then
                   systemctl daemon-reload
                   systemctl enable {package}.service
                   systemctl start {package}.service
               fi
               """;
    }

    public static string PreRmScript(string package)
    {
        return $"""
               #!/bin/sh
               set -e
               if [ "$1" = "remove" ] || [ "$1" = "upgrade" ]; then
                   systemctl stop {package}.service || true
                   systemctl disable {package}.service || true
               fi
               """;
    }

    public static string PostRmScript(string package)
    {
        return $"""
               #!/bin/sh
               set -e
               if [ "$1" = "purge" ]; then
                   systemctl daemon-reload
               fi
               """;
    }

    private static string FormatServiceType(ServiceType type) => type switch
    {
        ServiceType.Simple => "simple",
        ServiceType.Notify => "notify",
        ServiceType.Forking => "forking",
        ServiceType.OneShot => "oneshot",
        ServiceType.Idle => "idle",
        _ => "simple"
    };

    private static string FormatRestartPolicy(RestartPolicy policy) => policy switch
    {
        RestartPolicy.No => "no",
        RestartPolicy.Always => "always",
        RestartPolicy.OnFailure => "on-failure",
        RestartPolicy.OnAbnormal => "on-abnormal",
        RestartPolicy.OnAbort => "on-abort",
        RestartPolicy.OnWatchdog => "on-watchdog",
        _ => "on-failure"
    };
}
