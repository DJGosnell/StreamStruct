using System.Text;

namespace StreamStruct.Tests;

[TestFixture]
public class BidirectionalMemoryStreamTests
{
    [Test]
    public async Task AsyncWriteAndRead_ShouldTransferDataCorrectly()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        var testData = "Hello Async World!"u8.ToArray();

        await bidirectional.Client.WriteAsync(testData).WithTimeout();
        
        var readBuffer = new byte[testData.Length];
        var bytesRead = await bidirectional.Server.ReadAsync(readBuffer).WithTimeout();

        Assert.That(bytesRead, Is.EqualTo(testData.Length));
        Assert.That(readBuffer, Is.EqualTo(testData));
    }

    [Test]
    public async Task SyncWriteAndRead_ShouldTransferDataCorrectly()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        var testData = "Hello Sync World!"u8.ToArray();

        await Task.Run(() => bidirectional.Server.Write(testData, 0, testData.Length)).WithTimeout();
        
        var readBuffer = new byte[testData.Length];
        var bytesRead = await Task.Run(() => bidirectional.Client.Read(readBuffer, 0, readBuffer.Length)).WithTimeout();

        Assert.That(bytesRead, Is.EqualTo(testData.Length));
        Assert.That(readBuffer, Is.EqualTo(testData));
    }

    [Test]
    public async Task MixedAsyncSyncOperations_ShouldWork()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        var testData = "Mixed Operations!"u8.ToArray();

        await bidirectional.Client.WriteAsync(testData).WithTimeout();
        
        var readBuffer = new byte[testData.Length];
        var bytesRead = await Task.Run(() => bidirectional.Server.Read(readBuffer, 0, readBuffer.Length)).WithTimeout();

        Assert.That(bytesRead, Is.EqualTo(testData.Length));
        Assert.That(readBuffer, Is.EqualTo(testData));
    }

    [Test]
    public async Task StreamClosing_ShouldPreventFurtherOperations()
    {
        var bidirectional = new BidirectionalMemoryStream();
        var testData = "Before Close"u8.ToArray();

        await bidirectional.Client.WriteAsync(testData).WithTimeout();
        
        var readBuffer = new byte[testData.Length];
        var bytesRead = await bidirectional.Server.ReadAsync(readBuffer).WithTimeout();
        Assert.That(bytesRead, Is.EqualTo(testData.Length));

        bidirectional.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await bidirectional.Client.WriteAsync("After Close"u8.ToArray()));
        
        var buffer = new byte[10];
        Assert.That(await bidirectional.Server.ReadAsync(buffer), Is.EqualTo(0));
    }

    [Test]
    public async Task LargeDataTransfer_ShouldHandleCorrectly()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        var largeData = new byte[64 * 1024]; // 64KB
        new Random().NextBytes(largeData);

        var writeTask = bidirectional.Client.WriteAsync(largeData);
        
        var readBuffer = new byte[largeData.Length];
        var totalRead = 0;
        
        while (totalRead < largeData.Length)
        {
            var tempBuffer = new byte[largeData.Length - totalRead];
            var bytesRead = await bidirectional.Server.ReadAsync(tempBuffer).WithTimeout();
            
            if (bytesRead == 0) break;
            Array.Copy(tempBuffer, 0, readBuffer, totalRead, bytesRead);
            totalRead += bytesRead;
        }

        await writeTask.WithTimeout();

        Assert.That(totalRead, Is.EqualTo(largeData.Length));
        Assert.That(readBuffer, Is.EqualTo(largeData));
    }

    [Test]
    public async Task MultipleWritesAndReads_ShouldPreserveOrder()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        var messages = new[]
        {
            "Message 1"u8.ToArray(),
            "Message 2"u8.ToArray(),
            "Message 3"u8.ToArray()
        };

        foreach (var message in messages)
        {
            await bidirectional.Client.WriteAsync(message).WithTimeout();
        }

        var allReadData = new List<byte>();
        var totalExpectedBytes = messages.Sum(m => m.Length);
        
        while (allReadData.Count < totalExpectedBytes)
        {
            var buffer = new byte[1024];
            var bytesRead = await bidirectional.Server.ReadAsync(buffer).WithTimeout();
            
            if (bytesRead == 0) break;
            allReadData.AddRange(buffer.Take(bytesRead));
        }

        var expectedData = messages.SelectMany(m => m).ToArray();
        Assert.That(allReadData.Count, Is.EqualTo(expectedData.Length));
        Assert.That(allReadData.ToArray(), Is.EqualTo(expectedData));
    }

    [Test]
    public async Task ConcurrentOperations_ShouldWorkCorrectly()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        var clientToServerData = "Client -> Server"u8.ToArray();
        var serverToClientData = "Server -> Client"u8.ToArray();

        var clientWriteTask = bidirectional.Client.WriteAsync(clientToServerData);
        var serverWriteTask = bidirectional.Server.WriteAsync(serverToClientData);

        var clientReadBuffer = new byte[serverToClientData.Length];
        var serverReadBuffer = new byte[clientToServerData.Length];

        var clientReadTask = bidirectional.Client.ReadAsync(clientReadBuffer);
        var serverReadTask = bidirectional.Server.ReadAsync(serverReadBuffer);

        await Task.WhenAll(clientWriteTask.AsTask(), serverWriteTask.AsTask(), clientReadTask.AsTask(), serverReadTask.AsTask()).WithTimeout();

        Assert.That(serverReadBuffer, Is.EqualTo(clientToServerData));
        Assert.That(clientReadBuffer, Is.EqualTo(serverToClientData));
    }

    [Test]
    public async Task PartialReads_ShouldReconstructDataCorrectly()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        var testData = "This is a longer message for partial reading"u8.ToArray();

        await bidirectional.Client.WriteAsync(testData).WithTimeout();

        var allData = new List<byte>();
        var smallBuffer = new byte[5]; // Read in small chunks
        
        while (allData.Count < testData.Length)
        {
            var bytesRead = await bidirectional.Server.ReadAsync(smallBuffer).WithTimeout();
            if (bytesRead == 0) break;
            
            allData.AddRange(smallBuffer.Take(bytesRead));
        }

        Assert.That(allData.Count, Is.EqualTo(testData.Length));
        Assert.That(allData.ToArray(), Is.EqualTo(testData));
    }

    [Test]
    public void StreamProperties_ShouldReturnCorrectValues()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        Assert.That(bidirectional.Client.CanRead, Is.True);
        Assert.That(bidirectional.Client.CanWrite, Is.True);
        Assert.That(bidirectional.Client.CanSeek, Is.False);
        
        Assert.That(bidirectional.Server.CanRead, Is.True);
        Assert.That(bidirectional.Server.CanWrite, Is.True);
        Assert.That(bidirectional.Server.CanSeek, Is.False);
    }

    [Test]
    public void UnsupportedOperations_ShouldThrowNotSupportedException()
    {
        using var bidirectional = new BidirectionalMemoryStream();

        Assert.Throws<NotSupportedException>(() => _ = bidirectional.Client.Length);
        Assert.Throws<NotSupportedException>(() => _ = bidirectional.Client.Position);
        Assert.Throws<NotSupportedException>(() => bidirectional.Client.Position = 0);
        Assert.Throws<NotSupportedException>(() => bidirectional.Client.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => bidirectional.Client.SetLength(100));
    }

    [Test]
    public async Task FlushAsync_ShouldCompleteSuccessfully()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        await bidirectional.Client.FlushAsync().WithTimeout();
        await bidirectional.Server.FlushAsync().WithTimeout();
        
        // Should not throw any exceptions
        Assert.Pass();
    }

    [Test]
    public void Flush_ShouldCompleteSuccessfully()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        bidirectional.Client.Flush();
        bidirectional.Server.Flush();
        
        // Should not throw any exceptions
        Assert.Pass();
    }

    [Test]
    public async Task ValueTaskWriteAsync_ShouldWorkCorrectly()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        var testData = "ValueTask Write Test"u8.ToArray();

        await bidirectional.Client.WriteAsync(testData.AsMemory()).WithTimeout();
        
        var readBuffer = new byte[testData.Length];
        var bytesRead = await bidirectional.Server.ReadAsync(readBuffer).WithTimeout();

        Assert.That(bytesRead, Is.EqualTo(testData.Length));
        Assert.That(readBuffer, Is.EqualTo(testData));
    }

    [Test]
    public async Task ValueTaskReadAsync_ShouldWorkCorrectly()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        var testData = "ValueTask Read Test"u8.ToArray();

        await bidirectional.Client.WriteAsync(testData).WithTimeout();
        
        var readBuffer = new byte[testData.Length];
        var bytesRead = await bidirectional.Server.ReadAsync(readBuffer.AsMemory()).WithTimeout();

        Assert.That(bytesRead, Is.EqualTo(testData.Length));
        Assert.That(readBuffer, Is.EqualTo(testData));
    }

    [Test]
    public async Task DisposeAsync_ShouldPreventFurtherOperations()
    {
        var bidirectional = new BidirectionalMemoryStream();
        var testData = "Before Async Dispose"u8.ToArray();

        await bidirectional.Client.WriteAsync(testData).WithTimeout();
        
        var readBuffer = new byte[testData.Length];
        var bytesRead = await bidirectional.Server.ReadAsync(readBuffer).WithTimeout();
        Assert.That(bytesRead, Is.EqualTo(testData.Length));

        await bidirectional.Client.DisposeAsync().WithTimeout();
        await bidirectional.Server.DisposeAsync().WithTimeout();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await bidirectional.Client.WriteAsync("After Dispose"u8.ToArray()).WithTimeout());
    }

    [Test]
    public async Task EmptyReads_ShouldReturnZero()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        // Dispose to close the channels
        bidirectional.Dispose();
        
        var buffer = new byte[10];
        var bytesRead = await bidirectional.Server.ReadAsync(buffer).WithTimeout();
        
        Assert.That(bytesRead, Is.EqualTo(0));
    }

    [Test]
    public async Task ZeroLengthWrite_ShouldNotAffectStream()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        var emptyData = Array.Empty<byte>();

        await bidirectional.Client.WriteAsync(emptyData);
        
        var testData = "After empty write"u8.ToArray();
        await bidirectional.Client.WriteAsync(testData).WithTimeout();
        
        var readBuffer = new byte[testData.Length];
        var bytesRead = await bidirectional.Server.ReadAsync(readBuffer).WithTimeout();

        Assert.That(bytesRead, Is.EqualTo(testData.Length));
        Assert.That(readBuffer, Is.EqualTo(testData));
    }
    
    [Test]
    public void Close_ShouldPreventNewOperations()
    {
        var bidirectional = new BidirectionalMemoryStream();
        var testData = "Test data"u8.ToArray();

        bidirectional.Close();

        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await bidirectional.Client.WriteAsync(testData));
        
        var buffer = new byte[10];
        Assert.That(async () => await bidirectional.Server.ReadAsync(buffer), 
            Throws.Nothing);
    }

    [Test]
    public void DoubleClose_ShouldNotThrow()
    {
        var bidirectional = new BidirectionalMemoryStream();

        bidirectional.Close();
        Assert.DoesNotThrow(() => bidirectional.Close());
    }

    [Test]
    public void DoubleDispose_ShouldNotThrow()
    {
        var bidirectional = new BidirectionalMemoryStream();

        bidirectional.Dispose();
        Assert.DoesNotThrow(() => bidirectional.Dispose());
    }

    [Test]
    public void WriteAfterDispose_ShouldThrowObjectDisposedException()
    {
        var bidirectional = new BidirectionalMemoryStream();
        bidirectional.Dispose();

        var testData = "Test"u8.ToArray();
        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await bidirectional.Client.WriteAsync(testData));
        
        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await bidirectional.Server.WriteAsync(testData));
    }

    [Test]
    public async Task ReadAfterDispose_ShouldReturnZero()
    {
        var bidirectional = new BidirectionalMemoryStream();
        bidirectional.Dispose();

        var buffer = new byte[10];
        var bytesRead = await bidirectional.Client.ReadAsync(buffer);
        Assert.That(bytesRead, Is.EqualTo(0));

        bytesRead = await bidirectional.Server.ReadAsync(buffer);
        Assert.That(bytesRead, Is.EqualTo(0));
    }

    [Test]
    public async Task BidirectionalCommunication_ShouldWork()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        var clientMessage = "Hello from client"u8.ToArray();
        var serverMessage = "Hello from server"u8.ToArray();

        // Client sends to server
        await bidirectional.Client.WriteAsync(clientMessage);
        
        // Server sends to client
        await bidirectional.Server.WriteAsync(serverMessage);

        // Server reads client message
        var serverBuffer = new byte[clientMessage.Length];
        var bytesRead = await bidirectional.Server.ReadAsync(serverBuffer);
        Assert.That(bytesRead, Is.EqualTo(clientMessage.Length));
        Assert.That(serverBuffer, Is.EqualTo(clientMessage));

        // Client reads server message
        var clientBuffer = new byte[serverMessage.Length];
        bytesRead = await bidirectional.Client.ReadAsync(clientBuffer);
        Assert.That(bytesRead, Is.EqualTo(serverMessage.Length));
        Assert.That(clientBuffer, Is.EqualTo(serverMessage));
    }

    [Test]
    public async Task WriteWithOffsetAndCount_ShouldWorkCorrectly()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        var largeBuffer = "PREFIX-Hello World-SUFFIX"u8.ToArray();
        var expectedData = "Hello World"u8.ToArray();
        
        // Write only the middle part
        await bidirectional.Client.WriteAsync(largeBuffer, 7, expectedData.Length);
        
        var readBuffer = new byte[expectedData.Length];
        var bytesRead = await bidirectional.Server.ReadAsync(readBuffer);
        
        Assert.That(bytesRead, Is.EqualTo(expectedData.Length));
        Assert.That(readBuffer, Is.EqualTo(expectedData));
    }

    [Test]
    public async Task ReadWithOffsetAndCount_ShouldWorkCorrectly()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        var testData = "Hello World"u8.ToArray();
        await bidirectional.Client.WriteAsync(testData);
        
        var largeBuffer = new byte[30];
        var bytesRead = await bidirectional.Server.ReadAsync(largeBuffer, 5, testData.Length);
        
        Assert.That(bytesRead, Is.EqualTo(testData.Length));
        
        // Check that data was written at offset 5
        var writtenData = new byte[testData.Length];
        Array.Copy(largeBuffer, 5, writtenData, 0, testData.Length);
        Assert.That(writtenData, Is.EqualTo(testData));
        
        // Check that data before offset 5 is still zero
        Assert.That(largeBuffer.Take(5).All(b => b == 0), Is.True);
    }

    [Test]
    public async Task VeryLargeData_ShouldTransferCorrectly()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        var largeData = new byte[1024 * 1024]; // 1MB
        new Random(42).NextBytes(largeData);
        
        await bidirectional.Client.WriteAsync(largeData);
        
        var readBuffer = new byte[largeData.Length];
        var totalRead = 0;
        
        while (totalRead < largeData.Length)
        {
            var remainingBytes = largeData.Length - totalRead;
            var chunkSize = Math.Min(8192, remainingBytes); // Read in 8KB chunks
            var tempBuffer = new byte[chunkSize];
            
            var bytesRead = await bidirectional.Server.ReadAsync(tempBuffer).WithTimeout();
            if (bytesRead == 0) break;
            
            Array.Copy(tempBuffer, 0, readBuffer, totalRead, bytesRead);
            totalRead += bytesRead;
        }
        
        Assert.That(totalRead, Is.EqualTo(largeData.Length));
        Assert.That(readBuffer, Is.EqualTo(largeData));
    }

    [Test]
    public async Task MultipleSmallWrites_ShouldAccumulate()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        var words = new[] { "Hello", " ", "World", "!" };
        
        foreach (var word in words)
        {
            await bidirectional.Client.WriteAsync(Encoding.UTF8.GetBytes(word));
        }
        
        var expectedText = string.Concat(words);
        var buffer = new byte[expectedText.Length];
        var totalRead = 0;
        
        while (totalRead < expectedText.Length)
        {
            var bytesRead = await bidirectional.Server.ReadAsync(buffer, totalRead, buffer.Length - totalRead);
            if (bytesRead == 0) break;
            totalRead += bytesRead;
        }
        
        var actualText = Encoding.UTF8.GetString(buffer, 0, totalRead);
        Assert.That(actualText, Is.EqualTo(expectedText));
    }

    [Test]
    public async Task CancellationToken_ShouldCancelOperations()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        using var cts = new CancellationTokenSource();
        
        cts.Cancel();
        
        var testData = "Test"u8.ToArray();
        var buffer = new byte[10];
        
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await bidirectional.Client.WriteAsync(testData, cts.Token));
        
        Assert.That(await bidirectional.Server.ReadAsync(buffer, cts.Token), Is.EqualTo(0));
    }

    [Test]
    public void CancellationDuringRead_ShouldThrowOperationCanceledException()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        using var cts = new CancellationTokenSource();
        
        var buffer = new byte[10];
        
        // Start a read operation and cancel it quickly
        var readTask = bidirectional.Server.ReadAsync(buffer, cts.Token);
        cts.CancelAfter(50);
        
        Assert.ThrowsAsync<OperationCanceledException>(async () => await readTask);
    }

    [Test]
    public void StreamProperties_AfterDispose_ShouldReturnFalse()
    {
        var bidirectional = new BidirectionalMemoryStream();
        
        Assert.That(bidirectional.Client.CanRead, Is.True);
        Assert.That(bidirectional.Client.CanWrite, Is.True);
        Assert.That(bidirectional.Server.CanRead, Is.True);
        Assert.That(bidirectional.Server.CanWrite, Is.True);
        
        bidirectional.Dispose();
        
        Assert.That(bidirectional.Client.CanRead, Is.False);
        Assert.That(bidirectional.Client.CanWrite, Is.False);
        Assert.That(bidirectional.Server.CanRead, Is.False);
        Assert.That(bidirectional.Server.CanWrite, Is.False);
    }

    [Test]
    public async Task PartialDataInBuffer_ShouldBeReadable()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        var testData = "Hello World"u8.ToArray();
        await bidirectional.Client.WriteAsync(testData);
        
        // Read first part
        var buffer1 = new byte[5];
        var bytesRead1 = await bidirectional.Server.ReadAsync(buffer1);
        Assert.That(bytesRead1, Is.EqualTo(5));
        Assert.That(buffer1, Is.EqualTo("Hello"u8.ToArray()));
        
        // Read second part
        var buffer2 = new byte[6];
        var bytesRead2 = await bidirectional.Server.ReadAsync(buffer2);
        Assert.That(bytesRead2, Is.EqualTo(6));
        Assert.That(buffer2, Is.EqualTo(" World"u8.ToArray()));
    }

    [Test]
    public async Task ReadBufferLargerThanData_ShouldReturnExactDataSize()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        var testData = "Hello"u8.ToArray();
        await bidirectional.Client.WriteAsync(testData);
        
        var largeBuffer = new byte[100];
        var bytesRead = await bidirectional.Server.ReadAsync(largeBuffer);
        
        Assert.That(bytesRead, Is.EqualTo(testData.Length));
        
        var actualData = new byte[bytesRead];
        Array.Copy(largeBuffer, actualData, bytesRead);
        Assert.That(actualData, Is.EqualTo(testData));
    }

    [Test]
    public async Task ReadWithZeroCount_ShouldReturnZero()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        var testData = "Hello"u8.ToArray();
        await bidirectional.Client.WriteAsync(testData);
        
        var buffer = new byte[10];
        var bytesRead = await bidirectional.Server.ReadAsync(buffer, 0, 0);
        
        Assert.That(bytesRead, Is.EqualTo(0));
    }

    [Test]
    public async Task WriteWithZeroCount_ShouldNotAffectStream()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        var buffer = new byte[10];
        await bidirectional.Client.WriteAsync(buffer, 0, 0);
        
        var testData = "Hello"u8.ToArray();
        await bidirectional.Client.WriteAsync(testData);
        
        var readBuffer = new byte[testData.Length];
        var bytesRead = await bidirectional.Server.ReadAsync(readBuffer);
        
        Assert.That(bytesRead, Is.EqualTo(testData.Length));
        Assert.That(readBuffer, Is.EqualTo(testData));
    }

    [Test]
    public async Task SequentialWritesAndReads_ShouldMaintainOrder()
    {
        using var bidirectional = new BidirectionalMemoryStream();
        
        var messages = Enumerable.Range(1, 10)
            .Select(i => $"Message {i}")
            .Select(s => Encoding.UTF8.GetBytes(s))
            .ToArray();
        
        // Write all messages
        foreach (var message in messages)
        {
            await bidirectional.Client.WriteAsync(message);
        }
        
        // Read all messages back
        var receivedMessages = new List<byte[]>();
        for (int i = 0; i < messages.Length; i++)
        {
            var buffer = new byte[messages[i].Length];
            var bytesRead = await bidirectional.Server.ReadAsync(buffer);
            Assert.That(bytesRead, Is.EqualTo(messages[i].Length));
            receivedMessages.Add(buffer);
        }
        
        // Verify order and content
        for (int i = 0; i < messages.Length; i++)
        {
            Assert.That(receivedMessages[i], Is.EqualTo(messages[i]));
        }
    }

    [Test]
    public async Task CloseMethodTest_ShouldNotDisposeObject()
    {
        var bidirectional = new BidirectionalMemoryStream();
        var testData = "Test"u8.ToArray();
        
        // Write data before closing
        await bidirectional.Client.WriteAsync(testData);
        
        // Close the stream
        bidirectional.Close();
        
        // Should be able to read remaining data
        var buffer = new byte[testData.Length];
        var bytesRead = await bidirectional.Server.ReadAsync(buffer);
        Assert.That(bytesRead, Is.EqualTo(0)); // Should return 0 after close
        
        // But writes should throw
        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await bidirectional.Client.WriteAsync(testData));
    }
}