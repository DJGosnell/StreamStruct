# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

StreamStruct is a .NET 9.0 library for parsing and processing structured binary data from streams using field definition syntax. The library provides type-safe parsing with support for variable-length fields and bidirectional communication.

## Development Commands

### Building
```bash
# Build the main library
dotnet build src/StreamStruct/StreamStruct.csproj

# Build entire solution
dotnet build src/StreamStruct.slnx

# Build for release
dotnet build src/StreamStruct/StreamStruct.csproj --configuration Release
```

### Testing
```bash
# Run all tests
dotnet test src/StreamStruct.Tests/StreamStruct.Tests.csproj

# Run tests with verbose output
dotnet test src/StreamStruct.Tests/StreamStruct.Tests.csproj --verbosity normal

# Run tests for release configuration
dotnet test src/StreamStruct.Tests/StreamStruct.Tests.csproj --configuration Release

# Run a specific test
dotnet test src/StreamStruct.Tests/StreamStruct.Tests.csproj --filter "TestMethodName"

```

### Dependencies
```bash
# Restore dependencies
dotnet restore src/StreamStruct/StreamStruct.csproj
```

### NuGet Package
```bash
# Pack NuGet package
dotnet pack src/StreamStruct/StreamStruct.csproj --configuration Release --output ./artifacts
```

## Architecture

### Core Components

- **StreamFieldProcessor** (`src/StreamStruct/StreamFieldProcessor.cs`): Main class for processing structured binary data from streams. Handles both reading and writing operations using field definition syntax.

- **ParseResult** (`src/StreamStruct/ParseResult.cs`): Result container that holds parsed data with type-safe accessors and comprehensive error handling. Includes `ParseError` enum with detailed error codes.

- **StreamFieldDefinition** (`src/StreamStruct/StreamFieldDefinition.cs`): Internal class that represents field definitions, supporting both fixed-size types and variable-length fields.

- **BidirectionalMemoryStream** (`src/StreamStruct/BidirectionalMemoryStream.cs`): Testing utility that enables bidirectional communication between client and server processors.

- **EchoMemoryStream** (`src/StreamStruct/EchoMemoryStream.cs`): Testing utility that echoes written data back for reading.

### Field Definition Syntax

The library uses bracket notation for field definitions: `[fieldName:typeOrLength]` or `[fieldName:type:count]`

- Fixed types: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `char`, `bool`
- Variable-length fields reference previously parsed fields for dynamic sizing
- Fixed arrays use count parameter: `[data:int:4]` for array of 4 integers

### Key Features

- Type-safe reading with `TryRead<T>()` methods on ParseResult
- Comprehensive validation including duplicate and reserved field name detection
- Async/await support throughout the API
- UTF-8 string reading support with `TryReadUtf8()`
- Error handling via `ParseError` enum and detailed error messages

### Test Framework

Uses NUnit 4.x with .NET 9.0. Tests cover field parsing, validation, bidirectional communication, and error scenarios.

## Project Structure

- `StreamStruct/`: Main library project
- `StreamStruct.Tests/`: NUnit test project with comprehensive test coverage
- `StreamStructSandbox/`: Console application for testing and examples