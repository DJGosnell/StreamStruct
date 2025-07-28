using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace StreamStruct;

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

public class ParseResult
{
    public bool Success { get; init; }
    public object?[] Data { get; init; } = Array.Empty<object?>();
    public string? ErrorMessage { get; init; }
    public ParseError ErrorCode { get; init; } = ParseError.Unset;
    public IReadOnlyDictionary<string, int> FieldIndexes { get; init; } = new Dictionary<string, int>();

    public static ParseResult CreateSuccess(object?[] data)
    {
        return new ParseResult { Success = true, Data = data, ErrorCode = ParseError.Success };
    }

    public static ParseResult CreateSuccess(object?[] data, IReadOnlyDictionary<string, int> fieldIndexes)
    {
        return new ParseResult { Success = true, Data = data, ErrorCode = ParseError.Success, FieldIndexes = fieldIndexes };
    }

    public static ParseResult CreateFailure(string errorMessage, ParseError errorCode = ParseError.UnknownError)
    {
        return new ParseResult { Success = false, ErrorMessage = errorMessage, ErrorCode = errorCode };
    }

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

    public bool TryRead<T>(string fieldName, out T? value)
    {
        value = default;
        
        if (!FieldIndexes.TryGetValue(fieldName, out var index))
        {
            return false;
        }
        
        return TryRead(index, out value);
    }

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