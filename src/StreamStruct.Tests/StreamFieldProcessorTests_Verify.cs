using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace StreamStruct.Tests;

[TestFixture]
public class StreamFieldProcessorTests_Verify
{
    private MemoryStream _stream = null!;
    private StreamFieldProcessor _processor = null!;

    [SetUp]
    public void Setup()
    {
        _stream = new MemoryStream();
        _processor = new StreamFieldProcessor(_stream);
    }

    [TearDown]
    public void TearDown()
    {
        _stream?.Dispose();
    }

    [Test]
    public async Task VerifyAsync_SingleByte_Success()
    {
        // Arrange
        var definition = "[value:byte]";
        var expectedValues = new object[] { (byte)42 };
        await _processor.WriteAsync(definition, expectedValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task VerifyAsync_SingleByte_Mismatch()
    {
        // Arrange
        var definition = "[value:byte]";
        var writeValues = new object[] { (byte)42 };
        var expectedValues = new object[] { (byte)24 };
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public async Task VerifyAsync_MultipleIntegers_Success()
    {
        // Arrange
        var definition = "[first:int][second:int][third:int]";
        var expectedValues = new object[] { 100, 200, 300 };
        await _processor.WriteAsync(definition, expectedValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task VerifyAsync_MultipleIntegers_OneMismatch()
    {
        // Arrange
        var definition = "[first:int][second:int][third:int]";
        var writeValues = new object[] { 100, 200, 300 };
        var expectedValues = new object[] { 100, 999, 300 };
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public async Task VerifyAsync_AllPrimitiveTypes_Success()
    {
        // Arrange
        var definition = "[b:byte][sb:sbyte][s:short][us:ushort][i:int][ui:uint][l:long][ul:ulong][f:float][d:double][c:char][flag:bool]";
        var expectedValues = new object[] 
        { 
            (byte)1, (sbyte)-1, (short)2, (ushort)3, 
            4, (uint)5, 6L, (ulong)7, 
            8.5f, 9.5, 'A', true 
        };
        await _processor.WriteAsync(definition, expectedValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task VerifyAsync_ByteArray_Success()
    {
        // Arrange
        var definition = "[data:byte:4]";
        var expectedValues = new object[] { new byte[] { 1, 2, 3, 4 } };
        await _processor.WriteAsync(definition, expectedValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task VerifyAsync_ByteArray_Mismatch()
    {
        // Arrange
        var definition = "[data:byte:4]";
        var writeValues = new object[] { new byte[] { 1, 2, 3, 4 } };
        var expectedValues = new object[] { new byte[] { 1, 2, 3, 5 } };
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public async Task VerifyAsync_IntArray_Success()
    {
        // Arrange
        var definition = "[numbers:int:3]";
        var expectedValues = new object[] { new int[] { 100, 200, 300 } };
        await _processor.WriteAsync(definition, expectedValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task VerifyAsync_IntArray_Mismatch()
    {
        // Arrange
        var definition = "[numbers:int:3]";
        var writeValues = new object[] { new int[] { 100, 200, 300 } };
        var expectedValues = new object[] { new int[] { 100, 999, 300 } };
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public async Task VerifyAsync_VariableLength_Success()
    {
        // Arrange
        var definition = "[length:int][data:length]";
        var data = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
        var expectedValues = new object[] { data.Length, data };
        await _processor.WriteAsync(definition, expectedValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task VerifyAsync_VariableLength_Mismatch()
    {
        // Arrange
        var definition = "[length:int][data:length]";
        var writeData = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
        var expectedData = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x99 };
        var writeValues = new object[] { writeData.Length, writeData };
        var expectedValues = new object[] { writeData.Length, expectedData };
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public async Task VerifyAsync_TypeCasting_IntToFloat_Success()
    {
        // Arrange
        var definition = "[value:float]";
        var writeValues = new object[] { 42.5f };
        var expectedValues = new object[] { 42.5 }; // double cast to float
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task VerifyAsync_NullExpectedValues_ReturnsFalse()
    {
        // Arrange
        var definition = "[value:int]";

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, null!);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public async Task VerifyAsync_InvalidDefinition_ReturnsFalse()
    {
        // Arrange
        var invalidDefinition = "[invalid";
        var expectedValues = new object[] { 42 };

        // Act
        var (success, errors) = await _processor.VerifyAsync(invalidDefinition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public async Task VerifyAsync_MismatchedFieldCount_ReturnsFalse()
    {
        // Arrange
        var definition = "[first:int][second:int]";
        var expectedValues = new object[] { 42 }; // Only one value for two fields

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public async Task VerifyAsync_WrongArrayType_ReturnsFalse()
    {
        // Arrange
        var definition = "[data:int:3]";
        var expectedValues = new object[] { new byte[] { 1, 2, 3 } }; // byte array instead of int array

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public async Task VerifyAsync_WrongArraySize_ReturnsFalse()
    {
        // Arrange
        var definition = "[data:int:3]";
        var expectedValues = new object[] { new int[] { 1, 2 } }; // 2 elements instead of 3

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public async Task VerifyAsync_VariableLengthMismatchedSize_ReturnsFalse()
    {
        // Arrange
        var definition = "[length:int][data:length]";
        var writeData = new byte[] { 1, 2, 3 };
        var expectedData = new byte[] { 1, 2 }; // Different size than length field
        var writeValues = new object[] { 3, writeData };
        var expectedValues = new object[] { 3, expectedData };
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    [Test]
    public async Task VerifyAsync_ComplexStructure_Success()
    {
        // Arrange
        var definition = "[id:int][name_length:byte][name:name_length][scores:float:3][active:bool]";
        var nameBytes = System.Text.Encoding.UTF8.GetBytes("Test");
        var expectedValues = new object[] 
        { 
            123, 
            (byte)nameBytes.Length, 
            nameBytes, 
            new float[] { 85.5f, 92.0f, 78.5f }, 
            true 
        };
        await _processor.WriteAsync(definition, expectedValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task VerifyAsync_ComplexStructure_OneMismatch()
    {
        // Arrange
        var definition = "[id:int][name_length:byte][name:name_length][scores:float:3][active:bool]";
        var nameBytes = System.Text.Encoding.UTF8.GetBytes("Test");
        var writeValues = new object[] 
        { 
            123, 
            (byte)nameBytes.Length, 
            nameBytes, 
            new float[] { 85.5f, 92.0f, 78.5f }, 
            true 
        };
        var expectedValues = new object[] 
        { 
            123, 
            (byte)nameBytes.Length, 
            nameBytes, 
            new float[] { 85.5f, 99.9f, 78.5f }, // Different middle score
            true 
        };
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors, Is.Not.Empty);
    }

    // New tests for ensuring all validation errors are collected

    [Test]
    public async Task VerifyAsync_MultipleFieldMismatches_CollectsAllErrors()
    {
        // Arrange - create a scenario with multiple mismatches
        var definition = "[first:int][second:byte][third:float][fourth:bool]";
        var writeValues = new object[] { 100, (byte)50, 75.5f, true };
        var expectedValues = new object[] { 999, (byte)99, 88.8f, false }; // All different
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(4), "Should collect all 4 validation errors");
        
        // Verify each error contains correct field information
        Assert.That(errors[0].FieldDefinition, Is.EqualTo("[first:int]"));
        Assert.That(errors[0].ActualValue, Is.EqualTo(100));
        Assert.That(errors[0].ExpectedValue, Is.EqualTo(999));
        
        Assert.That(errors[1].FieldDefinition, Is.EqualTo("[second:byte]"));
        Assert.That(errors[1].ActualValue, Is.EqualTo((byte)50));
        Assert.That(errors[1].ExpectedValue, Is.EqualTo((byte)99));
        
        Assert.That(errors[2].FieldDefinition, Is.EqualTo("[third:float]"));
        Assert.That(errors[2].ActualValue, Is.EqualTo(75.5f));
        Assert.That(errors[2].ExpectedValue, Is.EqualTo(88.8f));
        
        Assert.That(errors[3].FieldDefinition, Is.EqualTo("[fourth:bool]"));
        Assert.That(errors[3].ActualValue, Is.EqualTo(true));
        Assert.That(errors[3].ExpectedValue, Is.EqualTo(false));
    }

    [Test]
    public async Task VerifyAsync_MixedSuccessAndFailure_CollectsOnlyFailures()
    {
        // Arrange - alternating success and failure pattern
        var definition = "[a:int][b:byte][c:short][d:float][e:bool]";
        var writeValues = new object[] { 10, (byte)20, (short)30, 40.5f, true };
        var expectedValues = new object[] { 10, (byte)99, (short)30, 88.8f, true }; // b and d mismatch
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(2), "Should only collect errors for mismatched fields");
        
        // Verify only the mismatched fields have errors
        Assert.That(errors[0].FieldDefinition, Is.EqualTo("[b:byte]"));
        Assert.That(errors[0].ActualValue, Is.EqualTo((byte)20));
        Assert.That(errors[0].ExpectedValue, Is.EqualTo((byte)99));
        
        Assert.That(errors[1].FieldDefinition, Is.EqualTo("[d:float]"));
        Assert.That(errors[1].ActualValue, Is.EqualTo(40.5f));
        Assert.That(errors[1].ExpectedValue, Is.EqualTo(88.8f));
    }

    [Test]
    public async Task VerifyAsync_ArrayFieldMismatches_CollectsAllArrayErrors()
    {
        // Arrange - multiple array fields with mismatches
        var definition = "[bytes:byte:3][ints:int:2][floats:float:2]";
        var writeValues = new object[] 
        { 
            new byte[] { 1, 2, 3 }, 
            new int[] { 100, 200 }, 
            new float[] { 1.1f, 2.2f } 
        };
        var expectedValues = new object[] 
        { 
            new byte[] { 1, 2, 9 }, // Last byte different
            new int[] { 100, 999 }, // Last int different
            new float[] { 9.9f, 2.2f } // First float different
        };
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(3), "Should collect errors for all mismatched arrays");
        
        Assert.That(errors[0].FieldDefinition, Is.EqualTo("[bytes:byte:3]"));
        Assert.That(errors[1].FieldDefinition, Is.EqualTo("[ints:int:2]"));
        Assert.That(errors[2].FieldDefinition, Is.EqualTo("[floats:float:2]"));
    }

    [Test]
    public async Task VerifyAsync_VariableLengthMismatches_CollectsAllErrors()
    {
        // Arrange - multiple variable length fields with different types of errors
        var definition = "[len1:byte][data1:len1][len2:byte][data2:len2]";
        var data1 = new byte[] { 0x10, 0x20, 0x30 };
        var data2 = new byte[] { 0x40, 0x50 };
        var writeValues = new object[] { (byte)data1.Length, data1, (byte)data2.Length, data2 };
        
        var expectedData1 = new byte[] { 0x10, 0x20, 0x99 }; // Last byte different
        var expectedData2 = new byte[] { 0x99, 0x50 }; // First byte different
        var expectedValues = new object[] { (byte)data1.Length, expectedData1, (byte)data2.Length, expectedData2 };
        
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(2), "Should collect errors for both variable length fields");
        
        Assert.That(errors[0].FieldDefinition, Is.EqualTo("[data1:len1]"));
        Assert.That(errors[1].FieldDefinition, Is.EqualTo("[data2:len2]"));
    }

    [Test]
    public async Task VerifyAsync_StreamOffsetAccuracy_VerifyOffsets()
    {
        // Arrange - test that stream offsets are correctly recorded in validation errors
        var definition = "[a:int][b:byte][c:short]"; // int=4 bytes, byte=1 byte, short=2 bytes
        var writeValues = new object[] { 1000, (byte)50, (short)300 };
        var expectedValues = new object[] { 9999, (byte)99, (short)999 }; // All different
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(3));
        
        // Check stream offsets are correct
        Assert.That(errors[0].StreamOffset, Is.EqualTo(0), "First field should be at offset 0");
        Assert.That(errors[1].StreamOffset, Is.EqualTo(4), "Second field should be at offset 4 (after 4-byte int)");
        Assert.That(errors[2].StreamOffset, Is.EqualTo(5), "Third field should be at offset 5 (after int + byte)");
    }

    [Test]
    public async Task VerifyAsync_ComplexStructureAllFieldsMismatch_CollectsAllErrors()
    {
        // Arrange - complex structure where every field mismatches
        var definition = "[id:int][flags:byte][scores:float:2][name_len:byte][name:name_len][active:bool]";
        var nameBytes = System.Text.Encoding.UTF8.GetBytes("Test");
        var writeValues = new object[] 
        { 
            123, 
            (byte)0x0F, 
            new float[] { 85.5f, 92.0f }, 
            (byte)nameBytes.Length, 
            nameBytes, 
            true 
        };
        
        var expectedNameBytes = System.Text.Encoding.UTF8.GetBytes("Different");
        var expectedValues = new object[] 
        { 
            999, // Different ID
            (byte)0xFF, // Different flags
            new float[] { 99.9f, 88.8f }, // Different scores
            (byte)expectedNameBytes.Length, // Different name length
            expectedNameBytes, // Different name
            false // Different active flag
        };
        
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(6), "Should collect errors for all 6 fields");
        
        // Verify field definitions are captured correctly
        var expectedFieldDefs = new[] 
        {
            "[id:int]",
            "[flags:byte]", 
            "[scores:float:2]",
            "[name_len:byte]",
            "[name:name_len]",
            "[active:bool]"
        };
        
        for (int i = 0; i < errors.Count; i++)
        {
            Assert.That(errors[i].FieldDefinition, Is.EqualTo(expectedFieldDefs[i]), 
                $"Error {i} should have correct field definition");
            Assert.That(errors[i].ErrorMessage, Is.Not.Null.And.Not.Empty, 
                $"Error {i} should have a meaningful error message");
        }
    }

    [Test]
    public async Task VerifyAsync_ErrorMessagesContent_AreDescriptive()
    {
        // Arrange
        var definition = "[test:int]";
        var writeValues = new object[] { 42 };
        var expectedValues = new object[] { 999 };
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(1));
        
        var error = errors[0];
        Assert.That(error.ErrorMessage, Does.Contain("test"));
        Assert.That(error.ErrorMessage, Does.Contain("verification failed"));
        Assert.That(error.ErrorMessage, Does.Contain("42"));
        Assert.That(error.ErrorMessage, Does.Contain("999"));
    }

    [Test]
    public async Task VerifyAsync_TruncatedStream_ContinuesAndCollectsAllPossibleErrors()
    {
        // Arrange - create a stream that will run out of data partway through
        var definition = "[first:int][second:int][third:int]";
        var writeValues = new object[] { 100, 200 }; // Only write first two values
        await _processor.WriteAsync("[first:int][second:int]", writeValues);
        _stream.Position = 0;

        var expectedValues = new object[] { 999, 888, 777 }; // Expect all three to mismatch
        
        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(3), "Should collect errors for first two field mismatches and one read error");
        
        // First two should be value mismatches
        Assert.That(errors[0].FieldDefinition, Is.EqualTo("[first:int]"));
        Assert.That(errors[0].ActualValue, Is.EqualTo(100));
        Assert.That(errors[0].ExpectedValue, Is.EqualTo(999));
        
        Assert.That(errors[1].FieldDefinition, Is.EqualTo("[second:int]"));
        Assert.That(errors[1].ActualValue, Is.EqualTo(200));
        Assert.That(errors[1].ExpectedValue, Is.EqualTo(888));
        
        // Third should be a read error
        Assert.That(errors[2].FieldDefinition, Is.EqualTo("[third:int]"));
        Assert.That(errors[2].ErrorMessage, Does.Contain("Read error"));
    }

    [Test]
    public async Task VerifyAsync_EmptyStream_GeneratesReadErrorsForAllFields()
    {
        // Arrange - create an empty stream 
        _stream.SetLength(0);
        _stream.Position = 0;
        
        var definition = "[a:int][b:byte][c:short]";
        var expectedValues = new object[] { 42, (byte)24, (short)100 };

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(3), "Should generate read errors for all three fields");
        
        // All errors should be read errors
        foreach (var error in errors)
        {
            Assert.That(error.ErrorMessage, Does.Contain("Read error"));
        }
        
        // Verify field definitions are correct
        Assert.That(errors[0].FieldDefinition, Is.EqualTo("[a:int]"));
        Assert.That(errors[1].FieldDefinition, Is.EqualTo("[b:byte]"));
        Assert.That(errors[2].FieldDefinition, Is.EqualTo("[c:short]"));
    }

    [Test]
    public async Task VerifyAsync_PartialStreamWithArrays_ContinuesAfterReadErrors()
    {
        // Arrange - write partial data that will cause read errors on arrays
        var partialData = new byte[] { 1, 2, 3, 4 }; // Only 4 bytes (one int)
        _stream.Write(partialData);
        _stream.Position = 0;
        
        var definition = "[first:int][array1:byte:4][array2:int:2]";
        var expectedValues = new object[] 
        { 
            999, // This will mismatch 
            new byte[] { 10, 20, 30, 40 }, // This will cause read error (not enough data)
            new int[] { 100, 200 } // This will also cause read error
        };

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(3), "Should collect value mismatch + 2 read errors");
        
        // First error should be value mismatch
        Assert.That(errors[0].FieldDefinition, Is.EqualTo("[first:int]"));
        Assert.That(errors[0].ActualValue, Is.Not.EqualTo(999));
        
        // Second and third should be read errors
        Assert.That(errors[1].FieldDefinition, Is.EqualTo("[array1:byte:4]"));
        Assert.That(errors[1].ErrorMessage, Does.Contain("Read error"));
        
        Assert.That(errors[2].FieldDefinition, Is.EqualTo("[array2:int:2]"));
        Assert.That(errors[2].ErrorMessage, Does.Contain("Read error"));
    }

    [Test]
    public async Task VerifyAsync_MixedErrorTypes_CollectsAllErrorTypes()
    {
        // Arrange - create a scenario with validation errors and read errors
        var definition = "[id:int][value:byte][missing:int]";
        
        // Write only partial data: id + value, but missing the final int
        var partialData = new byte[5]; // 4 bytes for int + 1 byte for byte
        BitConverter.GetBytes(100).CopyTo(partialData, 0); // id = 100
        partialData[4] = 50; // value = 50
        _stream.Write(partialData);
        _stream.Position = 0;
        
        var expectedValues = new object[] 
        { 
            999, // id mismatch (validation error)
            (byte)50, // value matches
            42 // missing will cause read error
        };

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(2), "Should collect 1 value mismatch + 1 read error");
        
        // First should be value mismatch for id
        Assert.That(errors[0].FieldDefinition, Is.EqualTo("[id:int]"));
        Assert.That(errors[0].ActualValue, Is.EqualTo(100));
        Assert.That(errors[0].ExpectedValue, Is.EqualTo(999));
        
        // Second should be read error for missing field
        Assert.That(errors[1].FieldDefinition, Is.EqualTo("[missing:int]"));
        Assert.That(errors[1].ErrorMessage, Does.Contain("Read error"));
    }

    [Test]
    public async Task VerifyAsync_ValidationErrorFieldTypes_AreCorrect()
    {
        // Arrange - test that field types are correctly captured in ValidationError
        var definition = "[int_field:int][byte_field:byte][float_field:float][bool_field:bool]";
        var writeValues = new object[] { 100, (byte)50, 25.5f, true };
        var expectedValues = new object[] { 999, (byte)99, 88.8f, false };
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(4));
        
        // Verify field types are captured correctly
        Assert.That(errors[0].FieldType, Is.EqualTo("int"));
        Assert.That(errors[1].FieldType, Is.EqualTo("byte"));
        Assert.That(errors[2].FieldType, Is.EqualTo("float"));
        Assert.That(errors[3].FieldType, Is.EqualTo("bool"));
    }

    [Test]
    public async Task VerifyAsync_ArrayValidationErrorFieldTypes_AreCorrect()
    {
        // Arrange - test that array field types are correctly captured
        var definition = "[bytes:byte:2][ints:int:2]";
        var writeValues = new object[] 
        { 
            new byte[] { 1, 2 }, 
            new int[] { 100, 200 }
        };
        var expectedValues = new object[] 
        { 
            new byte[] { 9, 8 }, 
            new int[] { 999, 888 }
        };
        await _processor.WriteAsync(definition, writeValues);
        _stream.Position = 0;

        // Act
        var (success, errors) = await _processor.VerifyAsync(definition, expectedValues);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(errors.Count, Is.EqualTo(2));
        
        // Verify array field types are captured correctly
        Assert.That(errors[0].FieldType, Is.EqualTo("byte"));
        Assert.That(errors[1].FieldType, Is.EqualTo("int"));
    }
}