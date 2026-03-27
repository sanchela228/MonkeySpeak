using System.Collections.Concurrent;

namespace Core.Application.Media;

public class ProducerConsumerStream : Stream
{
    private readonly ConcurrentQueue<byte[]> _chunks = new();
    private readonly AutoResetEvent _dataReady = new(false);
    private volatile bool _completed;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        while (true)
        {
            if (_chunks.TryDequeue(out var chunk))
            {
                var toCopy = Math.Min(count, chunk.Length);
                Buffer.BlockCopy(chunk, 0, buffer, offset, toCopy);
                
                if (toCopy < chunk.Length)
                {
                    var rest = new byte[chunk.Length - toCopy];
                    Buffer.BlockCopy(chunk, toCopy, rest, 0, rest.Length);
                    _chunks.Enqueue(rest);
                    _dataReady.Set();
                }
                return toCopy;
            }

            if (_completed) return 0;

            _dataReady.WaitOne();
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var copy = new byte[count];
        Buffer.BlockCopy(buffer, offset, copy, 0, count);
        _chunks.Enqueue(copy);
        _dataReady.Set();
    }

    public void Clear()
    {
        while (_chunks.TryDequeue(out _))
        {
        }
    }

    public void Complete()
    {
        _completed = true;
        _dataReady.Set();
    }

    protected override void Dispose(bool disposing)
    {
        _completed = true;
        _dataReady.Set();
        base.Dispose(disposing);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}