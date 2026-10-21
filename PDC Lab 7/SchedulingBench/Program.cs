
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    const int N = 4000;
    const int Repetitions = 3;

    static long DoWork(int i)
    {
        // Higher iteration indexes require more work.
        Thread.SpinWait(500 + i * 4);
        return i;
    }

    static long ExpectedSum()
    {
        return (long)N * (N - 1) / 2;
    }

    static long RunSerial(out double timeMs)
    {
        long sum = 0;
        Stopwatch sw = Stopwatch.StartNew();

        for (int i = 0; i < N; i++)
            sum += DoWork(i);

        sw.Stop();
        timeMs = sw.Elapsed.TotalMilliseconds;

        return sum;
    }

    class Result
    {
        public string Policy = "";
        public double WallMs;
        public double BusyMax;
        public double BusyMin;
        public double Imbalance;
        public bool Correct;
    }

    static Result RunPolicy(string policy, int degree)
    {
        long[] partial = new long[degree];
        double[] busy = new double[degree];
        int next = 0;

        Stopwatch wall = Stopwatch.StartNew();

        Parallel.For(
            0,
            degree,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = degree
            },
            worker =>
            {
                long localSum = 0;
                Stopwatch workerWatch = Stopwatch.StartNew();

                void ProcessRange(int start, int end)
                {
                    for (int i = start; i < end; i++)
                        localSum += DoWork(i);
                }

                if (policy == "static")
                {
                    // Each worker receives one contiguous block.
                    int start = worker * N / degree;
                    int end = (worker + 1) * N / degree;

                    ProcessRange(start, end);
                }
                else if (policy == "static,1")
                {
                    // Workers process iterations cyclically.
                    for (int i = worker; i < N; i += degree)
                        localSum += DoWork(i);
                }
                else if (policy == "static,64")
                {
                    // Cyclic scheduling with chunks of 64.
                    for (int start = worker * 64;
                         start < N;
                         start += degree * 64)
                    {
                        ProcessRange(start, Math.Min(start + 64, N));
                    }
                }
                else if (policy == "dynamic,1" ||
                         policy == "dynamic,64")
                {
                    int chunk = policy == "dynamic,1" ? 1 : 64;

                    while (true)
                    {
                        int start =
                            Interlocked.Add(ref next, chunk) - chunk;

                        if (start >= N)
                            break;

                        ProcessRange(start, Math.Min(start + chunk, N));
                    }
                }
                else if (policy == "guided,1" ||
                         policy == "guided,64")
                {
                    int minimumChunk =
                        policy == "guided,1" ? 1 : 64;

                    while (true)
                    {
                        int start = 0;
                        int end = 0;

                        while (true)
                        {
                            start = Volatile.Read(ref next);

                            if (start >= N)
                                break;

                            int remaining = N - start;

                            int chunk = Math.Max(
                                minimumChunk,
                                (remaining + degree - 1) / degree
                            );

                            end = Math.Min(N, start + chunk);

                            // Claim this range atomically.
                            if (Interlocked.CompareExchange(
                                    ref next, end, start) == start)
                            {
                                break;
                            }
                        }

                        if (start >= N)
                            break;

                        ProcessRange(start, end);
                    }
                }

                workerWatch.Stop();

                partial[worker] = localSum;
                busy[worker] = workerWatch.Elapsed.TotalMilliseconds;
            }
        );

        wall.Stop();

        long total = partial.Sum();
        double max = busy.Max();
        double min = busy.Min();

        return new Result
        {
            Policy = policy,
            WallMs = wall.Elapsed.TotalMilliseconds,
            BusyMax = max,
            BusyMin = min,
            Imbalance = min > 0 ? max / min : 0,
            Correct = total == ExpectedSum()
        };
    }

    class WorkerStats
    {
        public long Sum;
        public Stopwatch Watch = new Stopwatch();
    }

    static Result RunParallelFor(int degree)
    {
        long sum = 0;
        ConcurrentBag<double> workerTimes = new();

        Stopwatch wall = Stopwatch.StartNew();

        Parallel.For(
            0,
            N,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = degree
            },
            () => new WorkerStats(),
            (i, state, local) =>
            {
                local.Watch.Start();
                local.Sum += DoWork(i);
                local.Watch.Stop();

                return local;
            },
            local =>
            {
                Interlocked.Add(ref sum, local.Sum);
                workerTimes.Add(local.Watch.Elapsed.TotalMilliseconds);
            }
        );

        wall.Stop();

        double max = workerTimes.Count > 0
            ? workerTimes.Max()
            : 0;

        double min = workerTimes.Count > 0
            ? workerTimes.Min()
            : 0;

        return new Result
        {
            Policy = "Parallel.For",
            WallMs = wall.Elapsed.TotalMilliseconds,
            BusyMax = max,
            BusyMin = min,
            Imbalance = min > 0 ? max / min : 0,
            Correct = sum == ExpectedSum()
        };
    }

    static Result BestOfThree(string policy, int degree)
    {
        Result? best = null;

        for (int run = 0; run < Repetitions; run++)
        {
            Result current = policy == "Parallel.For"
                ? RunParallelFor(degree)
                : RunPolicy(policy, degree);

            if (best == null || current.WallMs < best.WallMs)
                best = current;
        }

        return best!;
    }

    static void Main(string[] args)
    {
        int degree = args.Length > 0
            ? int.Parse(args[0])
            : 4;

        if (degree < 1)
        {
            Console.WriteLine("Thread count must be at least 1.");
            return;
        }

        Console.WriteLine($"Threads: {degree}");
        Console.WriteLine($"N: {N}");
        Console.WriteLine(
            $"Environment.ProcessorCount: {Environment.ProcessorCount}");

        long expected = RunSerial(out double serialMs);

        Console.WriteLine($"Expected sum: {expected}");
        Console.WriteLine($"Serial time: {serialMs:F2} ms");
        Console.WriteLine();

        string[] policies =
        {
            "static",
            "static,1",
            "static,64",
            "dynamic,1",
            "dynamic,64",
            "guided,1",
            "guided,64",
            "Parallel.For"
        };

        Console.WriteLine(
            $"{"Policy",-14} {"Wall(ms)",10} {"Busy max",10} " +
            $"{"Busy min",10} {"Imbalance",10} {"Correct",8}");

        Result? fastest = null;

        foreach (string policy in policies)
        {
            Result result = BestOfThree(policy, degree);

            Console.WriteLine(
                $"{result.Policy,-14} " +
                $"{result.WallMs,10:F2} " +
                $"{result.BusyMax,10:F2} " +
                $"{result.BusyMin,10:F2} " +
                $"{result.Imbalance,10:F2} " +
                $"{(result.Correct ? "Y" : "N"),8}");

            if (fastest == null || result.WallMs < fastest.WallMs)
                fastest = result;
        }

        Console.WriteLine();
        Console.WriteLine($"Fastest policy: {fastest!.Policy}");
    }
}
