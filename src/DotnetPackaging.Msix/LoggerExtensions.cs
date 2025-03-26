using CSharpFunctionalExtensions;

namespace MsixPackaging;

public static class LoggerExtensions
{
    public static void Debug(this Maybe<ILogger> maybeLogger, string message, params object[] args)
        => maybeLogger.Execute(logger => logger.Debug(message, args));

    public static void Info(this Maybe<ILogger> maybeLogger, string message, params object[] args)
        => maybeLogger.Execute(logger => logger.Information(message, args));

    public static void Warn(this Maybe<ILogger> maybeLogger, string message, params object[] args)
        => maybeLogger.Execute(logger => logger.Warning(message, args));

    public static void Error(this Maybe<ILogger> maybeLogger, string message, params object[] args)
        => maybeLogger.Execute(logger => logger.Error(message, args));
}