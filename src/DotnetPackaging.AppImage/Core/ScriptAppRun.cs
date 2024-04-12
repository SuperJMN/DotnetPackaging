using CSharpFunctionalExtensions;

namespace DotnetPackaging.AppImage.Core;

public class ScriptAppRun : IAppRun
{
    public string Script { get; }

    public ScriptAppRun(string script)
    {
        Script = script;
    }

    public Func<Task<Result<Stream>>> Open => () => Task.FromResult(Result.Success((Stream)new MemoryStream(Script.GetAsciiBytes())));
}

