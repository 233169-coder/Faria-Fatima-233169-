using System;
using System.Threading;

class Program
{
    const int NumThreads = 4;
    const int IncrementsPerThread = 1_000_000;

    static long counter = 0;

    // Shared lock object
    static readonly object counterLock = new object();

    static void Worker()
    {
        for (int i = 0; i < IncrementsPerThread; i++)
        {
            lock (counterLock)
            {
                counter++;
            }
        }
    }

    static void Main()
    {
        Thread[] threads = new Thread[NumThreads];

        for (int t = 0; t < NumThreads; t++)
        {
            threads[t] = new Thread(Worker);
            threads[t].Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        long expected = (long)NumThreads * IncrementsPerThread;

        Console.WriteLine($"Expected: {expected:N0}");
        Console.WriteLine($"Actual:   {counter:N0}");
        Console.WriteLine($"Lost:     {expected - counter:N0}");
    }
}