using System;
using System.Threading;

class Program
{
    static long[] data = new long[10_000_000];
    static long[] partialSums;
    static int numWorkers;

    static void SumSlice(int index)
    {
        int sliceSize = data.Length / numWorkers;

        int start = index * sliceSize;

        int end;

        if (index == numWorkers - 1)
        {
            end = data.Length;
        }
        else
        {
            end = start + sliceSize;
        }

        long sum = 0;

        for (int i = start; i < end; i++)
        {
            sum += data[i];
        }

        partialSums[index] = sum;
    }

    static void Main()
    {
        // Fill the array with values from 1 to 10,000,000
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = i + 1;
        }

        // Use the number of logical processors available
        numWorkers = Environment.ProcessorCount;

        partialSums = new long[numWorkers];

        Thread[] threads = new Thread[numWorkers];

        Console.WriteLine($"Array size: {data.Length:N0}");
        Console.WriteLine($"Worker threads: {numWorkers}");
        Console.WriteLine();

        // Create and start worker threads
        for (int i = 0; i < numWorkers; i++)
        {
            int index = i;

            threads[i] = new Thread(() => SumSlice(index));
            threads[i].Start();
        }

        // Wait for all threads to finish
        for (int i = 0; i < numWorkers; i++)
        {
            threads[i].Join();
        }

        // Combine all partial sums
        long threadedTotal = 0;

        for (int i = 0; i < numWorkers; i++)
        {
            threadedTotal += partialSums[i];
        }

        // Calculate the sum sequentially
        long sequentialTotal = 0;

        for (int i = 0; i < data.Length; i++)
        {
            sequentialTotal += data[i];
        }

        Console.WriteLine($"Threaded total:   {threadedTotal:N0}");
        Console.WriteLine($"Sequential total: {sequentialTotal:N0}");
        Console.WriteLine($"Match: {threadedTotal == sequentialTotal}");
    }
}