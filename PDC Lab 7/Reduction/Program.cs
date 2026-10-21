
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    const int N = 5_000_000;
    const int Reps = 3;

    static readonly int[] data = new int[N];
    static readonly object gate = new object();

    static int degree = Environment.ProcessorCount;

    struct Partial
    {
        public long Sum;
        public int Max;
    }

    delegate void Variant(out long sum, out int max);

    static ParallelOptions Options() =>
        new ParallelOptions
        {
            MaxDegreeOfParallelism = degree
        };

    static void SerialVersion(out long sum, out int max)
    {
        long s = 0;
        int m = int.MinValue;

        for (int i = 0; i < N; i++)
        {
            s += data[i];

            if (data[i] > m)
                m = data[i];
        }

        sum = s;
        max = m;
    }

    // Intentionally incorrect: demonstrates race conditions.
    static void RacyVersion(out long sum, out int max)
    {
        long s = 0;
        int m = int.MinValue;

        Parallel.For(0, N, Options(), i =>
        {
            s += data[i];

            if (data[i] > m)
                m = data[i];
        });

        sum = s;
        max = m;
    }

    // One lock for every element.
    static void LockEachVersion(out long sum, out int max)
    {
        long s = 0;
        int m = int.MinValue;

        Parallel.For(0, N, Options(), i =>
        {
            lock (gate)
            {
                s += data[i];

                if (data[i] > m)
                    m = data[i];
            }
        });

        sum = s;
        max = m;
    }

    // Each task calculates a private partial result.
    // The partial results are combined using one short lock per task.
    static void LockPartialVersion(out long sum, out int max)
    {
        long s = 0;
        int m = int.MinValue;

        Parallel.ForEach(
            Partitioner.Create(0, N),
            Options(),
            () => new Partial
            {
                Sum = 0,
                Max = int.MinValue
            },
            (range, state, local) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    local.Sum += data[i];

                    if (data[i] > local.Max)
                        local.Max = data[i];
                }

                return local;
            },
            local =>
            {
                lock (gate)
                {
                    s += local.Sum;

                    if (local.Max > m)
                        m = local.Max;
                }
            });

        sum = s;
        max = m;
    }

    // PLINQ performs parallel aggregation.
    static void PlinqVersion(out long sum, out int max)
    {
        sum = data
            .AsParallel()
            .WithDegreeOfParallelism(degree)
            .Select(x => (long)x)
            .Sum();

        max = data
            .AsParallel()
            .WithDegreeOfParallelism(degree)
            .Max();
    }

    static void Main(string[] args)
    {
        if (args.Length > 0)
            degree = int.Parse(args[0]);

        if (degree < 1)
            throw new ArgumentOutOfRangeException(
                nameof(degree), "Degree must be at least 1.");

        for (int i = 0; i < N; i++)
            data[i] = (int)(((long)i * 7919) % 100003);

        SerialVersion(out long refSum, out int refMax);

        Console.WriteLine(
            $"degree={degree} n={N} reps={Reps}");

        string[] names =
        {
            "serial",
            "racy",
            "lock-each",
            "lock-partial",
            "plinq"
        };

        Variant[] functions =
        {
            SerialVersion,
            RacyVersion,
            LockEachVersion,
            LockPartialVersion,
            PlinqVersion
        };

        for (int v = 0; v < functions.Length; v++)
        {
            long sum = 0;
            int max = 0;
            double total = 0;

            for (int r = 0; r < Reps; r++)
            {
                Stopwatch sw = Stopwatch.StartNew();

                functions[v](out sum, out max);

                sw.Stop();
                total += sw.Elapsed.TotalMilliseconds;
            }

            Console.WriteLine(
                $"[{names[v],-13}] sum={sum} max={max} " +
                $"sum_ok={(sum == refSum ? "YES" : "NO")} " +
                $"max_ok={(max == refMax ? "YES" : "NO")} " +
                $"avg_time={total / Reps:F2} ms");
        }
    }
}
