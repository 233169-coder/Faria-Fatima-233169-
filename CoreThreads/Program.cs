using System;
using System.Threading;

class Program
{
    static void Worker(object? arg)
    {
        int threadIndex = (int)arg!;

        int processorId = Thread.GetCurrentProcessorId();

        Console.WriteLine(
            $"Thread {threadIndex}: running on logical processor {processorId}"
        );
    }

    static void Main()
    {

        int numCores = Environment.ProcessorCount;

        Console.WriteLine($"Detected logical cores: {numCores}");

        Thread[] threads = new Thread[numCores];

        for (int i = 0; i < numCores; i++)
        {
            int threadIndex = i;

            threads[i] = new Thread(() => Worker(threadIndex));

            threads[i].Start();
        }

        for (int i = 0; i < numCores; i++)
        {
            threads[i].Join();
        }

        Console.WriteLine();
        Console.WriteLine($"Total threads created: {numCores}");
        Console.WriteLine($"All {numCores} threads completed.");
    }
}