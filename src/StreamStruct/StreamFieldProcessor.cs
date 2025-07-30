using System.Text.RegularExpressions;

namespace StreamStruct;

/// <summary>
/// Processes structured binary data from streams using field definition syntax.
/// Supports reading from and writing to streams with type-safe field parsing.
/// </summary>
public class StreamFieldProcessor
{
    private static readonly Regex FieldPattern = new(@"\[([^:\]]+):([^:\]]+)(?::([^:\]]+))?\]", RegexOptions.Compiled);
    private readonly Stream _stream;

    /// <summary>
    /// Gets or sets the logger instance for logging stream processing operations.
    /// When null, no logging will occur.
    /// </summary>
    public IStreamLogger? Logger { get; set; }

    /// <summary>
    /// Initializes a new instance of the StreamFieldProcessor class with the specified stream.
    /// </summary>
    /// <param name="stream">The stream to read from and write to.</param>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    public StreamFieldProcessor(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <summary>
    /// Asynchronously reads structured data from the stream using the specified field definition.
    /// </summary>
    /// <param name="streamDefinition">The field definition string in format [fieldName:type] or [fieldName:type:count].</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A ParseResult containing the parsed data or error information.</returns>
    public async Task<ParseResult> ReadAsync(string streamDefinition, CancellationToken cancellationToken = default)
    {
        Logger?.LogInfo($"Starting read operation with definition: {streamDefinition}");
        
        var (parseError, fields) = ParseStreamDefinition(streamDefinition);
        if (parseError != ParseError.Success)
        {
            Logger?.LogError($"[parseError:{parseError}] Failed to parse stream definition");
            return ParseResult.CreateFailure(parseError);
        }

        try
        {
            var results = new object?[fields!.Count];
            var parsedValues = new Dictionary<string, object>();
            var fieldIndexes = new Dictionary<string, int>();
            var readValues = new List<string>();

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                fieldIndexes[field.Name] = i;
                
                if (field.IsVariableLength)
                {
                    if (!parsedValues.TryGetValue(field.TypeOrLength, out var lengthValue))
                    {
                        Logger?.LogError($"[{field.Name}:variable] Missing variable reference '{field.TypeOrLength}'");
                        return ParseResult.CreateFailure(ParseError.MissingVariableReference);
                    }

                    var length = Convert.ToInt32(lengthValue);
                    Logger?.LogDebug($"[{field.Name}:variable] Reading {length} bytes");
                    var buffer = new byte[length];
                    await _stream.ReadExactlyAsync(buffer, cancellationToken);
                    results[i] = buffer;
                    parsedValues[field.Name] = buffer;
                    Logger?.LogDebug($"[{field.Name}:variable] Successfully read {buffer.Length} bytes");
                    readValues.Add($"[{buffer.Length}:variable]");
                }
                else if (field.IsFixedArray)
                {
                    Logger?.LogDebug($"[{field.Name}:{field.BaseType}:{field.FixedCount}] Reading fixed array");
                    var (readError, arrayValue) = await ReadFixedArrayAsync(_stream, field.BaseType, field.FixedCount!.Value, cancellationToken);
                    if (readError != ParseError.Success)
                    {
                        Logger?.LogError($"[{field.Name}:{field.BaseType}:{field.FixedCount}] Read error: {readError}");
                        return ParseResult.CreateFailure(readError);
                    }
                    results[i] = arrayValue;
                    parsedValues[field.Name] = arrayValue!;
                    Logger?.LogDebug($"[{field.Name}:{field.BaseType}:{field.FixedCount}] Successfully read array");
                    readValues.Add($"[{string.Join(",", GetArrayValues(arrayValue))}:{field.BaseType}]");
                }
                else
                {
                    Logger?.LogDebug($"[{field.Name}:{field.TypeOrLength}] Reading fixed type");
                    var (readError, value) = await ReadFixedTypeAsync(_stream, field.TypeOrLength, cancellationToken);
                    if (readError != ParseError.Success)
                    {
                        Logger?.LogError($"[{field.Name}:{field.TypeOrLength}] Read error: {readError}");
                        return ParseResult.CreateFailure(readError);
                    }
                    results[i] = value;
                    parsedValues[field.Name] = value!;
                    Logger?.LogDebug($"[{field.Name}:{field.TypeOrLength}] Successfully read value: {value}");
                    readValues.Add($"[{value}:{field.TypeOrLength}]");
                }
            }

            Logger?.LogInfo($"Read values: {string.Join("", readValues)}");
            Logger?.LogInfo($"Successfully completed read operation with {results.Length} fields");
            return ParseResult.CreateSuccess(results, fieldIndexes);
        }
        catch (OperationCanceledException)
        {
            Logger?.LogWarning("Read operation was cancelled");
            return ParseResult.CreateFailure(ParseError.OperationCancelled);
        }
        catch (Exception ex)
        {
            Logger?.LogError($"Stream read error: {ex.Message}");
            return ParseResult.CreateFailure(ParseError.StreamReadError);
        }
    }

    private static (ParseError Error, List<StreamFieldDefinition>? Fields) ParseStreamDefinition(string definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
        {
            return (ParseError.EmptyDefinition, null);
        }

        var fields = new List<StreamFieldDefinition>();
        var fieldNames = new HashSet<string>();
        var matches = FieldPattern.Matches(definition);

        // Special check for brackets with no matches - could be field-specific issues
        if (definition.Contains('[') && matches.Count == 0)
        {
            // Check for specific field issues before general bracket mismatch
            if (definition.Contains("[:"))
            {
                return (ParseError.EmptyFieldName, null);
            }
            if (definition.Contains(":]"))
            {
                return (ParseError.EmptyTypeOrLength, null);
            }
            
            // Check for proper bracket pairing (equal [ and ])
            var openBrackets = definition.Count(c => c == '[');
            var closeBrackets = definition.Count(c => c == ']');
            
            if (openBrackets != closeBrackets)
            {
                return (ParseError.MismatchedBrackets, null);
            }
            
            // General case - invalid field format with proper brackets
            return (ParseError.NoValidFieldPatterns, null);
        }

        // Check for bracket count mismatch (e.g., missing closing bracket)
        var bracketCount = definition.Count(c => c == '[') + definition.Count(c => c == ']');
        var matchedBrackets = matches.Count * 2;
        
        if (bracketCount != matchedBrackets && bracketCount > 0)
        {
            return (ParseError.MismatchedBrackets, null);
        }

        // Process matched fields
        foreach (Match match in matches)
        {
            var fieldName = match.Groups[1].Value.Trim();
            var typeOrLength = match.Groups[2].Value.Trim();
            var countValue = match.Groups[3].Value.Trim();

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return (ParseError.EmptyFieldName, null);
            }

            if (StreamFieldDefinition.IsReservedTypeName(fieldName))
            {
                return (ParseError.ReservedFieldName, null);
            }

            if (!fieldNames.Add(fieldName))
            {
                return (ParseError.DuplicateFieldName, null);
            }

            if (string.IsNullOrWhiteSpace(typeOrLength))
            {
                return (ParseError.EmptyTypeOrLength, null);
            }

            int? fixedCount = null;
            if (!string.IsNullOrWhiteSpace(countValue))
            {
                if (!int.TryParse(countValue, out var count) || count <= 0)
                {
                    return (ParseError.InvalidFieldFormat, null);
                }
                fixedCount = count;
                
                // Validate that the type is a fixed type when count is specified
                if (!IsFixedType(typeOrLength))
                {
                    return (ParseError.UnsupportedType, null);
                }
            }

            fields.Add(new StreamFieldDefinition
            {
                Name = fieldName,
                TypeOrLength = typeOrLength,
                FixedCount = fixedCount
            });
        }

        return (ParseError.Success, fields);
    }

    private static bool IsFixedType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "byte" or "sbyte" => true,
            "short" or "ushort" => true,
            "int" or "uint" => true,
            "long" or "ulong" => true,
            "float" => true,
            "double" => true,
            "char" => true,
            "bool" => true,
            _ => false
        };
    }

    /// <summary>
    /// Asynchronously reads structured data from the stream and verifies each field matches the expected values.
    /// </summary>
    /// <param name="streamDefinition">The field definition string in format [fieldName:type] or [fieldName:type:count].</param>
    /// <param name="expectedValues">The array of expected values to verify against, matching the field definitions in order.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A tuple containing a success indicator and a list of validation errors.</returns>
    public async Task<(bool Success, List<ValidationError> ValidationErrors)> VerifyAsync(string streamDefinition, object?[] expectedValues, CancellationToken cancellationToken = default)
    {
        Logger?.LogInfo($"Starting read verification operation with definition: {streamDefinition}");
        var validationErrors = new List<ValidationError>();
        
        var (parseError, fields) = ParseStreamDefinition(streamDefinition);
        if (parseError != ParseError.Success || fields == null)
        {
            Logger?.LogError($"[parseError:{parseError}] Failed to parse stream definition");
            validationErrors.Add(new ValidationError(
                0, 
                streamDefinition, 
                "definition", 
                parseError.ToString(), 
                "Success", 
                $"Failed to parse stream definition: {parseError}"
            ));
            return (false, validationErrors);
        }

        if (expectedValues == null)
        {
            Logger?.LogError("Expected values array is null");
            validationErrors.Add(new ValidationError(
                0, 
                streamDefinition, 
                "expectedValues", 
                null, 
                "non-null array", 
                "Expected values array is null"
            ));
            return (false, validationErrors);
        }
        
        if (fields.Count != expectedValues.Length)
        {
            Logger?.LogError($"Field count ({fields.Count}) does not match expected values length ({expectedValues.Length})");
            validationErrors.Add(new ValidationError(
                0, 
                streamDefinition, 
                "fieldCount", 
                expectedValues.Length, 
                fields.Count, 
                $"Field count ({fields.Count}) does not match expected values length ({expectedValues.Length})"
            ));
            return (false, validationErrors);
        }

        // Validate variable length references exist and type validation
        var availableFields = new HashSet<string>();
        var parsedValues = new Dictionary<string, object>();
        
        for (int i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var expectedValue = expectedValues[i];
            var fieldDef = field.IsFixedArray ? $"[{field.Name}:{field.BaseType}:{field.FixedCount}]" : $"[{field.Name}:{field.TypeOrLength}]";
            
            if (field.IsVariableLength)
            {
                if (!availableFields.Contains(field.TypeOrLength))
                {
                    Logger?.LogError($"[{field.Name}:variable] Missing variable reference '{field.TypeOrLength}'");
                    validationErrors.Add(new ValidationError(
                        0, 
                        fieldDef, 
                        "variable", 
                        null, 
                        $"reference to '{field.TypeOrLength}'", 
                        $"Missing variable reference '{field.TypeOrLength}' for field '{field.Name}'"
                    ));
                    return (false, validationErrors);
                }
                
                if (expectedValue is not byte[])
                {
                    Logger?.LogError($"[{field.Name}:variable] Expected byte array but got {expectedValue?.GetType().Name ?? "null"}");
                    validationErrors.Add(new ValidationError(
                        0, 
                        fieldDef, 
                        "variable", 
                        expectedValue?.GetType().Name ?? "null", 
                        "byte[]", 
                        $"Expected byte array for variable field '{field.Name}' but got {expectedValue?.GetType().Name ?? "null"}"
                    ));
                    return (false, validationErrors);
                }
            }
            else if (field.IsFixedArray)
            {
                if (!ValidateArrayType(expectedValue, field.BaseType, field.FixedCount!.Value))
                {
                    Logger?.LogError($"[{field.Name}:{field.BaseType}:{field.FixedCount}] Type validation failed for expected value");
                    validationErrors.Add(new ValidationError(
                        0, 
                        fieldDef, 
                        field.BaseType, 
                        expectedValue?.GetType().Name ?? "null", 
                        $"{field.BaseType}[{field.FixedCount}]", 
                        $"Type validation failed for expected value in field '{field.Name}'. Expected {field.BaseType}[{field.FixedCount}] but got {expectedValue?.GetType().Name ?? "null"}"
                    ));
                    return (false, validationErrors);
                }
            }
            else
            {
                if (!ValidateFieldType(expectedValue, field.TypeOrLength))
                {
                    Logger?.LogError($"[{field.Name}:{field.TypeOrLength}] Type validation failed for expected value");
                    validationErrors.Add(new ValidationError(
                        0, 
                        fieldDef, 
                        field.TypeOrLength, 
                        expectedValue?.GetType().Name ?? "null", 
                        field.TypeOrLength, 
                        $"Type validation failed for expected value in field '{field.Name}'. Expected {field.TypeOrLength} but got {expectedValue?.GetType().Name ?? "null"}"
                    ));
                    return (false, validationErrors);
                }
            }
            
            availableFields.Add(field.Name);
        }

        try
        {
            var verificationResults = new List<string>();
            long currentOffset = _stream.Position;
            
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var expectedValue = expectedValues[i];
                var fieldDef = field.IsFixedArray ? $"[{field.Name}:{field.BaseType}:{field.FixedCount}]" : $"[{field.Name}:{field.TypeOrLength}]";

                if (field.IsVariableLength)
                {
                    var expectedBytes = (byte[])expectedValue!;
                    var lengthFieldValue = parsedValues[field.TypeOrLength];
                    var expectedLength = Convert.ToInt32(lengthFieldValue);
                    
                    if (expectedBytes.Length != expectedLength)
                    {
                        Logger?.LogError($"[{field.Name}:variable] Expected length {expectedLength} but expected value has length {expectedBytes.Length}");
                        validationErrors.Add(new ValidationError(
                            currentOffset, 
                            fieldDef, 
                            "variable", 
                            expectedBytes.Length, 
                            expectedLength, 
                            $"Expected length {expectedLength} for variable field '{field.Name}' but expected value has length {expectedBytes.Length}"
                        ));
                        // Skip processing this field but continue with the rest
                        currentOffset += expectedLength;
                        continue;
                    }
                    
                    Logger?.LogDebug($"[{field.Name}:variable] Reading and verifying {expectedLength} bytes");
                    var buffer = new byte[expectedLength];
                    await _stream.ReadExactlyAsync(buffer, cancellationToken);
                    
                    if (!buffer.SequenceEqual(expectedBytes))
                    {
                        Logger?.LogError($"[{field.Name}:variable] Verification failed - read bytes do not match expected");
                        validationErrors.Add(new ValidationError(
                            currentOffset, 
                            fieldDef, 
                            "variable", 
                            Convert.ToHexString(buffer), 
                            Convert.ToHexString(expectedBytes), 
                            $"Variable field '{field.Name}' verification failed - read bytes do not match expected"
                        ));
                    }
                    
                    parsedValues[field.Name] = buffer;
                    currentOffset += expectedLength;
                    Logger?.LogDebug($"[{field.Name}:variable] Successfully verified {buffer.Length} bytes");
                    verificationResults.Add($"[{buffer.Length}:variable:verified]");
                }
                else if (field.IsFixedArray)
                {
                    Logger?.LogDebug($"[{field.Name}:{field.BaseType}:{field.FixedCount}] Reading and verifying fixed array");
                    var (readError, arrayValue) = await ReadFixedArrayAsync(_stream, field.BaseType, field.FixedCount!.Value, cancellationToken);
                    if (readError != ParseError.Success)
                    {
                        Logger?.LogError($"[{field.Name}:{field.BaseType}:{field.FixedCount}] Read error: {readError}");
                        validationErrors.Add(new ValidationError(
                            currentOffset, 
                            fieldDef, 
                            field.BaseType, 
                            readError.ToString(), 
                            "Success", 
                            $"Read error for fixed array field '{field.Name}': {readError}"
                        ));
                        // Skip processing this field but continue with the rest
                        currentOffset += StreamFieldDefinition.GetTypeSize(field.BaseType) * field.FixedCount!.Value;
                        continue;
                    }
                    
                    if (!CompareArrayValues(arrayValue, expectedValue, field.BaseType))
                    {
                        Logger?.LogError($"[{field.Name}:{field.BaseType}:{field.FixedCount}] Verification failed - read array does not match expected");
                        validationErrors.Add(new ValidationError(
                            currentOffset, 
                            fieldDef, 
                            field.BaseType, 
                            FormatArrayValue(arrayValue), 
                            FormatArrayValue(expectedValue), 
                            $"Fixed array field '{field.Name}' verification failed - read array does not match expected"
                        ));
                    }
                    
                    parsedValues[field.Name] = arrayValue!;
                    currentOffset += StreamFieldDefinition.GetTypeSize(field.BaseType) * field.FixedCount!.Value;
                    Logger?.LogDebug($"[{field.Name}:{field.BaseType}:{field.FixedCount}] Successfully verified array");
                    verificationResults.Add($"[{string.Join(",", GetArrayValues(arrayValue))}:{field.BaseType}:verified]");
                }
                else
                {
                    Logger?.LogDebug($"[{field.Name}:{field.TypeOrLength}] Reading and verifying fixed type");
                    var (readError, value) = await ReadFixedTypeAsync(_stream, field.TypeOrLength, cancellationToken);
                    if (readError != ParseError.Success)
                    {
                        Logger?.LogError($"[{field.Name}:{field.TypeOrLength}] Read error: {readError}");
                        validationErrors.Add(new ValidationError(
                            currentOffset, 
                            fieldDef, 
                            field.TypeOrLength, 
                            readError.ToString(), 
                            "Success", 
                            $"Read error for field '{field.Name}': {readError}"
                        ));
                        // Skip processing this field but continue with the rest
                        currentOffset += StreamFieldDefinition.GetTypeSize(field.TypeOrLength);
                        continue;
                    }
                    
                    var castedExpected = CastToFieldType(expectedValue, field.TypeOrLength);
                    if (!Equals(value, castedExpected))
                    {
                        Logger?.LogError($"[{field.Name}:{field.TypeOrLength}] Verification failed - read value '{value}' does not match expected '{castedExpected}'");
                        validationErrors.Add(new ValidationError(
                            currentOffset, 
                            fieldDef, 
                            field.TypeOrLength, 
                            value, 
                            castedExpected, 
                            $"Field '{field.Name}' verification failed - read value '{value}' does not match expected '{castedExpected}'"
                        ));
                    }
                    
                    parsedValues[field.Name] = value!;
                    currentOffset += StreamFieldDefinition.GetTypeSize(field.TypeOrLength);
                    Logger?.LogDebug($"[{field.Name}:{field.TypeOrLength}] Successfully verified value: {value}");
                    verificationResults.Add($"[{value}:{field.TypeOrLength}:verified]");
                }
            }

            Logger?.LogInfo($"Verified values: {string.Join("", verificationResults)}");
            var hasErrors = validationErrors.Count > 0;
            Logger?.LogInfo($"{(hasErrors ? "Completed" : "Successfully")} read verification operation with {fields.Count} fields{(hasErrors ? $" ({validationErrors.Count} errors)" : "")}");
            return (!hasErrors, validationErrors);
        }
        catch (OperationCanceledException)
        {
            Logger?.LogWarning("Read verification operation was cancelled");
            validationErrors.Add(new ValidationError(
                _stream.Position, 
                streamDefinition, 
                "operation", 
                "cancelled", 
                "completed", 
                "Read verification operation was cancelled"
            ));
            return (false, validationErrors);
        }
        catch (Exception ex)
        {
            Logger?.LogError($"Stream read verification error: {ex.Message}");
            validationErrors.Add(new ValidationError(
                _stream.Position, 
                streamDefinition, 
                "stream", 
                ex.Message, 
                "successful read", 
                $"Stream read verification error: {ex.Message}"
            ));
            return (false, validationErrors);
        }
    }

    /// <summary>
    /// Asynchronously writes structured data to the stream using the specified field definition.
    /// </summary>
    /// <param name="streamDefinition">The field definition string in format [fieldName:type] or [fieldName:type:count].</param>
    /// <param name="data">The array of data objects to write, matching the field definitions in order.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>true if the data was successfully written; otherwise, false.</returns>
    public async Task<bool> WriteAsync(string streamDefinition, object?[] data, CancellationToken cancellationToken = default)
    {
        Logger?.LogInfo($"Starting write operation with definition: {streamDefinition}");
        
        var (parseError, fields) = ParseStreamDefinition(streamDefinition);
        if (parseError != ParseError.Success || fields == null)
        {
            Logger?.LogError($"[parseError:{parseError}] Failed to parse stream definition");
            return false;
        }

        if (data == null)
        {
            Logger?.LogError("Data array is null");
            return false;
        }
        
        if (fields.Count != data.Length)
        {
            Logger?.LogError($"Field count ({fields.Count}) does not match data length ({data.Length})");
            return false;
        }

        // Validate variable length references exist
        var availableFields = new HashSet<string>();
        for (int i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (field.IsVariableLength)
            {
                if (!availableFields.Contains(field.TypeOrLength))
                {
                    return false;
                }
            }
            availableFields.Add(field.Name);
        }

        try
        {
            var writtenValues = new List<string>();
            
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var value = data[i];

                if (field.IsVariableLength)
                {
                    if (value is not byte[] byteArray)
                    {
                        Logger?.LogError($"[{field.Name}:variable] Expected byte array but got {value?.GetType().Name ?? "null"}");
                        return false;
                    }
                    Logger?.LogDebug($"[{field.Name}:variable] Writing {byteArray.Length} bytes");
                    await _stream.WriteAsync(byteArray, cancellationToken);
                    Logger?.LogDebug($"[{field.Name}:variable] Successfully wrote {byteArray.Length} bytes");
                    writtenValues.Add($"[{byteArray.Length}:variable]");
                }
                else if (field.IsFixedArray)
                {
                    Logger?.LogDebug($"[{field.Name}:{field.BaseType}:{field.FixedCount}] Converting and writing fixed array");
                    var (convertError, bytes) = ConvertArrayToBytes(value, field.BaseType, field.FixedCount!.Value);
                    if (convertError != ParseError.Success || bytes == null)
                    {
                        Logger?.LogError($"[{field.Name}:{field.BaseType}:{field.FixedCount}] Conversion error: {convertError}");
                        return false;
                    }
                    await _stream.WriteAsync(bytes, cancellationToken);
                    Logger?.LogDebug($"[{field.Name}:{field.BaseType}:{field.FixedCount}] Successfully wrote {bytes.Length} bytes");
                    writtenValues.Add($"[{string.Join(",", GetArrayValues(value))}:{field.BaseType}]");
                }
                else
                {
                    Logger?.LogDebug($"[{field.Name}:{field.TypeOrLength}] Converting and writing fixed type value: {value}");
                    var castedValue = CastToFieldType(value, field.TypeOrLength);
                    var (convertError, bytes) = ConvertToBytes(castedValue, field.TypeOrLength);
                    if (convertError != ParseError.Success || bytes == null)
                    {
                        Logger?.LogError($"[{field.Name}:{field.TypeOrLength}] Conversion error: {convertError}");
                        return false;
                    }
                    await _stream.WriteAsync(bytes, cancellationToken);
                    Logger?.LogDebug($"[{field.Name}:{field.TypeOrLength}] Successfully wrote {bytes.Length} bytes");
                    writtenValues.Add($"[{castedValue}:{field.TypeOrLength}]");
                }
            }

            await _stream.FlushAsync(cancellationToken);
            Logger?.LogInfo($"Written values: {string.Join("", writtenValues)}");
            Logger?.LogInfo($"Successfully completed write operation with {fields.Count} fields");
            return true;
        }
        catch (OperationCanceledException)
        {
            Logger?.LogWarning("Write operation was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Logger?.LogError($"Stream write error: {ex.Message}");
            return false;
        }
    }

    private static object? CastToFieldType(object? value, string type)
    {
        if (value == null) return null;

        try
        {
            return type.ToLowerInvariant() switch
            {
                "byte" => Convert.ToByte(value),
                "sbyte" => Convert.ToSByte(value),
                "short" => Convert.ToInt16(value),
                "ushort" => Convert.ToUInt16(value),
                "int" => Convert.ToInt32(value),
                "uint" => Convert.ToUInt32(value),
                "long" => Convert.ToInt64(value),
                "ulong" => Convert.ToUInt64(value),
                "float" => Convert.ToSingle(value),
                "double" => Convert.ToDouble(value),
                "char" => Convert.ToChar(value),
                "bool" => Convert.ToBoolean(value),
                _ => value
            };
        }
        catch
        {
            return value;
        }
    }

    private static (ParseError Error, byte[]? Bytes) ConvertToBytes(object? value, string type)
    {
        if (value == null)
        {
            return (ParseError.NullValue, null);
        }

        try
        {
            var bytes = type.ToLowerInvariant() switch
            {
                "byte" => [(byte)value],
                "sbyte" => [(byte)(sbyte)value],
                "short" => BitConverter.GetBytes((short)value),
                "ushort" => BitConverter.GetBytes((ushort)value),
                "int" => BitConverter.GetBytes((int)value),
                "uint" => BitConverter.GetBytes((uint)value),
                "long" => BitConverter.GetBytes((long)value),
                "ulong" => BitConverter.GetBytes((ulong)value),
                "float" => BitConverter.GetBytes((float)value),
                "double" => BitConverter.GetBytes((double)value),
                "char" => BitConverter.GetBytes((char)value),
                "bool" => BitConverter.GetBytes((bool)value),
                _ => null
            };
            
            return bytes == null ? (ParseError.UnsupportedType, null) : (ParseError.Success, bytes);
        }
        catch
        {
            return (ParseError.WrongVariableType, null);
        }
    }

    private static async Task<(ParseError Error, object? Value)> ReadFixedTypeAsync(Stream stream, string type, CancellationToken cancellationToken)
    {
        try
        {
            var typeSize = StreamFieldDefinition.GetTypeSize(type);
            if (typeSize <= 0)
            {
                return (ParseError.UnsupportedType, null);
            }
            
            var buffer = new byte[typeSize];
            await stream.ReadExactlyAsync(buffer, cancellationToken);

            object? value = type.ToLowerInvariant() switch
            {
                "byte" => buffer[0],
                "sbyte" => (sbyte)buffer[0],
                "short" => BitConverter.ToInt16(buffer),
                "ushort" => BitConverter.ToUInt16(buffer),
                "int" => BitConverter.ToInt32(buffer),
                "uint" => BitConverter.ToUInt32(buffer),
                "long" => BitConverter.ToInt64(buffer),
                "ulong" => BitConverter.ToUInt64(buffer),
                "float" => BitConverter.ToSingle(buffer),
                "double" => BitConverter.ToDouble(buffer),
                "char" => BitConverter.ToChar(buffer),
                "bool" => BitConverter.ToBoolean(buffer),
                _ => null
            };
            
            return value == null ? (ParseError.UnsupportedType, null) : (ParseError.Success, value);
        }
        catch (OperationCanceledException)
        {
            return (ParseError.OperationCancelled, null);
        }
        catch
        {
            return (ParseError.StreamReadError, null);
        }
    }

    private static async Task<(ParseError Error, object? Value)> ReadFixedArrayAsync(Stream stream, string type, int count, CancellationToken cancellationToken)
    {
        try
        {
            var typeSize = StreamFieldDefinition.GetTypeSize(type);
            if (typeSize <= 0)
            {
                return (ParseError.UnsupportedType, null);
            }
            
            var totalSize = typeSize * count;
            var buffer = new byte[totalSize];
            await stream.ReadExactlyAsync(buffer, cancellationToken);

            // For byte arrays, return the raw bytes
            if (type.ToLowerInvariant() == "byte")
            {
                return (ParseError.Success, buffer);
            }

            // For other types, convert to appropriate array
            object? result = type.ToLowerInvariant() switch
            {
                "sbyte" => ConvertToSByteArray(buffer),
                "short" => ConvertToShortArray(buffer),
                "ushort" => ConvertToUShortArray(buffer),
                "int" => ConvertToIntArray(buffer),
                "uint" => ConvertToUIntArray(buffer),
                "long" => ConvertToLongArray(buffer),
                "ulong" => ConvertToULongArray(buffer),
                "float" => ConvertToFloatArray(buffer),
                "double" => ConvertToDoubleArray(buffer),
                "char" => ConvertToCharArray(buffer),
                "bool" => ConvertToBoolArray(buffer),
                _ => null
            };
            
            return result == null ? (ParseError.UnsupportedType, null) : (ParseError.Success, result);
        }
        catch (OperationCanceledException)
        {
            return (ParseError.OperationCancelled, null);
        }
        catch
        {
            return (ParseError.StreamReadError, null);
        }
    }

    private static (ParseError Error, byte[]? Bytes) ConvertArrayToBytes(object? value, string type, int expectedCount)
    {
        if (value == null)
        {
            return (ParseError.NullValue, null);
        }

        try
        {
            var bytes = type.ToLowerInvariant() switch
            {
                "byte" when value is byte[] byteArray && byteArray.Length == expectedCount => byteArray,
                "sbyte" when value is sbyte[] sbyteArray && sbyteArray.Length == expectedCount => 
                    sbyteArray.Select(b => (byte)b).ToArray(),
                "short" when value is short[] shortArray && shortArray.Length == expectedCount => 
                    shortArray.SelectMany(BitConverter.GetBytes).ToArray(),
                "ushort" when value is ushort[] ushortArray && ushortArray.Length == expectedCount => 
                    ushortArray.SelectMany(BitConverter.GetBytes).ToArray(),
                "int" when value is int[] intArray && intArray.Length == expectedCount => 
                    intArray.SelectMany(BitConverter.GetBytes).ToArray(),
                "uint" when value is uint[] uintArray && uintArray.Length == expectedCount => 
                    uintArray.SelectMany(BitConverter.GetBytes).ToArray(),
                "long" when value is long[] longArray && longArray.Length == expectedCount => 
                    longArray.SelectMany(BitConverter.GetBytes).ToArray(),
                "ulong" when value is ulong[] ulongArray && ulongArray.Length == expectedCount => 
                    ulongArray.SelectMany(BitConverter.GetBytes).ToArray(),
                "float" when value is float[] floatArray && floatArray.Length == expectedCount => 
                    floatArray.SelectMany(BitConverter.GetBytes).ToArray(),
                "double" when value is double[] doubleArray && doubleArray.Length == expectedCount => 
                    doubleArray.SelectMany(BitConverter.GetBytes).ToArray(),
                "char" when value is char[] charArray && charArray.Length == expectedCount => 
                    charArray.SelectMany(BitConverter.GetBytes).ToArray(),
                "bool" when value is bool[] boolArray && boolArray.Length == expectedCount => 
                    boolArray.SelectMany(BitConverter.GetBytes).ToArray(),
                _ => null
            };
            
            return bytes == null ? (ParseError.WrongVariableType, null) : (ParseError.Success, bytes);
        }
        catch
        {
            return (ParseError.WrongVariableType, null);
        }
    }

    private static sbyte[] ConvertToSByteArray(byte[] buffer)
    {
        return buffer.Select(b => (sbyte)b).ToArray();
    }

    private static short[] ConvertToShortArray(byte[] buffer)
    {
        var result = new short[buffer.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToInt16(buffer, i * 2);
        }
        return result;
    }

    private static ushort[] ConvertToUShortArray(byte[] buffer)
    {
        var result = new ushort[buffer.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToUInt16(buffer, i * 2);
        }
        return result;
    }

    private static int[] ConvertToIntArray(byte[] buffer)
    {
        var result = new int[buffer.Length / 4];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToInt32(buffer, i * 4);
        }
        return result;
    }

    private static uint[] ConvertToUIntArray(byte[] buffer)
    {
        var result = new uint[buffer.Length / 4];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToUInt32(buffer, i * 4);
        }
        return result;
    }

    private static long[] ConvertToLongArray(byte[] buffer)
    {
        var result = new long[buffer.Length / 8];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToInt64(buffer, i * 8);
        }
        return result;
    }

    private static ulong[] ConvertToULongArray(byte[] buffer)
    {
        var result = new ulong[buffer.Length / 8];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToUInt64(buffer, i * 8);
        }
        return result;
    }

    private static float[] ConvertToFloatArray(byte[] buffer)
    {
        var result = new float[buffer.Length / 4];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToSingle(buffer, i * 4);
        }
        return result;
    }

    private static double[] ConvertToDoubleArray(byte[] buffer)
    {
        var result = new double[buffer.Length / 8];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToDouble(buffer, i * 8);
        }
        return result;
    }

    private static char[] ConvertToCharArray(byte[] buffer)
    {
        var result = new char[buffer.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToChar(buffer, i * 2);
        }
        return result;
    }

    private static bool[] ConvertToBoolArray(byte[] buffer)
    {
        return buffer.Select(b => BitConverter.ToBoolean(new[] { b }, 0)).ToArray();
    }

    private static IEnumerable<object> GetArrayValues(object? value)
    {
        return value switch
        {
            byte[] byteArray => byteArray.Cast<object>(),
            sbyte[] sbyteArray => sbyteArray.Cast<object>(),
            short[] shortArray => shortArray.Cast<object>(),
            ushort[] ushortArray => ushortArray.Cast<object>(),
            int[] intArray => intArray.Cast<object>(),
            uint[] uintArray => uintArray.Cast<object>(),
            long[] longArray => longArray.Cast<object>(),
            ulong[] ulongArray => ulongArray.Cast<object>(),
            float[] floatArray => floatArray.Cast<object>(),
            double[] doubleArray => doubleArray.Cast<object>(),
            char[] charArray => charArray.Cast<object>(),
            bool[] boolArray => boolArray.Cast<object>(),
            _ => []
        };
    }

    private static bool ValidateFieldType(object? value, string type)
    {
        if (value == null) return false;
        
        try
        {
            var castedValue = CastToFieldType(value, type);
            return castedValue != null && type.ToLowerInvariant() switch
            {
                "byte" => value is byte or sbyte or short or ushort or int or uint or long or ulong,
                "sbyte" => value is byte or sbyte or short or ushort or int or uint or long or ulong,
                "short" => value is byte or sbyte or short or ushort or int or uint or long or ulong,
                "ushort" => value is byte or sbyte or short or ushort or int or uint or long or ulong,
                "int" => value is byte or sbyte or short or ushort or int or uint or long or ulong,
                "uint" => value is byte or sbyte or short or ushort or int or uint or long or ulong,
                "long" => value is byte or sbyte or short or ushort or int or uint or long or ulong,
                "ulong" => value is byte or sbyte or short or ushort or int or uint or long or ulong,
                "float" => value is float or double or byte or sbyte or short or ushort or int or uint or long or ulong,
                "double" => value is float or double or byte or sbyte or short or ushort or int or uint or long or ulong,
                "char" => value is char,
                "bool" => value is bool,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static bool ValidateArrayType(object? value, string type, int expectedCount)
    {
        if (value == null) return false;
        
        return type.ToLowerInvariant() switch
        {
            "byte" => value is byte[] byteArray && byteArray.Length == expectedCount,
            "sbyte" => value is sbyte[] sbyteArray && sbyteArray.Length == expectedCount,
            "short" => value is short[] shortArray && shortArray.Length == expectedCount,
            "ushort" => value is ushort[] ushortArray && ushortArray.Length == expectedCount,
            "int" => value is int[] intArray && intArray.Length == expectedCount,
            "uint" => value is uint[] uintArray && uintArray.Length == expectedCount,
            "long" => value is long[] longArray && longArray.Length == expectedCount,
            "ulong" => value is ulong[] ulongArray && ulongArray.Length == expectedCount,
            "float" => value is float[] floatArray && floatArray.Length == expectedCount,
            "double" => value is double[] doubleArray && doubleArray.Length == expectedCount,
            "char" => value is char[] charArray && charArray.Length == expectedCount,
            "bool" => value is bool[] boolArray && boolArray.Length == expectedCount,
            _ => false
        };
    }

    private static bool CompareArrayValues(object? readValue, object? expectedValue, string type)
    {
        if (readValue == null || expectedValue == null) return false;
        
        return type.ToLowerInvariant() switch
        {
            "byte" when readValue is byte[] readBytes && expectedValue is byte[] expectedBytes => 
                readBytes.SequenceEqual(expectedBytes),
            "sbyte" when readValue is sbyte[] readSBytes && expectedValue is sbyte[] expectedSBytes => 
                readSBytes.SequenceEqual(expectedSBytes),
            "short" when readValue is short[] readShorts && expectedValue is short[] expectedShorts => 
                readShorts.SequenceEqual(expectedShorts),
            "ushort" when readValue is ushort[] readUShorts && expectedValue is ushort[] expectedUShorts => 
                readUShorts.SequenceEqual(expectedUShorts),
            "int" when readValue is int[] readInts && expectedValue is int[] expectedInts => 
                readInts.SequenceEqual(expectedInts),
            "uint" when readValue is uint[] readUInts && expectedValue is uint[] expectedUInts => 
                readUInts.SequenceEqual(expectedUInts),
            "long" when readValue is long[] readLongs && expectedValue is long[] expectedLongs => 
                readLongs.SequenceEqual(expectedLongs),
            "ulong" when readValue is ulong[] readULongs && expectedValue is ulong[] expectedULongs => 
                readULongs.SequenceEqual(expectedULongs),
            "float" when readValue is float[] readFloats && expectedValue is float[] expectedFloats => 
                readFloats.SequenceEqual(expectedFloats),
            "double" when readValue is double[] readDoubles && expectedValue is double[] expectedDoubles => 
                readDoubles.SequenceEqual(expectedDoubles),
            "char" when readValue is char[] readChars && expectedValue is char[] expectedChars => 
                readChars.SequenceEqual(expectedChars),
            "bool" when readValue is bool[] readBools && expectedValue is bool[] expectedBools => 
                readBools.SequenceEqual(expectedBools),
            _ => false
        };
    }

    private static string FormatArrayValue(object? value)
    {
        if (value == null) return "null";
        
        return value switch
        {
            byte[] byteArray => $"[{string.Join(",", byteArray)}]",
            sbyte[] sbyteArray => $"[{string.Join(",", sbyteArray)}]",
            short[] shortArray => $"[{string.Join(",", shortArray)}]",
            ushort[] ushortArray => $"[{string.Join(",", ushortArray)}]",
            int[] intArray => $"[{string.Join(",", intArray)}]",
            uint[] uintArray => $"[{string.Join(",", uintArray)}]",
            long[] longArray => $"[{string.Join(",", longArray)}]",
            ulong[] ulongArray => $"[{string.Join(",", ulongArray)}]",
            float[] floatArray => $"[{string.Join(",", floatArray)}]",
            double[] doubleArray => $"[{string.Join(",", doubleArray)}]",
            char[] charArray => $"[{string.Join(",", charArray)}]",
            bool[] boolArray => $"[{string.Join(",", boolArray)}]",
            _ => value.ToString() ?? "null"
        };
    }
}