using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int ItemsPerProducer = 50_000;
    const int StopSignal = -1;

    static int bufferSize;
    static int producerCount;
    static int consumerCount;

    static int[] buffer = null!;

    static int inPos = 0;
    static int outPos = 0;

    static readonly object gate = new object();

    static SemaphoreSlim emptySlots = null!;
    static SemaphoreSlim fullSlots = null!;

    static int[] seen = null!;

    static void Producer(object? obj)
    {
        int id = (int)obj!;

        for (int k = 0; k < ItemsPerProducer; k++)
        {
            int item = id * ItemsPerProducer + k;

            // Wait for an empty buffer position
            emptySlots.Wait();

            lock (gate)
            {
                buffer[inPos] = item;
                inPos = (inPos + 1) % bufferSize;
            }

            // One more item is available
            fullSlots.Release();
        }
    }

    static void Consumer()
    {
        while (true)
        {
            // Wait until an item is available
            fullSlots.Wait();

            int item;

            lock (gate)
            {
                item = buffer[outPos];
                outPos = (outPos + 1) % bufferSize;
            }

            // Make one buffer position available
            emptySlots.Release();

            // Stop signal received
            if (item == StopSignal)
                break;

            // Record the item
            Interlocked.Increment(ref seen[item]);
        }
    }

    static void Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine(
                "Usage: dotnet run --no-build -c Release -- <producers> <consumers> <capacity>"
            );
            return;
        }

        producerCount = int.Parse(args[0]);
        consumerCount = int.Parse(args[1]);
        bufferSize = int.Parse(args[2]);

        int totalItems = producerCount * ItemsPerProducer;

        buffer = new int[bufferSize];

        seen = new int[totalItems];

        // Semaphore for empty buffer positions
        emptySlots = new SemaphoreSlim(bufferSize, bufferSize);

        // Semaphore for available items
        fullSlots = new SemaphoreSlim(0, bufferSize);

        Thread[] producers = new Thread[producerCount];
        Thread[] consumers = new Thread[consumerCount];

        Stopwatch sw = Stopwatch.StartNew();

        // Start consumers
        for (int i = 0; i < consumerCount; i++)
        {
            consumers[i] = new Thread(Consumer);
            consumers[i].Start();
        }

        // Start producers
        for (int i = 0; i < producerCount; i++)
        {
            producers[i] = new Thread(Producer);
            producers[i].Start(i);
        }

        // Wait for all producers to finish
        foreach (Thread producer in producers)
            producer.Join();

        // Send one stop signal to each consumer
        for (int i = 0; i < consumerCount; i++)
        {
            emptySlots.Wait();

            lock (gate)
            {
                buffer[inPos] = StopSignal;
                inPos = (inPos + 1) % bufferSize;
            }

            fullSlots.Release();
        }

        // Wait for all consumers
        foreach (Thread consumer in consumers)
            consumer.Join();

        sw.Stop();

        int lost = 0;
        int duplicated = 0;

        for (int i = 0; i < totalItems; i++)
        {
            if (seen[i] == 0)
                lost++;

            if (seen[i] > 1)
                duplicated += seen[i] - 1;
        }

        Console.WriteLine(
            $"[semaphore] producers={producerCount} " +
            $"consumers={consumerCount} buffer={bufferSize}"
        );

        Console.WriteLine(
            $"items={totalItems} " +
            $"lost={lost} " +
            $"duplicated={duplicated} " +
            $"time={sw.Elapsed.TotalMilliseconds:F2} ms"
        );
    }
}