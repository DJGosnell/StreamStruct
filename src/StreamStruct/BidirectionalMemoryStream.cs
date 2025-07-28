using System.Threading.Channels;

namespace StreamStruct;

internal class BidirectionalMemoryStream : IDisposable
{
    private readonly Channel<byte[]> _clientToServerChannel;
    private readonly Channel<byte[]> _serverToClientChannel;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _closed;

    public ConnectedStream Client { get; }
    public ConnectedStream Server { get; }

    public BidirectionalMemoryStream()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        
        _clientToServerChannel = Channel.CreateUnbounded<byte[]>();
        _serverToClientChannel = Channel.CreateUnbounded<byte[]>();

        Client = new ConnectedStream(
            _clientToServerChannel.Writer,
            _serverToClientChannel.Reader,
            _cancellationTokenSource,
            "Client");

        Server = new ConnectedStream(
            _serverToClientChannel.Writer,
            _clientToServerChannel.Reader,
            _cancellationTokenSource,
            "Server");
    }

    public void Close()
    {
        if (_closed)
            return;
            
        _closed = true;
        
        Client.Dispose();
        Server.Dispose();
        
        if (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
        
        try
        {
            _clientToServerChannel.Writer.Complete();
        }
        catch (InvalidOperationException)
        {
            // Already completed
        }
        
        try
        {
            _serverToClientChannel.Writer.Complete();
        }
        catch (InvalidOperationException)
        {
            // Already completed
        }
    }

    public void Dispose()
    {
        if (!_closed)
            Close();
    }
}

internal class ConnectedStream : Stream
{
    private readonly ChannelWriter<byte[]> _writer;
    private readonly ChannelReader<byte[]> _reader;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly string _name;
    private readonly Queue<byte> _readBuffer = new();
    private bool _disposed;

    internal ConnectedStream(
        ChannelWriter<byte[]> writer,
        ChannelReader<byte[]> reader,
        CancellationTokenSource cancellationTokenSource,
        string name)
    {
        _writer = writer;
        _reader = reader;
        _cancellationTokenSource = cancellationTokenSource;
        _name = name;
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => !_disposed;
    public override long Length => throw new NotSupportedException();
    public override long Position 
    { 
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_disposed)
            return 0;

        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token).Token;

        int totalRead = 0;

        // First, read from any buffered data
        while (_readBuffer.Count > 0 && totalRead < count)
        {
            buffer[offset + totalRead] = _readBuffer.Dequeue();
            totalRead++;
        }

        // If we haven't read anything yet, try to get one item from the channel
        if (totalRead == 0)
        {
            try
            {
                if (await _reader.WaitToReadAsync(combinedToken))
                {
                    if (_reader.TryRead(out var data))
                    {
                        foreach (var b in data)
                        {
                            _readBuffer.Enqueue(b);
                        }

                        // Read from the buffer until we have enough data or the buffer is empty
                        while (_readBuffer.Count > 0 && totalRead < count)
                        {
                            buffer[offset + totalRead] = _readBuffer.Dequeue();
                            totalRead++;
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Channel is closed
            }
            catch (InvalidOperationException)
            {
                // Channel is closed
            }
        }

        return totalRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var tempBuffer = new byte[buffer.Length];
        var bytesRead = await ReadAsync(tempBuffer, 0, buffer.Length, cancellationToken);
        tempBuffer.AsSpan(0, bytesRead).CopyTo(buffer.Span);
        return bytesRead;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ConnectedStream));

        if (count == 0)
            return; // Don't write empty arrays to the channel

        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token).Token;

        var data = new byte[count];
        Array.Copy(buffer, offset, data, 0, count);

        try
        {
            await _writer.WriteAsync(data, combinedToken);
        }
        catch (TaskCanceledException e) when (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConnectedStream), e);
        }
        catch (TaskCanceledException e)
        {
            throw new OperationCanceledException(nameof(ConnectedStream), e);
        }
        catch (InvalidOperationException e)
        {
            throw new ObjectDisposedException(nameof(ConnectedStream), e);
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await WriteAsync(buffer.ToArray(), 0, buffer.Length, cancellationToken);
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
    }

    public override void Flush()
    {
        // No-op for memory streams
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
            _cancellationTokenSource.Cancel();
            _writer.Complete();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cancellationTokenSource.Cancel();
            _writer.Complete();
        }
        await base.DisposeAsync();
    }
}