namespace StreamStruct;

public partial class StreamFieldProcessor
{
    /// <summary>
    /// Asynchronously reads structured data from the stream and verifies each field matches the expected values.
    /// </summary>
    /// <param name="streamDefinition">The field definition string in format [fieldName:type] or [fieldName:type:count].</param>
    /// <param name="expectedValues">The array of expected values to verify against, matching the field definitions in order.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A tuple containing a success indicator and a list of validation errors.</returns>
    public async Task<(bool Success, List<ValidationError> ValidationErrors)> VerifyAsync(string streamDefinition,
        object?[]? expectedValues,
        CancellationToken cancellationToken = default)
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
            Logger?.LogError(
                $"Field count ({fields.Count}) does not match expected values length ({expectedValues.Length})");
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
            var fieldDef = field.IsFixedArray
                ? $"[{field.Name}:{field.BaseType}:{field.FixedCount}]"
                : $"[{field.Name}:{field.TypeOrLength}]";

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
                    Logger?.LogError(
                        $"[{field.Name}:variable] Expected byte array but got {expectedValue?.GetType().Name ?? "null"}");
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
                    Logger?.LogError(
                        $"[{field.Name}:{field.BaseType}:{field.FixedCount}] Type validation failed for expected value");
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
                var fieldDef = field.IsFixedArray
                    ? $"[{field.Name}:{field.BaseType}:{field.FixedCount}]"
                    : $"[{field.Name}:{field.TypeOrLength}]";

                if (field.IsVariableLength)
                {
                    var expectedBytes = (byte[])expectedValue!;
                    var lengthFieldValue = parsedValues[field.TypeOrLength];
                    var expectedLength = Convert.ToInt32(lengthFieldValue);

                    if (expectedBytes.Length != expectedLength)
                    {
                        Logger?.LogError(
                            $"[{field.Name}:variable] Expected length {expectedLength} but expected value has length {expectedBytes.Length}");
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
                        Logger?.LogError(
                            $"[{field.Name}:variable] Verification failed - read bytes do not match expected");
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
                    Logger?.LogDebug(
                        $"[{field.Name}:{field.BaseType}:{field.FixedCount}] Reading and verifying fixed array");
                    var (readError, arrayValue) = await ReadFixedArrayAsync(_stream, field.BaseType,
                        field.FixedCount!.Value, cancellationToken);
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
                        Logger?.LogError(
                            $"[{field.Name}:{field.BaseType}:{field.FixedCount}] Verification failed - read array does not match expected");
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
                    verificationResults.Add(
                        $"[{string.Join(",", GetArrayValues(arrayValue))}:{field.BaseType}:verified]");
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
                        Logger?.LogError(
                            $"[{field.Name}:{field.TypeOrLength}] Verification failed - read value '{value}' does not match expected '{castedExpected}'");
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
            Logger?.LogInfo(
                $"{(hasErrors ? "Completed" : "Successfully")} read verification operation with {fields.Count} fields{(hasErrors ? $" ({validationErrors.Count} errors)" : "")}");
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
}