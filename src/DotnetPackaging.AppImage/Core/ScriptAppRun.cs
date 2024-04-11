using CSharpFunctionalExtensions;
using DotnetPackaging.Common;

namespace DotnetPackaging.AppImage.Model;

public class ScriptAppRun : IAppRun
{
    public string Script { get; }

    public ScriptAppRun(string script)
    {
        Script = script;
    }

    public Func<Task<Result<Stream>>> StreamFactory => () => Task.FromResult(Result.Success((Stream)new MemoryStream(Script.GetAsciiBytes())));
}

