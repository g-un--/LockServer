using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LockServer
{
    public sealed class LockServer<T>
    {
        private readonly Channel<(T, TaskCompletionSource<IAsyncDisposable>)> _requests;
        private readonly CancellationToken _ct;
        private readonly Queue<Queue<TaskCompletionSource<IAsyncDisposable>>> _pool;

        public LockServer(CancellationToken ct)
        {
            _ct = ct;
            _requests = Channel.CreateUnbounded<(T, TaskCompletionSource<IAsyncDisposable>)>();
            _pool = new Queue<Queue<TaskCompletionSource<IAsyncDisposable>>>();
        }

        public async Task Start()
        {
            try
            {
                await Task.Run(async () =>
                {
                    var store = new Dictionary<T, Queue<TaskCompletionSource<IAsyncDisposable>>>();

                    while (!_ct.IsCancellationRequested)
                    {
                        var (key, tcs) = await _requests.Reader.ReadAsync(_ct);
                        bool hasPendingRequest = store.TryGetValue(key, out var pendingRequests);
                        bool isNew = !tcs.Task.IsCompleted;

                        if (isNew && hasPendingRequest)
                        {
                            pendingRequests.Enqueue(tcs);
                        }
                        else if (isNew && !hasPendingRequest)
                        {
                            var queue = _pool.Count > 0 ? _pool.Dequeue() : new Queue<TaskCompletionSource<IAsyncDisposable>>();
                            store.Add(key, queue);
                            tcs.TrySetResult(new SendCompletedDisposable<T>(_requests.Writer, _ct, (key, tcs)));
                        }
                        else if (!isNew && hasPendingRequest)
                        {
                            if (pendingRequests.Count == 0)
                            {
                                _pool.Enqueue(pendingRequests);
                                store.Remove(key);
                            }
                            else
                            {
                                var request = pendingRequests.Dequeue();
                                request.TrySetResult(new SendCompletedDisposable<T>(_requests.Writer, _ct, (key, tcs)));
                            }
                        }
                    }
                });
            }
            catch (OperationCanceledException) { }
        }

        public async Task<IAsyncDisposable> GetLock(T key)
        {
            _ct.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<IAsyncDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
            await _requests.Writer.WriteAsync((key, tcs), _ct);
            return await tcs.Task;
        }
    }
}
