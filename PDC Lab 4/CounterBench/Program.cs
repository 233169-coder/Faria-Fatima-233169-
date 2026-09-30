using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int NumThreads = 4;
    const int IncrementsPerThread = 1_000_000;

    static long counter = 0;

    static readonly object counterLock = new object();

    static void LockWorker()
    {
        for (int i = 0; i < IncrementsPerThread; i++)
        {
            lock (counterLock)
            {
                counter++;
            }
        }
    }

    static void InterlockedWorker()
    {
        for (int i = 0; i < IncrementsPerThread; i++)
        {
            Interlocked.Increment(ref counter);
        }
    }

    static double RunVersion(string name, ThreadStart worker)
    {
        counter = 0;

        Thread[] threads = new Thread[NumThreads];

        Stopwatch sw = Stopwatch.StartNew();

        for (int t = 0; t < NumThreads; t++)
        {
            threads[t] = new Thread(worker);
            threads[t].Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        sw.Stop();

        long expected = (long)NumThreads * IncrementsPerThread;
        double ms = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine(
            $"[{name}] expected={expected} actual={counter} " +
            $"correct={counter == expected} time={ms:F2} ms"
        );

        return ms;
    }

    static void Main()
    {
        // Warm-up
        RunVersion("warm-up lock", LockWorker);
        RunVersion("warm-up Interlocked", InterlockedWorker);

        // Actual measurements
        RunVersion("lock", LockWorker);
        RunVersion("Interlocked.Increment", InterlockedWorker);
    }
}