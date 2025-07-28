using Serilog;

namespace DotnetPackaging.Deployment.Core;

public static class LoggerExtensions
{
    public static Maybe<ILogger> ForPlatform(this Maybe<ILogger> logger, string platform)
    {
        return logger.Map(l => l.ForContext("Platform", platform));
    }
}

