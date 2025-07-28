namespace StreamStruct;

public class EchoMemoryStream : Stream
{
    private readonly Queue<byte> _buffer = new();
    private bool _disposed;

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => !_disposed;
    public override long Length => _buffer.Count;
    public override long Position 
    { 
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EchoMemoryStream));

        int bytesRead = 0;
        while (_buffer.Count > 0 && bytesRead < count)
        {
            buffer[offset + bytesRead] = _buffer.Dequeue();
            bytesRead++;
        }
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await Task.FromResult(Read(buffer, offset, count));
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var tempBuffer = new byte[buffer.Length];
        var bytesRead = Read(tempBuffer, 0, buffer.Length);
        tempBuffer.AsSpan(0, bytesRead).CopyTo(buffer.Span);
        return ValueTask.FromResult(bytesRead);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EchoMemoryStream));

        for (int i = 0; i < count; i++)
        {
            _buffer.Enqueue(buffer[offset + i]);
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer, offset, count);
        await Task.CompletedTask;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.ToArray(), 0, buffer.Length);
        await Task.CompletedTask;
    }

    public override void Flush()
    {
        // No-op for memory streams
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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
            _buffer.Clear();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _buffer.Clear();
        }
        await base.DisposeAsync();
    }
}