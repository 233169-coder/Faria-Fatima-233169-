
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    const int Iterations = 10_000_000;
    const int Repetitions = 3;

    // Padding helps separate counters onto different cache lines.
    // Each counter occupies approximately 128 bytes.
    class PaddedCounter
    {
        public long Value;
        public long P1, P2, P3, P4;
        public long P5, P6, P7, P8;
        public long P9, P10, P11, P12;
        public long P13, P14, P15;
    }

    static long Expected(int threads)
    {
        return (long)Iterations * threads;
    }

    static double Measure(Action action)
    {
        Stopwatch sw = Stopwatch.StartNew();
        action();
        sw.Stop();

        return sw.Elapsed.TotalMilliseconds;
    }

    static long RunAdjacent(int threads)
    {
        long[] counters = new long[threads];

        Parallel.For(
            0,
            threads,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = threads
            },
            worker =>
            {
                for (int i = 0; i < Iterations; i++)
                    counters[worker]++;
            }
        );

        long total = 0;

        for (int i = 0; i < threads; i++)
            total += counters[i];

        return total;
    }

    static long RunPadded(int threads)
    {
        PaddedCounter[] counters = new PaddedCounter[threads];

        for (int i = 0; i < threads; i++)
            counters[i] = new PaddedCounter();

        Parallel.For(
            0,
            threads,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = threads
            },
            worker =>
            {
                for (int i = 0; i < Iterations; i++)
                    counters[worker].Value++;
            }
        );

        long total = 0;

        for (int i = 0; i < threads; i++)
            total += counters[i].Value;

        return total;
    }

    static long RunLocalAccumulation(int threads)
    {
        long[] partial = new long[threads];

        Parallel.For(
            0,
            threads,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = threads
            },
            worker =>
            {
                // Keep updates in a local variable.
                long local = 0;

                for (int i = 0; i < Iterations; i++)
                    local++;

                // Write only once after the loop.
                partial[worker] = local;
            }
        );

        long total = 0;

        for (int i = 0; i < threads; i++)
            total += partial[i];

        return total;
    }

    static double BestTime(Func<long> operation, long expected,
                           out bool correct)
    {
        double best = double.MaxValue;
        correct = true;

        for (int run = 0; run < Repetitions; run++)
        {
            long result = 0;

            double time = Measure(() =>
            {
                result = operation();
            });

            if (result != expected)
                correct = false;

            if (time < best)
                best = time;
        }

        return best;
    }

    static void Main(string[] args)
    {
        int threads = args.Length > 0
            ? int.Parse(args[0])
            : 4;

        if (threads < 1)
        {
            Console.WriteLine("Thread count must be at least 1.");
            return;
        }

        long expected = Expected(threads);

        Console.WriteLine("False Sharing and Padding Benchmark");
        Console.WriteLine($"Logical processors: {Environment.ProcessorCount}");
        Console.WriteLine("Typical cache-line size: 64 bytes");
        Console.WriteLine($"Threads: {threads}");
        Console.WriteLine($"Iterations per thread: {Iterations:N0}");
        Console.WriteLine($"Expected counter total: {expected:N0}");
        Console.WriteLine();

        bool adjacentCorrect;
        bool paddedCorrect;
        bool localCorrect;

        double adjacent = BestTime(
            () => RunAdjacent(threads),
            expected,
            out adjacentCorrect);

        double padded = BestTime(
            () => RunPadded(threads),
            expected,
            out paddedCorrect);

        double local = BestTime(
            () => RunLocalAccumulation(threads),
            expected,
            out localCorrect);

        Console.WriteLine($"{"Version",-20} {"Time (ms)",12} {"Correct?",12}");
        Console.WriteLine(new string('-', 46));

        Console.WriteLine(
            $"{"Adjacent",-20} {adjacent,12:F2} {(adjacentCorrect ? "Y" : "N"),12}");

        Console.WriteLine(
            $"{"Padded",-20} {padded,12:F2} {(paddedCorrect ? "Y" : "N"),12}");

        Console.WriteLine(
            $"{"Local accumulation",-20} {local,12:F2} {(localCorrect ? "Y" : "N"),12}");

        Console.WriteLine();

        if (padded > 0)
            Console.WriteLine($"Adjacent / padded slowdown: {adjacent / padded:F2}x");

        Console.WriteLine(
            $"Fastest version: " +
            (local <= adjacent && local <= padded
                ? "Local accumulation"
                : adjacent <= padded
                    ? "Adjacent"
                    : "Padded"));
    }
}
