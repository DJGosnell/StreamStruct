namespace StreamStruct;

public partial class StreamFieldProcessor
{
    /// <summary>
    /// Asynchronously writes structured data to the stream using the specified field definition.
    /// </summary>
    /// <param name="streamDefinition">The field definition string in format [fieldName:type] or [fieldName:type:count].</param>
    /// <param name="data">The array of data objects to write, matching the field definitions in order.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>true if the data was successfully written; otherwise, false.</returns>
    public async Task<bool> WriteAsync(string streamDefinition,
        object?[] data,
        CancellationToken cancellationToken = default)
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
                        Logger?.LogError(
                            $"[{field.Name}:variable] Expected byte array but got {value?.GetType().Name ?? "null"}");
                        return false;
                    }

                    Logger?.LogDebug($"[{field.Name}:variable] Writing {byteArray.Length} bytes");
                    await _stream.WriteAsync(byteArray, cancellationToken);
                    Logger?.LogDebug($"[{field.Name}:variable] Successfully wrote {byteArray.Length} bytes");
                    writtenValues.Add($"[{byteArray.Length}:variable]");
                }
                else if (field.IsFixedArray)
                {
                    Logger?.LogDebug(
                        $"[{field.Name}:{field.BaseType}:{field.FixedCount}] Converting and writing fixed array");
                    var (convertError, bytes) = ConvertArrayToBytes(value, field.BaseType, field.FixedCount!.Value);
                    if (convertError != ParseError.Success || bytes == null)
                    {
                        Logger?.LogError(
                            $"[{field.Name}:{field.BaseType}:{field.FixedCount}] Conversion error: {convertError}");
                        return false;
                    }

                    await _stream.WriteAsync(bytes, cancellationToken);
                    Logger?.LogDebug(
                        $"[{field.Name}:{field.BaseType}:{field.FixedCount}] Successfully wrote {bytes.Length} bytes");
                    writtenValues.Add($"[{string.Join(",", GetArrayValues(value))}:{field.BaseType}]");
                }
                else
                {
                    Logger?.LogDebug(
                        $"[{field.Name}:{field.TypeOrLength}] Converting and writing fixed type value: {value}");
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
}