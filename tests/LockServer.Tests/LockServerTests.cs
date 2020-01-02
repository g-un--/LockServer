using Shouldly;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LockServer.Tests
{
    public class LockServerTests
    {
        [Fact]
        public async Task ShouldWaitForLockOnSameKey()
        {
            var cts = new CancellationTokenSource();
            var server = new LockServer<string>(cts.Token);
            var serverTask = server.Start();

            var concurrentCount = 0;
            async Task test() 
            {
                await using var _ = await server.GetLock("test");
                await Task.Delay(50);
                var newValue = Interlocked.Increment(ref concurrentCount);
                newValue.ShouldBe(1);
                await Task.Delay(50);
                newValue = Interlocked.Decrement(ref concurrentCount);
                newValue.ShouldBe(0);
            };

            await Task.WhenAll(test(), test());

            concurrentCount.ShouldBe(0);
            cts.Cancel();
            await serverTask;
        }

        [Fact]
        public async Task ShouldNotWaitForLockOnDifferentKey()
        {
            var cts = new CancellationTokenSource();
            var server = new LockServer<string>(cts.Token);
            var serverTask = server.Start();

            var concurrentCount = 0;
            async Task test(string key)
            {
                await using var _ = await server.GetLock(key);
                Interlocked.Increment(ref concurrentCount);
                await Task.Delay(50);
                Thread.MemoryBarrier();
                concurrentCount.ShouldBe(2);
                Interlocked.Decrement(ref concurrentCount);
            };

            await Task.WhenAll(test("test1"), test("test2"));

            concurrentCount.ShouldBe(0);
            cts.Cancel();
            await serverTask;
        }
    }
}
