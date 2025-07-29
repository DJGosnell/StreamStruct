namespace StreamStruct;

/// <summary>
/// Interface for logging stream processing operations.
/// Messages are formatted using field definition syntax [value:type].
/// </summary>
public interface IStreamLogger
{
    /// <summary>
    /// Logs a message at the specified level.
    /// </summary>
    /// <param name="level">The severity level of the message.</param>
    /// <param name="message">The message to log.</param>
    void Log(LogLevel level, string message);
}