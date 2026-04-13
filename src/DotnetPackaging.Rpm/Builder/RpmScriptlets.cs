namespace DotnetPackaging.Rpm.Builder;

internal static class RpmScriptlets
{
    public static string PostInstall(string package)
    {
        return $"""
               #!/bin/sh
               systemctl daemon-reload
               systemctl enable {package}.service
               if [ "$1" -eq 1 ]; then
                   systemctl start {package}.service
               fi
               """;
    }

    public static string PreUninstall(string package)
    {
        return $"""
               #!/bin/sh
               if [ "$1" -eq 0 ]; then
                   systemctl stop {package}.service || true
                   systemctl disable {package}.service || true
               fi
               """;
    }

    public static string PostUninstall(string package)
    {
        return $"""
               #!/bin/sh
               systemctl daemon-reload
               """;
    }
}
