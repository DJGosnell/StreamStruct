namespace StreamStruct;

internal class StreamFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public string TypeOrLength { get; set; } = string.Empty;
    public bool IsVariableLength => !IsFixedType(TypeOrLength);

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

    public static int GetTypeSize(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "byte" or "sbyte" or "bool" => 1,
            "short" or "ushort" or "char" => 2,
            "int" or "uint" or "float" => 4,
            "long" or "ulong" or "double" => 8,
            _ => throw new ArgumentException($"Unknown type: {type}")
        };
    }
}