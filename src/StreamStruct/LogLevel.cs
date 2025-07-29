namespace StreamStruct;

/// <summary>
/// Defines the severity levels for logging messages.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Detailed information, typically used for diagnosing problems.
    /// </summary>
    Trace = 0,

    /// <summary>
    /// Information useful for debugging during development.
    /// </summary>
    Debug = 1,

    /// <summary>
    /// General informational messages.
    /// </summary>
    Info = 2,

    /// <summary>
    /// Potentially harmful situations.
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Error events that might still allow the application to continue running.
    /// </summary>
    Error = 4,

    /// <summary>
    /// Very severe error events that will presumably lead the application to abort.
    /// </summary>
    Fatal = 5
}