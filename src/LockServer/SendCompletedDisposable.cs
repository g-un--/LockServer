using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LockServer
{
    internal sealed class SendCompletedDisposable<T> : IAsyncDisposable
    {
        private readonly ChannelWriter<(T, TaskCompletionSource<IAsyncDisposable>)> _writer;
        private readonly CancellationToken _ct;
        private readonly (T key, TaskCompletionSource<IAsyncDisposable>) _request;
        private int _disposed = 0; 

        public SendCompletedDisposable(
            ChannelWriter<(T, TaskCompletionSource<IAsyncDisposable>)> writer,
            CancellationToken ct,
            (T key, TaskCompletionSource<IAsyncDisposable>) request)
        {
            _disposed = 0;
            _writer = writer;
            _ct = ct;
            _request = request;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            try
            {
                while (!_ct.IsCancellationRequested && await _writer.WaitToWriteAsync(_ct))
                    if (_writer.TryWrite(_request))
                        return;
            }
            catch (OperationCanceledException) { }
        }
    }
}
