using System.Text;
using System.Text.RegularExpressions;

namespace StreamStruct;

public class StreamFieldProcessor
{
    private static readonly Regex FieldPattern = new(@"\[([^:]+):([^\]]+)\]", RegexOptions.Compiled);
    private readonly Stream _stream;

    public StreamFieldProcessor(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public async Task<ParseResult> ReadAsync(string streamDefinition, CancellationToken cancellationToken = default)
    {
        try
        {
            var fields = ParseStreamDefinition(streamDefinition);
            var results = new object?[fields.Count];
            var parsedValues = new Dictionary<string, object>();

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                
                if (field.IsVariableLength)
                {
                    if (!parsedValues.TryGetValue(field.TypeOrLength, out var lengthValue))
                    {
                        return ParseResult.CreateFailure(ParseError.MissingVariableReference, $"Variable '{field.TypeOrLength}' not found for field '{field.Name}'");
                    }

                    var length = Convert.ToInt32(lengthValue);
                    var buffer = new byte[length];
                    await _stream.ReadExactlyAsync(buffer, cancellationToken);
                    results[i] = buffer;
                    parsedValues[field.Name] = buffer;
                }
                else
                {
                    var value = await ReadFixedTypeAsync(_stream, field.TypeOrLength, cancellationToken);
                    results[i] = value;
                    parsedValues[field.Name] = value;
                }
            }

            return ParseResult.CreateSuccess(results);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Stream definition cannot be null or empty"))
        {
            return ParseResult.CreateFailure(ParseError.EmptyDefinition, ex.Message);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("mismatched or malformed brackets"))
        {
            return ParseResult.CreateFailure(ParseError.MismatchedBrackets, ex.Message);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("no valid field patterns found"))
        {
            return ParseResult.CreateFailure(ParseError.NoValidFieldPatterns, ex.Message);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Field name cannot be empty"))
        {
            return ParseResult.CreateFailure(ParseError.EmptyFieldName, ex.Message);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Type or length cannot be empty"))
        {
            return ParseResult.CreateFailure(ParseError.EmptyTypeOrLength, ex.Message);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Unsupported type"))
        {
            return ParseResult.CreateFailure(ParseError.UnsupportedType, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return ParseResult.CreateFailure(ParseError.OperationCancelled);
        }
        catch (Exception ex)
        {
            return ParseResult.CreateFailure(ParseError.StreamReadError, $"Error parsing stream: {ex.Message}");
        }
    }

    private static List<StreamFieldDefinition> ParseStreamDefinition(string definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
        {
            throw new ArgumentException("Stream definition cannot be null or empty", nameof(definition));
        }

        var fields = new List<StreamFieldDefinition>();
        var matches = FieldPattern.Matches(definition);

        // Special check for brackets with no matches - could be field-specific issues
        if (definition.Contains('[') && matches.Count == 0)
        {
            // Check for specific field issues before general bracket mismatch
            if (definition.Contains("[:"))
            {
                throw new ArgumentException("Field name cannot be empty", nameof(definition));
            }
            if (definition.Contains(":]"))
            {
                throw new ArgumentException("Type or length cannot be empty", nameof(definition));
            }
            
            // Check for proper bracket pairing (equal [ and ])
            var openBrackets = definition.Count(c => c == '[');
            var closeBrackets = definition.Count(c => c == ']');
            
            if (openBrackets != closeBrackets)
            {
                throw new ArgumentException("Invalid field definition format: mismatched or malformed brackets", nameof(definition));
            }
            
            // General case - invalid field format with proper brackets
            throw new ArgumentException("Invalid field definition format: no valid field patterns found", nameof(definition));
        }

        // Check for bracket count mismatch (e.g., missing closing bracket)
        var bracketCount = definition.Count(c => c == '[') + definition.Count(c => c == ']');
        var matchedBrackets = matches.Count * 2;
        
        if (bracketCount != matchedBrackets && bracketCount > 0)
        {
            throw new ArgumentException("Invalid field definition format: mismatched or malformed brackets", nameof(definition));
        }

        // Process matched fields
        foreach (Match match in matches)
        {
            var fieldName = match.Groups[1].Value.Trim();
            var typeOrLength = match.Groups[2].Value.Trim();

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                throw new ArgumentException("Field name cannot be empty", nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(typeOrLength))
            {
                throw new ArgumentException($"Type or length cannot be empty for field '{fieldName}'", nameof(definition));
            }

            fields.Add(new StreamFieldDefinition
            {
                Name = fieldName,
                TypeOrLength = typeOrLength
            });
        }

        return fields;
    }

    public async Task<bool> WriteAsync(string streamDefinition, object?[] data, CancellationToken cancellationToken = default)
    {
        try
        {
            var fields = ParseStreamDefinition(streamDefinition);
            
            if (fields.Count != data.Length)
            {
                throw new ArgumentException($"Data array length ({data.Length}) does not match field count ({fields.Count})");
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
                        throw new ArgumentException($"Variable length field '{field.Name}' references undefined field '{field.TypeOrLength}'");
                    }
                }
                availableFields.Add(field.Name);
            }

            var writtenValues = new Dictionary<string, object>();

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var value = data[i];

                if (field.IsVariableLength)
                {
                    if (value is byte[] byteArray)
                    {
                        await _stream.WriteAsync(byteArray, cancellationToken);
                        writtenValues[field.Name] = byteArray;
                    }
                    else
                    {
                        throw new ArgumentException($"Field '{field.Name}' expects byte array but got {value?.GetType().Name ?? "null"}");
                    }
                }
                else
                {
                    var bytes = ConvertToBytes(value, field.TypeOrLength);
                    await _stream.WriteAsync(bytes, cancellationToken);
                    writtenValues[field.Name] = value;
                }
            }

            await _stream.FlushAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ConvertToBytes(object? value, string type)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        return type.ToLowerInvariant() switch
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
            _ => throw new ArgumentException($"Unsupported type: {type}")
        };
    }

    private static async Task<object> ReadFixedTypeAsync(Stream stream, string type, CancellationToken cancellationToken)
    {
        var typeSize = StreamFieldDefinition.GetTypeSize(type);
        var buffer = new byte[typeSize];
        await stream.ReadExactlyAsync(buffer, cancellationToken);

        return type.ToLowerInvariant() switch
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
            _ => throw new ArgumentException($"Unsupported type: {type}")
        };
    }
}