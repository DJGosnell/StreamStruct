using System.Security.Cryptography;

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
    public async Task WriteAndRead_FixedLengthByteArray_ShouldSucceed()
    {
        var definition = "[value:byte:100][integers:int:5]";

        var data = new byte[100];
        var intData = new[] { 1, 2, 3, 4, 5 };
        RandomNumberGenerator.Fill(data);

        var writeResult =
            await _serverParser!.WriteAsync(definition, [data, intData]).WithTimeout();
        Assert.That(writeResult, Is.True);

        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.True);
        Assert.That(readResult.TryRead<byte[]>("value", out var value), Is.True);
        Assert.That(readResult.TryRead<int[]>("integers", out var integers), Is.True);
        Assert.That(value, Is.EqualTo(data));
        Assert.That(integers, Is.EqualTo(intData));
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
        var definition = "[b:byte][sb:sbyte][s:short][us:ushort][i:int][ui:uint][l:long][ul:ulong][f:float][d:double][c:char][flag:bool]";
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

    [Test]
    public async Task ReadAsync_ReservedFieldName_ShouldFail()
    {
        var definitions = new[]
        {
            "[byte:int]",
            "[sbyte:int]", 
            "[short:int]",
            "[ushort:int]",
            "[int:byte]",
            "[uint:int]",
            "[long:int]",
            "[ulong:int]",
            "[float:int]",
            "[double:int]",
            "[char:int]",
            "[bool:int]"
        };

        foreach (var definition in definitions)
        {
            var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
            Assert.That(readResult.Success, Is.False, $"Definition '{definition}' should fail validation");
            Assert.That(readResult.ErrorCode, Is.EqualTo(ParseError.ReservedFieldName), $"Definition '{definition}' should return ReservedFieldName error");
            Assert.That(readResult.ErrorMessage, Contains.Substring("Field name cannot be a reserved type name"), $"Definition '{definition}' should have correct error message");
        }
    }

    [Test]
    public async Task ReadAsync_DuplicateFieldName_ShouldFail()
    {
        var definitions = new[]
        {
            "[field:int][field:byte]",
            "[value:int][data:byte][value:short]",
            "[id:int][name:int][id:float]",
            "[test:byte][other:int][test:long]",
            "[a:int][b:int][c:int][a:byte]"
        };

        foreach (var definition in definitions)
        {
            var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
            Assert.That(readResult.Success, Is.False, $"Definition '{definition}' should fail due to duplicate field name");
            Assert.That(readResult.ErrorCode, Is.EqualTo(ParseError.DuplicateFieldName), $"Definition '{definition}' should return DuplicateFieldName error");
            Assert.That(readResult.ErrorMessage, Contains.Substring("Duplicate field name found in stream definition"), $"Definition '{definition}' should have correct error message");
        }
    }

    [Test]
    public async Task WriteAsync_DuplicateFieldName_ShouldFail()
    {
        var definition = "[value:int][value:byte]";
        var data = new object[] { 42, (byte)255 };
        
        var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
        Assert.That(writeResult, Is.False, "Write with duplicate field names should fail");
    }

    [Test]
    public async Task ReadAsync_DuplicateFieldNameWithVariableLength_ShouldFail()
    {
        var definition = "[length:int][data:length][length:byte]";
        
        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.False, "Definition with duplicate field name should fail");
        Assert.That(readResult.ErrorCode, Is.EqualTo(ParseError.DuplicateFieldName), "Should return DuplicateFieldName error");
    }

    [Test]
    public async Task ReadAsync_DuplicateFieldNameWithFixedArray_ShouldFail()
    {
        var definition = "[array:int:5][array:byte]";
        
        var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
        Assert.That(readResult.Success, Is.False, "Definition with duplicate field name should fail");
        Assert.That(readResult.ErrorCode, Is.EqualTo(ParseError.DuplicateFieldName), "Should return DuplicateFieldName error");
    }

    [Test]
    public async Task ReadAsync_MultipleReadsOnSameStream_ShouldSucceed()
    {
        // First message
        var definition1 = "[id:int][message:int]";
        var data1 = new object[] { 100, 200 };

        var writeResult1 = await _serverParser!.WriteAsync(definition1, data1).WithTimeout();
        Assert.That(writeResult1, Is.True);

        var readResult1 = await _clientParser!.ReadAsync(definition1).WithTimeout();
        Assert.That(readResult1.Success, Is.True);
        Assert.That(readResult1.TryRead<int>(0, out var id1), Is.True);
        Assert.That(readResult1.TryRead<int>(1, out var message1), Is.True);
        Assert.That(id1, Is.EqualTo(100));
        Assert.That(message1, Is.EqualTo(200));

        // Second message with different structure
        var definition2 = "[flag:bool][value:float][count:byte]";
        var data2 = new object[] { true, 3.14159f, (byte)42 };

        var writeResult2 = await _serverParser!.WriteAsync(definition2, data2).WithTimeout();
        Assert.That(writeResult2, Is.True);

        var readResult2 = await _clientParser!.ReadAsync(definition2).WithTimeout();
        Assert.That(readResult2.Success, Is.True);
        Assert.That(readResult2.TryRead<bool>(0, out var flag2), Is.True);
        Assert.That(readResult2.TryRead<float>(1, out var value2), Is.True);
        Assert.That(readResult2.TryRead<byte>(2, out var count2), Is.True);
        Assert.That(flag2, Is.True);
        Assert.That(value2, Is.EqualTo(3.14159f).Within(0.00001));
        Assert.That(count2, Is.EqualTo((byte)42));

        // Third message with variable length data
        var definition3 = "[length:int][data:length]";
        var testData3 = "Third message data"u8.ToArray();
        var data3 = new object[] { testData3.Length, testData3 };

        var writeResult3 = await _serverParser!.WriteAsync(definition3, data3).WithTimeout();
        Assert.That(writeResult3, Is.True);

        var readResult3 = await _clientParser!.ReadAsync(definition3).WithTimeout();
        Assert.That(readResult3.Success, Is.True);
        Assert.That(readResult3.TryRead<int>(0, out var length3), Is.True);
        Assert.That(readResult3.TryRead<byte[]>(1, out var receivedData3), Is.True);
        Assert.That(length3, Is.EqualTo(testData3.Length));
        Assert.That(receivedData3, Is.EqualTo(testData3));
    }

    [Test]
    public async Task ReadAsync_MultipleReadsWithSameDefinition_ShouldSucceed()
    {
        var definition = "[sequence:int][value:double]";
        
        // Send and read multiple messages with the same definition
        for (int i = 1; i <= 5; i++)
        {
            var data = new object[] { i, i * 1.5 };

            var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
            Assert.That(writeResult, Is.True, $"Write {i} should succeed");

            var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
            Assert.That(readResult.Success, Is.True, $"Read {i} should succeed");
            Assert.That(readResult.TryRead<int>(0, out var sequence), Is.True, $"Read sequence {i} should succeed");
            Assert.That(readResult.TryRead<double>(1, out var value), Is.True, $"Read value {i} should succeed");
            Assert.That(sequence, Is.EqualTo(i), $"Sequence {i} should match");
            Assert.That(value, Is.EqualTo(i * 1.5).Within(0.001), $"Value {i} should match");
        }
    }

    [Test]
    public async Task ReadAsync_MultipleReadsWithComplexData_ShouldSucceed()
    {
        // Complex message with multiple variable length fields
        var definition = "[headerLen:int][header:headerLen][payloadLen:int][payload:payloadLen][footer:byte]";
        
        var scenarios = new[]
        {
            ("MSG1"u8.ToArray(), "First payload"u8.ToArray(), (byte)1),
            ("HEADER2"u8.ToArray(), "Second message with longer payload content"u8.ToArray(), (byte)255),
            ("H3"u8.ToArray(), "Third"u8.ToArray(), (byte)128),
            ("VERYLONGHEADERTEXT"u8.ToArray(), Array.Empty<byte>(), (byte)0)
        };

        for (int i = 0; i < scenarios.Length; i++)
        {
            var (header, payload, footer) = scenarios[i];
            var data = new object[] { header.Length, header, payload.Length, payload, footer };

            var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
            Assert.That(writeResult, Is.True, $"Write scenario {i + 1} should succeed");

            var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
            Assert.That(readResult.Success, Is.True, $"Read scenario {i + 1} should succeed");
            
            Assert.That(readResult.TryRead<int>(0, out var headerLen), Is.True);
            Assert.That(readResult.TryRead<byte[]>(1, out var receivedHeader), Is.True);
            Assert.That(readResult.TryRead<int>(2, out var payloadLen), Is.True);
            Assert.That(readResult.TryRead<byte[]>(3, out var receivedPayload), Is.True);
            Assert.That(readResult.TryRead<byte>(4, out var receivedFooter), Is.True);

            Assert.That(headerLen, Is.EqualTo(header.Length), $"Header length {i + 1} should match");
            Assert.That(receivedHeader, Is.EqualTo(header), $"Header {i + 1} should match");
            Assert.That(payloadLen, Is.EqualTo(payload.Length), $"Payload length {i + 1} should match");
            Assert.That(receivedPayload, Is.EqualTo(payload), $"Payload {i + 1} should match");
            Assert.That(receivedFooter, Is.EqualTo(footer), $"Footer {i + 1} should match");
        }
    }

    [Test]
    public async Task WriteAsync_MultipleWritesOnSameStream_ShouldSucceed()
    {
        // Write multiple messages sequentially
        var scenarios = new[]
        {
            ("[id:int]", new object[] { 1001 }),
            ("[flag:bool][value:short]", new object[] { true, (short)-1000 }),
            ("[data1:byte][data2:byte][data3:byte]", new object[] { (byte)10, (byte)20, (byte)30 }),
            ("[number:long]", new object[] { 9223372036854775807L })
        };

        // Write all messages first
        foreach (var (definition, data) in scenarios)
        {
            var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
            Assert.That(writeResult, Is.True, $"Write with definition '{definition}' should succeed");
        }

        // Then read them back in order
        var readResult1 = await _clientParser!.ReadAsync("[id:int]").WithTimeout();
        Assert.That(readResult1.Success, Is.True);
        Assert.That(readResult1.TryRead<int>(0, out var id), Is.True);
        Assert.That(id, Is.EqualTo(1001));

        var readResult2 = await _clientParser!.ReadAsync("[flag:bool][value:short]").WithTimeout();
        Assert.That(readResult2.Success, Is.True);
        Assert.That(readResult2.TryRead<bool>(0, out var flag), Is.True);
        Assert.That(readResult2.TryRead<short>(1, out var value), Is.True);
        Assert.That(flag, Is.True);
        Assert.That(value, Is.EqualTo((short)-1000));

        var readResult3 = await _clientParser!.ReadAsync("[data1:byte][data2:byte][data3:byte]").WithTimeout();
        Assert.That(readResult3.Success, Is.True);
        Assert.That(readResult3.TryRead<byte>(0, out var data1), Is.True);
        Assert.That(readResult3.TryRead<byte>(1, out var data2), Is.True);
        Assert.That(readResult3.TryRead<byte>(2, out var data3), Is.True);
        Assert.That(data1, Is.EqualTo((byte)10));
        Assert.That(data2, Is.EqualTo((byte)20));
        Assert.That(data3, Is.EqualTo((byte)30));

        var readResult4 = await _clientParser!.ReadAsync("[number:long]").WithTimeout();
        Assert.That(readResult4.Success, Is.True);
        Assert.That(readResult4.TryRead<long>(0, out var number), Is.True);
        Assert.That(number, Is.EqualTo(9223372036854775807L));
    }

    [Test]
    public async Task WriteAsync_MultipleWritesWithVariableLength_ShouldSucceed()
    {
        var definition = "[length:int][data:length]";
        
        var testDataSets = new[]
        {
            "First message"u8.ToArray(),
            "Second message with more content"u8.ToArray(),
            "3rd"u8.ToArray(),
            Array.Empty<byte>(),
            new byte[1000] // Large array
        };

        // Fill large array with pattern
        for (int i = 0; i < testDataSets[4].Length; i++)
        {
            testDataSets[4][i] = (byte)(i % 256);
        }

        // Write all messages
        foreach (var testData in testDataSets)
        {
            var data = new object[] { testData.Length, testData };
            var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout(5000);
            Assert.That(writeResult, Is.True, $"Write with data length {testData.Length} should succeed");
        }

        // Read all messages back
        foreach (var expectedData in testDataSets)
        {
            var readResult = await _clientParser!.ReadAsync(definition).WithTimeout(5000);
            Assert.That(readResult.Success, Is.True, $"Read with expected data length {expectedData.Length} should succeed");
            Assert.That(readResult.TryRead<int>(0, out var length), Is.True);
            Assert.That(readResult.TryRead<byte[]>(1, out var receivedData), Is.True);
            Assert.That(length, Is.EqualTo(expectedData.Length), $"Length should match for data with {expectedData.Length} bytes");
            Assert.That(receivedData, Is.EqualTo(expectedData), $"Data should match for array with {expectedData.Length} bytes");
        }
    }

    [Test]
    public async Task WriteAsync_MultipleWritesSameDefinition_ShouldSucceed()
    {
        var definition = "[counter:int][timestamp:long][active:bool]";
        
        // Write multiple messages with the same structure
        for (int i = 1; i <= 10; i++)
        {
            var data = new object[] { i, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), i % 2 == 0 };
            
            var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
            Assert.That(writeResult, Is.True, $"Write {i} should succeed");
        }

        // Read all messages back
        for (int i = 1; i <= 10; i++)
        {
            var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
            Assert.That(readResult.Success, Is.True, $"Read {i} should succeed");
            Assert.That(readResult.TryRead<int>(0, out var counter), Is.True);
            Assert.That(readResult.TryRead<long>(1, out var timestamp), Is.True);
            Assert.That(readResult.TryRead<bool>(2, out var active), Is.True);
            
            Assert.That(counter, Is.EqualTo(i), $"Counter {i} should match");
            Assert.That(timestamp, Is.GreaterThan(0), $"Timestamp {i} should be valid");
            Assert.That(active, Is.EqualTo(i % 2 == 0), $"Active flag {i} should match expected pattern");
        }
    }

    [Test]
    public async Task ReadWriteAsync_InterleavedOperations_ShouldSucceed()
    {
        // Test interleaved read/write operations where both endpoints send data
        var clientToServerDef = "[clientMsg:int][clientFlag:bool]";
        var serverToClientDef = "[serverResponse:long][serverData:byte]";

        // Client writes, server reads
        var clientData1 = new object[] { 42, true };
        var clientWriteResult1 = await _clientParser!.WriteAsync(clientToServerDef, clientData1).WithTimeout();
        Assert.That(clientWriteResult1, Is.True);

        var serverReadResult1 = await _serverParser!.ReadAsync(clientToServerDef).WithTimeout();
        Assert.That(serverReadResult1.Success, Is.True);
        Assert.That(serverReadResult1.TryRead<int>(0, out var clientMsg1), Is.True);
        Assert.That(serverReadResult1.TryRead<bool>(1, out var clientFlag1), Is.True);
        Assert.That(clientMsg1, Is.EqualTo(42));
        Assert.That(clientFlag1, Is.True);

        // Server responds
        var serverData1 = new object[] { 1000L, (byte)255 };
        var serverWriteResult1 = await _serverParser!.WriteAsync(serverToClientDef, serverData1).WithTimeout();
        Assert.That(serverWriteResult1, Is.True);

        var clientReadResult1 = await _clientParser!.ReadAsync(serverToClientDef).WithTimeout();
        Assert.That(clientReadResult1.Success, Is.True);
        Assert.That(clientReadResult1.TryRead<long>(0, out var serverResponse1), Is.True);
        Assert.That(clientReadResult1.TryRead<byte>(1, out var serverDataByte1), Is.True);
        Assert.That(serverResponse1, Is.EqualTo(1000L));
        Assert.That(serverDataByte1, Is.EqualTo((byte)255));

        // Second round of communication
        var clientData2 = new object[] { 84, false };
        var clientWriteResult2 = await _clientParser!.WriteAsync(clientToServerDef, clientData2).WithTimeout();
        Assert.That(clientWriteResult2, Is.True);

        var serverReadResult2 = await _serverParser!.ReadAsync(clientToServerDef).WithTimeout();
        Assert.That(serverReadResult2.Success, Is.True);
        Assert.That(serverReadResult2.TryRead<int>(0, out var clientMsg2), Is.True);
        Assert.That(serverReadResult2.TryRead<bool>(1, out var clientFlag2), Is.True);
        Assert.That(clientMsg2, Is.EqualTo(84));
        Assert.That(clientFlag2, Is.False);

        var serverData2 = new object[] { 2000L, (byte)128 };
        var serverWriteResult2 = await _serverParser!.WriteAsync(serverToClientDef, serverData2).WithTimeout();
        Assert.That(serverWriteResult2, Is.True);

        var clientReadResult2 = await _clientParser!.ReadAsync(serverToClientDef).WithTimeout();
        Assert.That(clientReadResult2.Success, Is.True);
        Assert.That(clientReadResult2.TryRead<long>(0, out var serverResponse2), Is.True);
        Assert.That(clientReadResult2.TryRead<byte>(1, out var serverDataByte2), Is.True);
        Assert.That(serverResponse2, Is.EqualTo(2000L));
        Assert.That(serverDataByte2, Is.EqualTo((byte)128));
    }

    [Test]
    public async Task ReadWriteAsync_MultipleComplexMessages_ShouldMaintainDataIntegrity()
    {
        // Test complex message structures to ensure data integrity across multiple operations
        var definition = "[msgType:byte][headerLen:int][header:headerLen][payloadLen:int][payload:payloadLen][checksum:uint]";
        
        var messages = new[]
        {
            ((byte)1, "AUTH"u8.ToArray(), "username:admin,password:secret"u8.ToArray(), 12345u),
            ((byte)2, "DATA_REQUEST"u8.ToArray(), "table:users,limit:100"u8.ToArray(), 67890u),
            ((byte)3, "HEARTBEAT"u8.ToArray(), Array.Empty<byte>(), 11111u),
            ((byte)4, "RESPONSE"u8.ToArray(), "status:ok,records:50,timestamp:1234567890"u8.ToArray(), 99999u)
        };

        // Send all messages
        foreach (var (msgType, header, payload, checksum) in messages)
        {
            var data = new object[] { msgType, header.Length, header, payload.Length, payload, checksum };
            var writeResult = await _serverParser!.WriteAsync(definition, data).WithTimeout();
            Assert.That(writeResult, Is.True, $"Write message type {msgType} should succeed");
        }

        // Read and verify all messages
        for (int i = 0; i < messages.Length; i++)
        {
            var (expectedMsgType, expectedHeader, expectedPayload, expectedChecksum) = messages[i];
            
            var readResult = await _clientParser!.ReadAsync(definition).WithTimeout();
            Assert.That(readResult.Success, Is.True, $"Read message {i + 1} should succeed");
            
            Assert.That(readResult.TryRead<byte>(0, out var msgType), Is.True);
            Assert.That(readResult.TryRead<int>(1, out var headerLen), Is.True);
            Assert.That(readResult.TryRead<byte[]>(2, out var header), Is.True);
            Assert.That(readResult.TryRead<int>(3, out var payloadLen), Is.True);
            Assert.That(readResult.TryRead<byte[]>(4, out var payload), Is.True);
            Assert.That(readResult.TryRead<uint>(5, out var checksum), Is.True);

            Assert.That(msgType, Is.EqualTo(expectedMsgType), $"Message type {i + 1} should match");
            Assert.That(headerLen, Is.EqualTo(expectedHeader.Length), $"Header length {i + 1} should match");
            Assert.That(header, Is.EqualTo(expectedHeader), $"Header {i + 1} should match");
            Assert.That(payloadLen, Is.EqualTo(expectedPayload.Length), $"Payload length {i + 1} should match");
            Assert.That(payload, Is.EqualTo(expectedPayload), $"Payload {i + 1} should match");
            Assert.That(checksum, Is.EqualTo(expectedChecksum), $"Checksum {i + 1} should match");
        }
    }
}