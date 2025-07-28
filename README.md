# StreamStruct

A .NET library for parsing and processing structured binary data from streams using field definition syntax.

## Features

- **Type-safe parsing** of binary streams using intuitive field definitions
- **Bidirectional communication** support for client-server scenarios
- **Variable-length fields** with dynamic sizing based on previously parsed values
- **Field validation** including duplicate field name detection and reserved name checking
- **Comprehensive error handling** with detailed error codes and messages
- **Async/await support** throughout the API

## Installation

Add the StreamStruct library to your project

## Quick Start

### Basic Usage

```csharp
using StreamStruct;

// Create a processor with your stream
var stream = new EchoMemoryStream();
var processor = new StreamFieldProcessor(stream);

// Define the structure: [fieldName:type]
var definition = "[id:int][name_length:byte][name:name_length][flags:int:4]";

// Write structured data
var data = "Alice"u8.ToArray();
var flagIds = new[] { 1,2,3,4 };
await processor.WriteAsync(definition, [42, (byte)data.Length, data, flagIds]);

// Read it back
var result = await processor.ReadAsync(definition);
if (result.Success)
{
    result.TryRead<int>("id", out var id); // 42
    result.TryRead<byte>("name_length", out var nameLength); // 5
    result.TryRead<byte[]>("name", out var nameBytes); // 5
    result.TryReadUtf8("name", out var nameData); // Alice
    result.TryRead<int[]>("flags", out var flagIdData); // [1,2,3,4]
}
```

### Supported Types

- **Fixed-size types**: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `char`, `bool`
- **Variable-length fields**: Reference previously parsed fields for dynamic sizing

### Variable-Length Fields Example

```csharp
// The 'message' field size is determined by the 'length' field
var definition = "[length:ushort][message:length]";

await processor.WriteAsync(definition, [(ushort)messageBytes.Length, "Hello, World!"u8.ToArray()]);
```

### Type-Safe Reading

```csharp
var result = await processor.ReadAsync("[value:int]");
if (result.TryRead<int>(0, out var value))
    Console.WriteLine($"Read integer: {value}");

```

## Field Definition Syntax

Fields are defined using the format: `[fieldName:typeOrLength]`

### Examples:
- `[id:int]` - A 32-bit integer field named "id"
- `[count:byte]` - An 8-bit unsigned integer field named "count"  
- `[data:count]` - A variable-length byte array whose size is determined by the "count" field
- `[temperature:float]` - A 32-bit floating-point field named "temperature"

## Field Validation

StreamStruct performs comprehensive validation of field definitions to ensure data integrity:

### Duplicate Field Names
Field names must be unique within a stream definition. Duplicate field names are automatically detected and rejected:

```csharp
// ❌ This will fail with ParseError.DuplicateFieldName
var result = await processor.ReadAsync("[id:int][name:byte][id:float]");
```

### Reserved Field Names
Field names cannot use reserved type names (`byte`, `int`, `float`, etc.) to prevent conflicts:

```csharp
// ❌ This will fail with ParseError.ReservedFieldName  
var result = await processor.ReadAsync("[int:byte]");
```

### Error Handling
When validation fails, `ParseResult` provides detailed error information via `ErrorCode` and `ErrorMessage` properties.

## Bidirectional Communication

For testing or in-memory communication:

```csharp
using var bidirectional = new BidirectionalMemoryStream();
var serverProcessor = new StreamFieldProcessor(bidirectional.Server);
var clientProcessor = new StreamFieldProcessor(bidirectional.Client);

// Server writes
await serverProcessor.WriteAsync("[msg:int]", new object[] { 123 });

// Client reads
var result = await clientProcessor.ReadAsync("[msg:int]");
```

## Building

```bash
dotnet build src/StreamStruct.slnx
```

## Testing

```bash
dotnet test src/StreamStruct.Tests/StreamStruct.Tests.csproj
```

## License

This project is licensed under the MIT License.