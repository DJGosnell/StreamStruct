# StreamStruct

A .NET library for parsing and processing structured binary data from streams using field definition syntax.

## Features

- **Type-safe parsing** of binary streams using intuitive field definitions
- **Bidirectional communication** support for client-server scenarios
- **Variable-length fields** with dynamic sizing based on previously parsed values
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
var definition = "[id:int][name_length:byte][name:name_length]";

// Write structured data
var data = "Alice"u8.ToArray();
await processor.WriteAsync(definition, [42, (byte)data.Length, data]);

// Read it back
var result = await processor.ReadAsync(definition);
if (result.Success)
{
    result.TryRead<int>("id", out var id); // 42
    result.TryRead<byte>("name_length", out var nameLength); // 5
    result.TryRead<byte[]>("name", out var nameBytes); // 5
    result.TryReadUtf8("name", out var nameData); // Alice
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