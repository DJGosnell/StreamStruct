namespace StreamStruct.Tests;

[TestFixture]
public class StreamFieldProcessorTests
{
    private BidirectionalMemoryStream? _bidirectionalStream;
    private StreamFieldProcessor? _serverParser;
    private StreamFieldProcessor? _clientParser;

    [SetUp]
    public void SetUp()
    {
        _bidirectionalStream = new BidirectionalMemoryStream();
        _serverParser = new StreamFieldProcessor(_bidirectionalStream.Server);
        _clientParser = new StreamFieldProcessor(_bidirectionalStream.Client);
    }

    [TearDown]
    public void TearDown()
    {
        _bidirectionalStream?.Dispose();
    }

    [Test]
    public async Task WriteAndRead_BasicIntegerCommunication_ShouldSucceed()
    {
        var definition = "[value:int]";
        var data = new object[] { 42 };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<int>(0, out var value), Is.True);
        Assert.That(value, Is.EqualTo(42));
    }

    [Test]
    public async Task WriteAndRead_MultipleFixedTypes_ShouldSucceed()
    {
        var definition = "[id:int][flag:bool][amount:float]";
        var data = new object[] { 123, true, 45.67f };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<int>(0, out var id), Is.True);
        Assert.That(readResult.TryRead<bool>(1, out var flag), Is.True);
        Assert.That(readResult.TryRead<float>(2, out var amount), Is.True);
        Assert.That(id, Is.EqualTo(123));
        Assert.That(flag, Is.True);
        Assert.That(amount, Is.EqualTo(45.67f).Within(0.001));
    }

    [Test]
    public async Task WriteAndRead_VariableLengthData_ShouldSucceed()
    {
        var definition = "[length:int][data:length]";
        var testData = "Hello World"u8.ToArray();
        var data = new object[] { testData.Length, testData };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<int>(0, out var length), Is.True);
        Assert.That(readResult.TryRead<byte[]>(1, out var receivedData), Is.True);
        Assert.That(length, Is.EqualTo(testData.Length));
        Assert.That(receivedData, Is.EqualTo(testData));
    }

    [Test]
    public async Task WriteAndRead_AllSupportedTypes_ShouldSucceed()
    {
        var definition = "[b:byte][sb:sbyte][s:short][us:ushort][i:int][ui:uint][l:long][ul:ulong][f:float][d:double][c:char][bool:bool]";
        var data = new object[] { 
            (byte)255, 
            (sbyte)-128, 
            (short)-32768, 
            (ushort)65535, 
            -2147483648, 
            4294967295u, 
            -9223372036854775808L, 
            18446744073709551615ul, 
            3.14159f, 
            2.718281828, 
            'X', 
            true 
        };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        
        Assert.That(readResult.TryRead<byte>(0, out var b), Is.True);
        Assert.That(readResult.TryRead<sbyte>(1, out var sb), Is.True);
        Assert.That(readResult.TryRead<short>(2, out var s), Is.True);
        Assert.That(readResult.TryRead<ushort>(3, out var us), Is.True);
        Assert.That(readResult.TryRead<int>(4, out var i), Is.True);
        Assert.That(readResult.TryRead<uint>(5, out var ui), Is.True);
        Assert.That(readResult.TryRead<long>(6, out var l), Is.True);
        Assert.That(readResult.TryRead<ulong>(7, out var ul), Is.True);
        Assert.That(readResult.TryRead<float>(8, out var f), Is.True);
        Assert.That(readResult.TryRead<double>(9, out var d), Is.True);
        Assert.That(readResult.TryRead<char>(10, out var c), Is.True);
        Assert.That(readResult.TryRead<bool>(11, out var boolVal), Is.True);

        Assert.That(b, Is.EqualTo((byte)255));
        Assert.That(sb, Is.EqualTo((sbyte)-128));
        Assert.That(s, Is.EqualTo((short)-32768));
        Assert.That(us, Is.EqualTo((ushort)65535));
        Assert.That(i, Is.EqualTo(-2147483648));
        Assert.That(ui, Is.EqualTo(4294967295u));
        Assert.That(l, Is.EqualTo(-9223372036854775808L));
        Assert.That(ul, Is.EqualTo(18446744073709551615ul));
        Assert.That(f, Is.EqualTo(3.14159f).Within(0.00001));
        Assert.That(d, Is.EqualTo(2.718281828).Within(0.000000001));
        Assert.That(c, Is.EqualTo('X'));
        Assert.That(boolVal, Is.True);
    }

    [Test]
    public async Task WriteAndRead_BidirectionalCommunication_ShouldSucceed()
    {
        var definition = "[message:int]";
        
        var serverToClientData = new object[] { 100 };
        var clientToServerData = new object[] { 200 };

        var serverWriteResult = await _serverParser!.WriteAsync(definition, serverToClientData).WithTimeout();
        Assert.That(serverWriteResult, Is.True);

        var clientReadResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(clientReadResult.Success, Is.True);
        Assert.That(clientReadResult.TryRead<int>(0, out var serverMessage), Is.True);
        Assert.That(serverMessage, Is.EqualTo(100));

        var clientWriteResult = await _clientParser!.WriteAsync(definition, clientToServerData).WithTimeout();
        Assert.That(clientWriteResult, Is.True);

        var serverReadResult = await _serverParser!.ReadAsync(definition).WithTimeout();
        Assert.That(serverReadResult.Success, Is.True);
        Assert.That(serverReadResult.TryRead<int>(0, out var clientMessage), Is.True);
        Assert.That(clientMessage, Is.EqualTo(200));
    }

    [Test]
    public async Task ReadAsync_MissingVariableLengthReference_ShouldFail()
    {
        var definition = "[data:missingLength]";
        var data = new object[] { new byte[] { 1, 2, 3 } };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.False);
    }

    [Test]
    public async Task WriteAsync_InvalidDataLength_ShouldFail()
    {
        var definition = "[value1:int][value2:int]";
        var data = new object[] { 42 };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.False);
    }

    [Test]
    public async Task WriteAndRead_EmptyByteArray_ShouldSucceed()
    {
        var definition = "[length:int][data:length]";
        var emptyData = Array.Empty<byte>();
        var data = new object[] { 0, emptyData };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<int>(0, out var length), Is.True);
        Assert.That(readResult.TryRead<byte[]>(1, out var receivedData), Is.True);
        Assert.That(length, Is.EqualTo(0));
        Assert.That(receivedData, Is.Empty);
    }

    [Test]
    public async Task WriteAndRead_LargeByteArray_ShouldSucceed()
    {
        var definition = "[length:int][data:length]";
        var largeData = new byte[1024];
        for (int i = 0; i < largeData.Length; i++)
        {
            largeData[i] = (byte)(i % 256);
        }
        var data = new object[] { largeData.Length, largeData };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout(5000);
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout(5000);
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<int>(0, out var length), Is.True);
        Assert.That(readResult.TryRead<byte[]>(1, out var receivedData), Is.True);
        Assert.That(length, Is.EqualTo(largeData.Length));
        Assert.That(receivedData, Is.EqualTo(largeData));
    }

    [Test]
    public void ReadAsync_WithTimeout_ShouldThrowTimeoutException()
    {
        var definition = "[value:int]";
        
        Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await _clientParser!.ReadAsync(definition).WithTimeout(100);
        });
    }

    [Test]
    public async Task WriteAndRead_ComplexVariableLengthStructure_ShouldSucceed()
    {
        var definition = "[headerSize:int][header:headerSize][dataSize:int][payload:dataSize]";
        var headerData = "HEADER"u8.ToArray();
        var payloadData = "This is the payload data"u8.ToArray();
        var data = new object[] { headerData.Length, headerData, payloadData.Length, payloadData };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<int>(0, out var headerSize), Is.True);
        Assert.That(readResult.TryRead<byte[]>(1, out var receivedHeader), Is.True);
        Assert.That(readResult.TryRead<int>(2, out var dataSize), Is.True);
        Assert.That(readResult.TryRead<byte[]>(3, out var receivedPayload), Is.True);
        
        Assert.That(headerSize, Is.EqualTo(headerData.Length));
        Assert.That(receivedHeader, Is.EqualTo(headerData));
        Assert.That(dataSize, Is.EqualTo(payloadData.Length));
        Assert.That(receivedPayload, Is.EqualTo(payloadData));
    }

    [Test]
    public async Task WriteAsync_NullDataArray_ShouldFail()
    {
        var definition = "[value:int]";
        
        var writeResult = await _serverParser!.WriteAsync(definition, null!).WithTimeout();
        Assert.That(writeResult, Is.False);
    }

    [Test]
    public async Task WriteAsync_EmptyDefinition_ShouldFail()
    {
        var definition = "";
        var data = new object[] { 42 };
        
        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.False);
    }

    [Test]
    public async Task WriteAsync_InvalidFieldDefinition_ShouldFail()
    {
        var definition = "[invalid_field_format]";
        var data = new object[] { 42 };
        
        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.False);
    }

    [Test]
    public async Task WriteAsync_UnsupportedType_ShouldFail()
    {
        var definition = "[value:unsupportedtype]";
        var data = new object[] { 42 };
        
        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.False);
    }

    [Test]
    public async Task WriteAsync_NullValueForFixedType_ShouldFail()
    {
        var definition = "[value:int]";
        var data = new object?[] { null };
        
        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.False);
    }

    [Test]
    public async Task WriteAsync_WrongTypeForVariableLength_ShouldFail()
    {
        var definition = "[length:int][data:length]";
        var data = new object[] { 5, "not a byte array" };
        
        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.False);
    }

    [Test]
    public async Task ReadAsync_EmptyDefinition_ShouldFail()
    {
        var definition = "";
        
        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.False);
        Assert.That(readResult.ErrorMessage, Contains.Substring("Stream definition cannot be null or empty"));
    }

    [Test]
    public async Task ReadAsync_InvalidFieldDefinition_ShouldFail()
    {
        var definition = "[invalid_field_format]";
        
        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.False);
        Assert.That(readResult.ErrorMessage, Contains.Substring("no valid field patterns found"));
    }

    [Test]
    public async Task WriteAndRead_MinMaxValues_ShouldSucceed()
    {
        var definition = "[minByte:byte][maxByte:byte][minSByte:sbyte][maxSByte:sbyte][minShort:short][maxShort:short]";
        var data = new object[] { 
            byte.MinValue, byte.MaxValue,
            sbyte.MinValue, sbyte.MaxValue,
            short.MinValue, short.MaxValue
        };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<byte>(0, out var minByte), Is.True);
        Assert.That(readResult.TryRead<byte>(1, out var maxByte), Is.True);
        Assert.That(readResult.TryRead<sbyte>(2, out var minSByte), Is.True);
        Assert.That(readResult.TryRead<sbyte>(3, out var maxSByte), Is.True);
        Assert.That(readResult.TryRead<short>(4, out var minShort), Is.True);
        Assert.That(readResult.TryRead<short>(5, out var maxShort), Is.True);

        Assert.That(minByte, Is.EqualTo(byte.MinValue));
        Assert.That(maxByte, Is.EqualTo(byte.MaxValue));
        Assert.That(minSByte, Is.EqualTo(sbyte.MinValue));
        Assert.That(maxSByte, Is.EqualTo(sbyte.MaxValue));
        Assert.That(minShort, Is.EqualTo(short.MinValue));
        Assert.That(maxShort, Is.EqualTo(short.MaxValue));
    }

    [Test]
    public async Task WriteAndRead_SpecialFloatingPointValues_ShouldSucceed()
    {
        var definition = "[positiveInf:float][negativeInf:float][nan:float][zero:double][negativeZero:double]";
        var data = new object[] { 
            float.PositiveInfinity, 
            float.NegativeInfinity, 
            float.NaN,
            0.0, 
            -0.0
        };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<float>(0, out var positiveInf), Is.True);
        Assert.That(readResult.TryRead<float>(1, out var negativeInf), Is.True);
        Assert.That(readResult.TryRead<float>(2, out var nan), Is.True);
        Assert.That(readResult.TryRead<double>(3, out var zero), Is.True);
        Assert.That(readResult.TryRead<double>(4, out var negativeZero), Is.True);

        Assert.That(positiveInf, Is.EqualTo(float.PositiveInfinity));
        Assert.That(negativeInf, Is.EqualTo(float.NegativeInfinity));
        Assert.That(float.IsNaN(nan), Is.True);
        Assert.That(zero, Is.EqualTo(0.0));
        Assert.That(negativeZero, Is.EqualTo(-0.0));
    }

    [Test]
    public async Task WriteAndRead_UnicodeCharacters_ShouldSucceed()
    {
        var definition = "[char1:char][char2:char][char3:char]";
        var data = new object[] { 'A', '€', '\u2603' };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<char>(0, out var char1), Is.True);
        Assert.That(readResult.TryRead<char>(1, out var char2), Is.True);
        Assert.That(readResult.TryRead<char>(2, out var char3), Is.True);

        Assert.That(char1, Is.EqualTo('A'));
        Assert.That(char2, Is.EqualTo('€'));
        Assert.That(char3, Is.EqualTo('\u2603'));
    }

    [Test]
    public async Task WriteAndRead_MultipleVariableLengthFields_ShouldSucceed()
    {
        var definition = "[len1:int][data1:len1][len2:int][data2:len2][len3:int][data3:len3]";
        var data1 = "First"u8.ToArray();
        var data2 = "Second message"u8.ToArray();
        var data3 = "Third"u8.ToArray();
        var data = new object[] { 
            data1.Length, data1,
            data2.Length, data2,
            data3.Length, data3
        };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<int>(0, out var len1), Is.True);
        Assert.That(readResult.TryRead<byte[]>(1, out var receivedData1), Is.True);
        Assert.That(readResult.TryRead<int>(2, out var len2), Is.True);
        Assert.That(readResult.TryRead<byte[]>(3, out var receivedData2), Is.True);
        Assert.That(readResult.TryRead<int>(4, out var len3), Is.True);
        Assert.That(readResult.TryRead<byte[]>(5, out var receivedData3), Is.True);

        Assert.That(len1, Is.EqualTo(data1.Length));
        Assert.That(receivedData1, Is.EqualTo(data1));
        Assert.That(len2, Is.EqualTo(data2.Length));
        Assert.That(receivedData2, Is.EqualTo(data2));
        Assert.That(len3, Is.EqualTo(data3.Length));
        Assert.That(receivedData3, Is.EqualTo(data3));
    }

    [Test]
    public async Task WriteAndRead_VariableLengthWithZeroLength_ShouldSucceed()
    {
        var definition = "[count:int][items:count]";
        var data = new object[] { 0, Array.Empty<byte>() };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<int>(0, out var count), Is.True);
        Assert.That(readResult.TryRead<byte[]>(1, out var items), Is.True);

        Assert.That(count, Is.EqualTo(0));
        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task ParseResult_TryReadWithInvalidIndex_ShouldReturnFalse()
    {
        var definition = "[value:int]";
        var data = new object[] { 42 };

        await _serverParser!.WriteAsync(definition, data).WithTimeout();
        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();

        Assert.That(readResult.TryRead<int>(-1, out _), Is.False);
        Assert.That(readResult.TryRead<int>(1, out _), Is.False);
        Assert.That(readResult.TryRead<int>(100, out _), Is.False);
    }

    [Test]
    public async Task ParseResult_TryReadWithWrongType_ShouldReturnFalse()
    {
        var definition = "[value:int]";
        var data = new object[] { 42 };

        await _serverParser!.WriteAsync(definition, data).WithTimeout();
        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();

        Assert.That(readResult.TryRead<int>(0, out _), Is.True);
        Assert.That(readResult.TryRead<string>(0, out _), Is.False);
        Assert.That(readResult.TryRead<float>(0, out _), Is.False);
    }

    [Test]
    public async Task WriteAndRead_CancellationToken_ShouldWork()
    {
        var definition = "[value:int]";
        var data = new object[] { 42 };

        using var cts = new CancellationTokenSource();
        
        var writeResult = await _serverParser!.WriteAsync(definition, data, cts.Token).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition, cts.Token).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<int>(0, out var value), Is.True);
        Assert.That(value, Is.EqualTo(42));
    }

    [Test]
    public async Task WriteAsync_CancelledToken_ShouldFail()
    {
        var definition = "[value:int]";
        var data = new object[] { 42 };

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var writeResult = await _serverParser!.WriteAsync(definition, data, cts.Token).WithTimeout();
        Assert.That(writeResult, Is.False);
    }

    [Test]
    public async Task ReadAsync_CancelledToken_ShouldFail()
    {
        var definition = "[value:int]";

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var readResult = await _clientParser!.ReadAsync(definition, cts.Token).WithTimeout();
        Assert.That(readResult.Success, Is.False);
        Assert.That(readResult.ErrorMessage, Is.Not.Null);
    }

    [Test]
    public async Task WriteAndRead_VeryLargeVariableLength_ShouldSucceed()
    {
        var definition = "[size:int][data:size]";
        var largeData = new byte[100000];
        var random = new Random(42);
        random.NextBytes(largeData);
        var data = new object[] { largeData.Length, largeData };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout(10000);
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout(10000);
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<int>(0, out var size), Is.True);
        Assert.That(readResult.TryRead<byte[]>(1, out var receivedData), Is.True);

        Assert.That(size, Is.EqualTo(largeData.Length));
        Assert.That(receivedData, Is.EqualTo(largeData));
    }

    [Test]
    public async Task WriteAndRead_SingleByteField_ShouldSucceed()
    {
        var definition = "[flag:byte]";
        var data = new object[] { (byte)255 };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<byte>(0, out var flag), Is.True);
        Assert.That(flag, Is.EqualTo((byte)255));
    }

    [Test]
    public async Task WriteAndRead_BooleanValues_ShouldSucceed()
    {
        var definition = "[true_val:bool][false_val:bool]";
        var data = new object[] { true, false };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<bool>(0, out var trueVal), Is.True);
        Assert.That(readResult.TryRead<bool>(1, out var falseVal), Is.True);
        Assert.That(trueVal, Is.True);
        Assert.That(falseVal, Is.False);
    }

    [Test]
    public async Task WriteAndRead_FieldNamesWithSpecialCharacters_ShouldSucceed()
    {
        var definition = "[field_1:int][field-2:int][field3_test:int]";
        var data = new object[] { 100, 200, 300 };

        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<int>(0, out var field1), Is.True);
        Assert.That(readResult.TryRead<int>(1, out var field2), Is.True);
        Assert.That(readResult.TryRead<int>(2, out var field3), Is.True);

        Assert.That(field1, Is.EqualTo(100));
        Assert.That(field2, Is.EqualTo(200));
        Assert.That(field3, Is.EqualTo(300));
    }

    [Test]
    public async Task ReadAsync_StreamClosed_ShouldFail()
    {
        var definition = "[value:int]";
        
        _bidirectionalStream!.Close();
        
        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.False);
    }

    [Test]
    public async Task ReadAsync_EmptyFieldName_ShouldFail()
    {
        var definition = "[:int]";
        
        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.False);
        Assert.That(readResult.ErrorMessage, Contains.Substring("Field name cannot be empty"));
    }

    [Test]
    public async Task ReadAsync_EmptyTypeOrLength_ShouldFail()
    {
        var definition = "[fieldname:]";
        
        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.False);
        Assert.That(readResult.ErrorMessage, Contains.Substring("Type or length cannot be empty"));
    }

    [Test]
    public async Task ReadAsync_MismatchedBrackets_ShouldFail()
    {
        var definition = "[field:int";
        
        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.False);
        Assert.That(readResult.ErrorMessage, Contains.Substring("mismatched or malformed brackets"));
        Assert.That(readResult.ErrorCode, Is.EqualTo(ParseError.MismatchedBrackets));
    }

    [Test]
    public async Task ParseResult_ErrorCodes_ShouldBeCorrect()
    {
        // Test empty definition
        var emptyResult = await _clientParser!.ReadAsync("").WithTimeout();
        Assert.That(emptyResult.ErrorCode, Is.EqualTo(ParseError.EmptyDefinition));

        // Test success
        await _serverParser!.WriteAsync("[value:int]", new object[] { 42 }).WithTimeout();
        var successResult = await _clientParser!.ReadAsync("[value:int]").WithTimeout();
        Assert.That(successResult.ErrorCode, Is.EqualTo(ParseError.Success));

        // Test missing variable reference
        var missingRefResult = await _clientParser!.ReadAsync("[data:missingVar]").WithTimeout();
        Assert.That(missingRefResult.ErrorCode, Is.EqualTo(ParseError.MissingVariableReference));
    }
}