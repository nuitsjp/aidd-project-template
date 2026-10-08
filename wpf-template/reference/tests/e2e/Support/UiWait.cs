using System;
using System.Diagnostics;
using System.Threading;

namespace WpfNotesSample.E2eTests.Support;

internal static class UiWait
{
    public static void Until(Func<bool> condition, string message)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            if (elapsed.Elapsed >= TimeSpan.FromSeconds(20)) throw new TimeoutException(message);
            Thread.Sleep(100);
        }
    }
}
