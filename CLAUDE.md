# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

StreamStruct is a .NET 9.0 library for parsing and processing structured binary data from streams using field definition syntax. The library enables bidirectional communication through streams with type-safe field parsing.

## Architecture

The codebase consists of three core components that work together:

### Core Classes
- **StreamFieldProcessor** (main entry point): Handles stream definition parsing, reading from streams, writing to streams, and type conversion. Takes a stream and provides `ReadAsync()` and `WriteAsync()` methods.
- **StreamFieldDefinition** (internal): Represents individual field definitions with name, type/length, and helper methods for type validation and size calculation.
- **ParseResult**: Result object containing success/failure status, parsed data array, error codes, and helper methods like `TryRead<T>()`.

### Stream Implementation
- **BidirectionalMemoryStream**: Channel-based in-memory stream that creates two connected endpoints (Client/Server) for testing bidirectional communication.
- **ConnectedStream**: Custom Stream implementation using .NET Channels for async communication between endpoints.

### Field Definition Syntax
The library uses a bracket-based syntax: `[fieldName:type]` where:
- Fixed types: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `char`, `bool`
- Variable length: `[data:lengthField]` where `lengthField` references a previously parsed field containing the byte count

## Development Commands

### Building
```bash
# Build the solution
dotnet build src/StreamStruct.slnx

# Build with specific configuration
dotnet build src/StreamStruct/StreamStruct.csproj --configuration Release
```

### Testing
```bash
# Run all tests
dotnet test src/StreamStruct.Tests/StreamStruct.Tests.csproj

# Run with verbose output
dotnet test src/StreamStruct.Tests/StreamStruct.Tests.csproj --verbosity normal

# Run a specific test
dotnet test src/StreamStruct.Tests/StreamStruct.Tests.csproj --filter "TestMethodName"
```

### Project Structure
- **src/StreamStruct/**: Main library code
- **src/StreamStruct.Tests/**: NUnit test project with bidirectional communication tests
- **Test framework**: NUnit 4.2.2 with test adapter and coverage collection

## Key Implementation Details

### Error Handling
The library uses comprehensive error handling with specific `ParseError` enum values and detailed error messages. All parsing operations return `ParseResult` objects instead of throwing exceptions.

### Stream Processing Flow
1. Parse field definition string using regex pattern `\[([^:]+):([^\]]+)\]`
2. Validate field definitions and bracket matching
3. Process fields sequentially, maintaining a dictionary of parsed values for variable-length field resolution
4. Return results in a strongly-typed `ParseResult` object

### Testing Pattern
Tests use the `BidirectionalMemoryStream` to simulate client-server communication, where one endpoint writes structured data and the other reads it back using the same field definitions.

## GitHub Actions

The repository includes a `.github/workflows/dotnet.yml` workflow that:
- Runs on Ubuntu with .NET 9.0
- Builds and tests on all PRs and pushes to master
- Creates releases with binaries when tags matching `v*` are pushed
- Uses `softprops/action-gh-release@v1` for release creation