namespace StreamStruct;

public partial class StreamFieldProcessor
{
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
                    var (readError, arrayValue) = await ReadFixedArrayAsync(_stream, field.BaseType,
                        field.FixedCount!.Value, cancellationToken);
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
}