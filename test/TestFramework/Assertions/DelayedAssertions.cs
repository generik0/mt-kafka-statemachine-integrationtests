using System.Diagnostics;
using FluentAssertions;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework.Assertions;

internal static class DelayedAssertions
{
    internal static async Task AssertEvent(Func<Task<bool>> func, TimeSpan maxDelay, string because, bool increaseDebuggerTimeBy10 = true, int threadSleepMs = 100)
    {
        var waitTimes = CalculateWaitTimes(maxDelay, increaseDebuggerTimeBy10, threadSleepMs);

        for (var i = 0; i < waitTimes; i++)
        {
            if (await func.Invoke())
            {
                Thread.Sleep(threadSleepMs);
                return;
            }
            Thread.Sleep(threadSleepMs);
        }

        (await func.Invoke()).Should().BeTrue(because);
    }

    internal static void AssertEvent(Func<bool> func, TimeSpan maxDelay, string because, bool increaseDebuggerTimeBy10 = true, int threadSleepMs = 100)
    {
        var waitTimes = CalculateWaitTimes(maxDelay, increaseDebuggerTimeBy10, threadSleepMs);

        for (var i = 0; i < waitTimes; i++)
        {
            if (func.Invoke())
            {
                Thread.Sleep(threadSleepMs);
                return;
            }
            Thread.Sleep(threadSleepMs);
        }

        func.Invoke().Should().BeTrue(because);
    }

    private static int CalculateWaitTimes(TimeSpan maxDelay, bool increaseDebuggerTimeBy10, int threadSleepMs)
    {
        var waitTimes = (int)(maxDelay.TotalMilliseconds / threadSleepMs) + 1;
        if (increaseDebuggerTimeBy10 && Debugger.IsAttached)
        {
            waitTimes *= 10;
        }

        return waitTimes;
    }
}
