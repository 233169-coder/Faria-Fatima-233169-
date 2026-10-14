using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int BufferSize = 64;
    const int Items = 200_000;

    static readonly int[] buffer = new int[BufferSize];

    static int inPos = 0;
    static int outPos = 0;
    static int count = 0;

    static readonly object gate = new object();

    static long producerRetries = 0;
    static long consumerRetries = 0;

    static long consumedSum = 0;
    static long orderErrors = 0;

    static void Producer()
    {
        for (int item = 1; item <= Items; item++)
        {
            while (true)
            {
                bool inserted = false;

                lock (gate)
                {
                    if (count < BufferSize)
                    {
                        buffer[inPos] = item;

                        inPos = (inPos + 1) % BufferSize;
                        count++;

                        inserted = true;
                    }
                }

                if (inserted)
                    break;

                producerRetries++;
                Thread.Yield();
            }
        }
    }

    static void Consumer()
    {
        int expected = 1;

        while (expected <= Items)
        {
            bool removed = false;
            int item = 0;

            lock (gate)
            {
                if (count > 0)
                {
                    item = buffer[outPos];

                    outPos = (outPos + 1) % BufferSize;
                    count--;

                    removed = true;
                }
            }

            if (removed)
            {
                consumedSum += item;

                if (item != expected)
                    orderErrors++;

                expected++;
            }
            else
            {
                consumerRetries++;
                Thread.Yield();
            }
        }
    }

    static void Main()
    {
        Stopwatch sw = Stopwatch.StartNew();

        Thread producer = new Thread(Producer);
        Thread consumer = new Thread(Consumer);

        producer.Start();
        consumer.Start();

        producer.Join();
        consumer.Join();

        sw.Stop();

        long expectedSum = (long)Items * (Items + 1) / 2;

        bool checksumOk = consumedSum == expectedSum;

        Console.WriteLine($"[lock + polling] items={Items} buffer={BufferSize}");
        Console.WriteLine(
            $"expected_sum={expectedSum} " +
            $"actual_sum={consumedSum} " +
            $"checksum_ok={(checksumOk ? "YES" : "NO")} " +
            $"order_errors={orderErrors}"
        );

        Console.WriteLine(
            $"producer_retries={producerRetries} " +
            $"consumer_retries={consumerRetries} " +
            $"time={sw.Elapsed.TotalMilliseconds:F2} ms"
        );
    }
}