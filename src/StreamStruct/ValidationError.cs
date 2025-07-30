namespace StreamStruct;

/// <summary>
/// Represents a validation error that occurred during stream verification.
/// </summary>
/// <param name="StreamOffset">The byte offset in the stream where the validation failed.</param>
/// <param name="FieldDefinition">The field definition string that failed validation.</param>
/// <param name="FieldType">The type of the field that failed validation.</param>
/// <param name="ActualValue">The value that was actually parsed from the stream.</param>
/// <param name="ExpectedValue">The value that was expected to be parsed.</param>
/// <param name="ErrorMessage">A descriptive error message explaining the validation failure.</param>
public record ValidationError(
    long StreamOffset,
    string FieldDefinition,
    string FieldType,
    object? ActualValue,
    object? ExpectedValue,
    string ErrorMessage
);