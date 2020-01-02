using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LockServer.Tests
{
    [MemoryDiagnoser]
    public class Program
    {
        public static void Main()
        {
            BenchmarkRunner.Run<Program>(DefaultConfig.Instance.With(ConfigOptions.DisableOptimizationsValidator));
        }

        private static CancellationTokenSource Cts;
        private static LockServer<int> LockServer;
        private static Task ServerTask;

        [GlobalSetup]
        public void Setup()
        {
            Cts = new CancellationTokenSource();
            LockServer = new LockServer<int>(Cts.Token);
            ServerTask = LockServer.Start();
        }

        [GlobalCleanup]
        public async Task Clean()
        {
            Cts.Cancel();
            await ServerTask;
        }

        [Benchmark]
        public async Task GetDistinctLocks()
        {
            var tasks = Enumerable.Range(1, 1_00_000).Select(i => Task.Run(async () =>
            {
                await using var _ = await LockServer.GetLock(i);
            }));
            await Task.WhenAll(tasks.ToArray());
        }

        [Benchmark]
        public async Task GetSameLock()
        {
            var tasks = Enumerable.Range(1, 1_00_000).Select(_ => Task.Run(async () =>
            {
                await using var _ = await LockServer.GetLock(0);
            }));
            await Task.WhenAll(tasks.ToArray());
        }

        [Benchmark]
        public async Task Get10RandomLocks()
        {
            var random = new Random();
            var tasks = Enumerable.Range(1, 1_00_000).Select(_ => Task.Run(async () =>
            {
                var key = random.Next(1, 10); 
                await using var _ = await LockServer.GetLock(0);
            }));
            await Task.WhenAll(tasks.ToArray());
        }

        [Benchmark]
        public async Task Get100RandomLocks()
        {
            var random = new Random();
            var tasks = Enumerable.Range(1, 1_00_000).Select(_ => Task.Run(async () =>
            {
                var key = random.Next(1, 100);
                await using var _ = await LockServer.GetLock(0);
            }));
            await Task.WhenAll(tasks.ToArray());
        }

        [Benchmark]
        public async Task Get1000RandomLocks()
        {
            var random = new Random();
            var tasks = Enumerable.Range(1, 1_00_000).Select(_ => Task.Run(async () =>
            {
                var key = random.Next(1, 1000);
                await using var _ = await LockServer.GetLock(0);
            }));
            await Task.WhenAll(tasks.ToArray());
        }

        [Benchmark]
        public async Task SemaphoreSlimForComparison()
        {
            var semaphoreSlim = new SemaphoreSlim(1, 1);
            var tasks = Enumerable.Range(1, 1_00_000).Select(i => Task.Run(async () =>
            {
                await semaphoreSlim.WaitAsync();
                semaphoreSlim.Release();
            }));
            await Task.WhenAll(tasks.ToArray());
        }
    }
}
