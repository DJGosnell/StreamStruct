using System.Text.RegularExpressions;

namespace StreamStruct;

public class StreamFieldProcessor
{
    private static readonly Regex FieldPattern = new(@"\[([^:\]]+):([^:\]]+)(?::([^:\]]+))?\]", RegexOptions.Compiled);
    private readonly Stream _stream;

    public StreamFieldProcessor(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public async Task<ParseResult> ReadAsync(string streamDefinition, CancellationToken cancellationToken = default)
    {
        var (parseError, fields) = ParseStreamDefinition(streamDefinition);
        if (parseError != ParseError.Success)
        {
            return ParseResult.CreateFailure(parseError);
        }

        try
        {
            var results = new object?[fields!.Count];
            var parsedValues = new Dictionary<string, object>();
            var fieldIndexes = new Dictionary<string, int>();

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                fieldIndexes[field.Name] = i;
                
                if (field.IsVariableLength)
                {
                    if (!parsedValues.TryGetValue(field.TypeOrLength, out var lengthValue))
                    {
                        return ParseResult.CreateFailure(ParseError.MissingVariableReference);
                    }

                    var length = Convert.ToInt32(lengthValue);
                    var buffer = new byte[length];
                    await _stream.ReadExactlyAsync(buffer, cancellationToken);
                    results[i] = buffer;
                    parsedValues[field.Name] = buffer;
                }
                else if (field.IsFixedArray)
                {
                    var (readError, arrayValue) = await ReadFixedArrayAsync(_stream, field.BaseType, field.FixedCount!.Value, cancellationToken);
                    if (readError != ParseError.Success)
                    {
                        return ParseResult.CreateFailure(readError);
                    }
                    results[i] = arrayValue;
                    parsedValues[field.Name] = arrayValue!;
                }
                else
                {
                    var (readError, value) = await ReadFixedTypeAsync(_stream, field.TypeOrLength, cancellationToken);
                    if (readError != ParseError.Success)
                    {
                        return ParseResult.CreateFailure(readError);
                    }
                    results[i] = value;
                    parsedValues[field.Name] = value!;
                }
            }

            return ParseResult.CreateSuccess(results, fieldIndexes);
        }
        catch (OperationCanceledException)
        {
            return ParseResult.CreateFailure(ParseError.OperationCancelled);
        }
        catch
        {
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

    public async Task<bool> WriteAsync(string streamDefinition, object?[] data, CancellationToken cancellationToken = default)
    {
        var (parseError, fields) = ParseStreamDefinition(streamDefinition);
        if (parseError != ParseError.Success || fields == null)
        {
            return false;
        }

        if (data == null)
        {
            return false;
        }
        
        if (fields.Count != data.Length)
        {
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
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var value = data[i];

                if (field.IsVariableLength)
                {
                    if (value is not byte[] byteArray)
                    {
                        return false;
                    }
                    await _stream.WriteAsync(byteArray, cancellationToken);
                }
                else if (field.IsFixedArray)
                {
                    var (convertError, bytes) = ConvertArrayToBytes(value, field.BaseType, field.FixedCount!.Value);
                    if (convertError != ParseError.Success || bytes == null)
                    {
                        return false;
                    }
                    await _stream.WriteAsync(bytes, cancellationToken);
                }
                else
                {
                    var (convertError, bytes) = ConvertToBytes(value, field.TypeOrLength);
                    if (convertError != ParseError.Success || bytes == null)
                    {
                        return false;
                    }
                    await _stream.WriteAsync(bytes, cancellationToken);
                }
            }

            await _stream.FlushAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
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
}