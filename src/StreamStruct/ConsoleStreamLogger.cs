namespace StreamStruct;

/// <summary>
/// Console implementation of IStreamLogger that outputs messages to the console
/// with timestamps and log level prefixes. Supports minimum log level filtering.
/// </summary>
public class ConsoleStreamLogger : IStreamLogger
{
    /// <summary>
    /// Gets or sets the minimum log level. Messages below this level will be filtered out.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    /// <summary>
    /// Initializes a new instance of the ConsoleStreamLogger class.
    /// </summary>
    public ConsoleStreamLogger()
    {
    }

    /// <summary>
    /// Initializes a new instance of the ConsoleStreamLogger class with the specified minimum log level.
    /// </summary>
    /// <param name="minimumLevel">The minimum log level to output.</param>
    public ConsoleStreamLogger(LogLevel minimumLevel)
    {
        MinimumLevel = minimumLevel;
    }

    /// <summary>
    /// Logs a message at the specified level to the console if it meets the minimum level threshold.
    /// </summary>
    /// <param name="level">The severity level of the message.</param>
    /// <param name="message">The message to log.</param>
    public void Log(LogLevel level, string message)
    {
        if (level < MinimumLevel)
            return;

        var levelText = level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Fatal => "FATAL",
            _ => level.ToString().ToUpper()
        };

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{levelText}] {message}");
    }
}