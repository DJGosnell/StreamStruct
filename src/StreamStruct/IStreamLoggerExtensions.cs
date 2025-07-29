namespace StreamStruct;

/// <summary>
/// Extension methods for IStreamLogger to provide convenient logging methods.
/// </summary>
public static class IStreamLoggerExtensions
{
    /// <summary>
    /// Logs a trace message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    public static void LogTrace(this IStreamLogger logger, string message)
    {
        logger.Log(LogLevel.Trace, message);
    }

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    public static void LogDebug(this IStreamLogger logger, string message)
    {
        logger.Log(LogLevel.Debug, message);
    }

    /// <summary>
    /// Logs an information message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    public static void LogInfo(this IStreamLogger logger, string message)
    {
        logger.Log(LogLevel.Info, message);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    public static void LogWarning(this IStreamLogger logger, string message)
    {
        logger.Log(LogLevel.Warning, message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    public static void LogError(this IStreamLogger logger, string message)
    {
        logger.Log(LogLevel.Error, message);
    }

    /// <summary>
    /// Logs a fatal message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    public static void LogFatal(this IStreamLogger logger, string message)
    {
        logger.Log(LogLevel.Fatal, message);
    }
}