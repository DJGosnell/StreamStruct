using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace StreamStruct;

/// <summary>
/// Represents error codes that can occur during stream parsing operations.
/// </summary>
public enum ParseError
{
    Unset = 0,
    EmptyDefinition,
    InvalidFieldFormat,
    MismatchedBrackets,
    EmptyFieldName,
    EmptyTypeOrLength,
    NoValidFieldPatterns,
    MissingVariableReference,
    UnsupportedType,
    StreamReadError,
    StreamWriteError,
    OperationCancelled,
    InvalidDataLength,
    WrongVariableType,
    ReservedFieldName,
    DuplicateFieldName,
    NullValue,
    StreamClosed,
    UnknownError,
    Success
}

/// <summary>
/// Represents the result of a stream parsing operation, containing parsed data and error information.
/// </summary>
public class ParseResult
{
    /// <summary>
    /// Gets a value indicating whether the parsing operation was successful.
    /// </summary>
    public bool Success { get; init; }
    /// <summary>
    /// Gets the array of parsed data objects. Empty if the operation failed.
    /// </summary>
    public object?[] Data { get; init; } = Array.Empty<object?>();
    /// <summary>
    /// Gets the error message if the parsing operation failed; otherwise, null.
    /// </summary>
    public string? ErrorMessage { get; init; }
    /// <summary>
    /// Gets the error code indicating the type of error that occurred during parsing.
    /// </summary>
    public ParseError ErrorCode { get; init; } = ParseError.Unset;
    /// <summary>
    /// Gets a dictionary mapping field names to their array indexes in the Data array.
    /// </summary>
    public IReadOnlyDictionary<string, int> FieldIndexes { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Creates a successful parse result with the specified data.
    /// </summary>
    /// <param name="data">The parsed data array.</param>
    /// <returns>A successful ParseResult instance.</returns>
    public static ParseResult CreateSuccess(object?[] data)
    {
        return new ParseResult { Success = true, Data = data, ErrorCode = ParseError.Success };
    }

    /// <summary>
    /// Creates a successful parse result with the specified data and field indexes.
    /// </summary>
    /// <param name="data">The parsed data array.</param>
    /// <param name="fieldIndexes">Dictionary mapping field names to array indexes.</param>
    /// <returns>A successful ParseResult instance.</returns>
    public static ParseResult CreateSuccess(object?[] data, IReadOnlyDictionary<string, int> fieldIndexes)
    {
        return new ParseResult { Success = true, Data = data, ErrorCode = ParseError.Success, FieldIndexes = fieldIndexes };
    }

    /// <summary>
    /// Creates a failed parse result with the specified error message and code.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="errorCode">The error code indicating the type of failure.</param>
    /// <returns>A failed ParseResult instance.</returns>
    public static ParseResult CreateFailure(string errorMessage, ParseError errorCode = ParseError.UnknownError)
    {
        return new ParseResult { Success = false, ErrorMessage = errorMessage, ErrorCode = errorCode };
    }

    /// <summary>
    /// Creates a failed parse result with the specified error code and optional custom message.
    /// </summary>
    /// <param name="errorCode">The error code indicating the type of failure.</param>
    /// <param name="customMessage">Optional custom error message; if null, uses default message for the error code.</param>
    /// <returns>A failed ParseResult instance.</returns>
    public static ParseResult CreateFailure(ParseError errorCode, string? customMessage = null)
    {
        var defaultMessage = GetDefaultErrorMessage(errorCode);
        var message = customMessage ?? defaultMessage;
        return new ParseResult { Success = false, ErrorMessage = message, ErrorCode = errorCode };
    }

    private static string GetDefaultErrorMessage(ParseError errorCode)
    {
        return errorCode switch
        {
            ParseError.EmptyDefinition => "Stream definition cannot be null or empty",
            ParseError.InvalidFieldFormat => "Invalid field definition format",
            ParseError.MismatchedBrackets => "Invalid field definition format: mismatched or malformed brackets",
            ParseError.EmptyFieldName => "Field name cannot be empty",
            ParseError.EmptyTypeOrLength => "Type or length cannot be empty",
            ParseError.NoValidFieldPatterns => "Invalid field definition format: no valid field patterns found",
            ParseError.MissingVariableReference => "Variable reference not found",
            ParseError.UnsupportedType => "Unsupported type",
            ParseError.StreamReadError => "Error reading from stream",
            ParseError.StreamWriteError => "Error writing to stream",
            ParseError.OperationCancelled => "Operation was cancelled",
            ParseError.InvalidDataLength => "Data array length does not match field count",
            ParseError.WrongVariableType => "Wrong type for variable length field",
            ParseError.ReservedFieldName => "Field name cannot be a reserved type name",
            ParseError.DuplicateFieldName => "Duplicate field name found in stream definition",
            ParseError.NullValue => "Null value not allowed",
            ParseError.StreamClosed => "Stream is closed",
            ParseError.UnknownError => "An unknown error occurred",
            ParseError.Success => "Operation completed successfully",
            _ => "Unknown error"
        };
    }

    /// <summary>
    /// Attempts to read a value of the specified type from the data array at the given index.
    /// </summary>
    /// <typeparam name="T">The type to cast the value to.</typeparam>
    /// <param name="index">The zero-based index in the data array.</param>
    /// <param name="value">When this method returns, contains the value if successful; otherwise, the default value.</param>
    /// <returns>true if the value was successfully read and cast; otherwise, false.</returns>
    public bool TryRead<T>(int index, out T? value)
    {
        value = default;
        
        if (!Success || index < 0 || index >= Data.Length)
        {
            return false;
        }
        
        try
        {
            value = (T?)Data[index];
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to read a UTF-8 encoded string from a byte array at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index in the data array.</param>
    /// <param name="value">When this method returns, contains the UTF-8 decoded string if successful; otherwise, null.</param>
    /// <returns>true if the byte array was successfully decoded as UTF-8; otherwise, false.</returns>
    public bool TryReadUtf8(int index, out string? value)
    {
        value = null;
        
        if (!Success || index < 0 || index >= Data.Length)
        {
            return false;
        }
        
        try
        {
            if (Data[index] is byte[] bytes)
            {
                value = Encoding.UTF8.GetString(bytes);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to read a value of the specified type from the data array using the field name.
    /// </summary>
    /// <typeparam name="T">The type to cast the value to.</typeparam>
    /// <param name="fieldName">The name of the field to read.</param>
    /// <param name="value">When this method returns, contains the value if successful; otherwise, the default value.</param>
    /// <returns>true if the field was found and the value was successfully read and cast; otherwise, false.</returns>
    public bool TryRead<T>(string fieldName, out T? value)
    {
        value = default;
        
        if (!FieldIndexes.TryGetValue(fieldName, out var index))
        {
            return false;
        }
        
        return TryRead(index, out value);
    }

    /// <summary>
    /// Attempts to read a UTF-8 encoded string from a byte array field with the specified name.
    /// </summary>
    /// <param name="fieldName">The name of the field to read.</param>
    /// <param name="value">When this method returns, contains the UTF-8 decoded string if successful; otherwise, null.</param>
    /// <returns>true if the field was found and the byte array was successfully decoded as UTF-8; otherwise, false.</returns>
    public bool TryReadUtf8(string fieldName, out string? value)
    {
        value = null;
        
        if (!FieldIndexes.TryGetValue(fieldName, out var index))
        {
            return false;
        }
        
        return TryReadUtf8(index, out value);
    }
}