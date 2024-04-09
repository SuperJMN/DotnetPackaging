using DotnetPackaging.AppImage.Model;

namespace DotnetPackaging.AppImage.Core;

public class DefaultScriptAppRun : ScriptAppRun
{
    public DefaultScriptAppRun(string executablePath) : base($"#!/usr/bin/env sh\n\"$APPDIR/{executablePath}\" \"$@\"")
    {
    }
}