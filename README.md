# StreamStruct

[![.NET](https://github.com/djgosnell/StreamStruct/actions/workflows/dotnet.yml/badge.svg)](https://github.com/djgosnell/StreamStruct/actions/workflows/dotnet.yml)

A .NET library for parsing and processing structured binary data from streams using field definition syntax.

## Features

- **Type-safe parsing** of binary streams using intuitive field definitions
- **Automatic type casting** for flexible data input with type safety
- **Bidirectional communication** support for client-server scenarios
- **Variable-length fields** with dynamic sizing based on previously parsed values
- **Field validation** including duplicate field name detection and reserved name checking
- **Comprehensive error handling** with detailed error codes and messages
- **Async/await support** throughout the API

## Installation

Install the StreamStruct NuGet package:

### Package Manager Console
```powershell
Install-Package StreamStruct
```

### .NET CLI
```bash
dotnet add package StreamStruct
```

### PackageReference
```xml
<PackageReference Include="StreamStruct" />
```

**NuGet Package**: https://www.nuget.org/packages/StreamStruct/

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

## Automatic Type Casting

StreamStruct automatically casts data to the appropriate field types when writing, providing flexibility while maintaining type safety:

### Flexible Data Input

```csharp
var definition = "[id:int][score:float][active:bool][grade:char]";

// All of these work - values are automatically cast to field types
await processor.WriteAsync(definition, [
    42,         // int -> stays int
    "85.5",     // string -> float
    1,          // int -> bool (true)
    "A"         // string -> char
]);

// Same as explicitly casting:
await processor.WriteAsync(definition, [
    (int)42,
    (float)85.5,
    (bool)true,
    (char)'A'
]);
```

### Supported Conversions

- **Numeric types**: Automatic conversion between all numeric types (`int`, `float`, `double`, etc.)
- **String to primitives**: Numeric strings to numbers, "true"/"false" to booleans
- **String to char**: Single-character strings to `char`
- **Boolean conversions**: Numbers (0=false, non-zero=true) and string representations

### Variable-Length Fields Are Not Cast

Variable-length fields (byte arrays) are not subject to type casting and must always be provided as `byte[]`:

```csharp
var definition = "[length:int][data:length]";
var data = "Hello"u8.ToArray();  // Must be byte[]
await processor.WriteAsync(definition, [data.Length, data]);
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
// This will fail with ParseError.DuplicateFieldName
var result = await processor.ReadAsync("[id:int][name:byte][id:float]");
```

### Reserved Field Names
Field names cannot use reserved type names (`byte`, `int`, `float`, etc.) to prevent conflicts:

```csharp
// This will fail with ParseError.ReservedFieldName  
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