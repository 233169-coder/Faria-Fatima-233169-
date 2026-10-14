using System;
using System.Diagnostics;
using System.Threading;

class PhaseBarrier
{
    private readonly int parties;
    private int waiting;
    private int generation;

    public PhaseBarrier(int parties)
    {
        if (parties < 2)
            throw new ArgumentException("At least 2 parties are required.");

        this.parties = parties;
        waiting = 0;
        generation = 0;
    }

    public void SignalAndWait()
    {
        lock (this)
        {
            int myGeneration = generation;

            waiting++;

            if (waiting == parties)
            {
                waiting = 0;
                generation++;

                Monitor.PulseAll(this);
                return;
            }

            while (myGeneration == generation)
            {
                Monitor.Wait(this);
            }
        }
    }
}

class Program
{
    const int N = 500;
    const int ROUNDS = 5;

    static int[,] matrixA = new int[N, N];
    static int[,] matrixB = new int[N, N];
    static int[,] result = new int[N, N];

    static int workers = 4;
    static bool useBarrier = true;

    static PhaseBarrier? barrier;

    static void Worker(object? obj)
    {
        int id = (int)obj!;

        int start = id * N / workers;
        int end = (id + 1) * N / workers;

        for (int round = 0; round < ROUNDS; round++)
        {
            // Phase 1: Matrix multiplication
            for (int i = start; i < end; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    int sum = 0;

                    for (int k = 0; k < N; k++)
                    {
                        sum += matrixA[i, k] * matrixB[k, j];
                    }

                    result[i, j] = sum;
                }
            }

            // First barrier:
            // All workers must finish Phase 1
            // before moving to Phase 2.
            if (useBarrier)
            {
                barrier!.SignalAndWait();
            }

            // Phase 2
            for (int i = start; i < end; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    result[i, j]++;
                }
            }

            // Second barrier:
            // All workers must finish Phase 2
            // before starting the next round.
            if (useBarrier)
            {
                barrier!.SignalAndWait();
            }
        }
    }

    static void Main(string[] args)
    {
        // Default: 4 workers with barrier
        workers = 4;
        useBarrier = true;

        // Handle command-line arguments
        if (args.Length > 0)
        {
            if (args[0] == "--no-barrier")
            {
                useBarrier = false;
            }
            else if (int.TryParse(args[0], out int requestedWorkers))
            {
                workers = requestedWorkers;
            }
        }

        // Initialize matrices
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                matrixA[i, j] = 1;
                matrixB[i, j] = 1;
                result[i, j] = 0;
            }
        }

        barrier = new PhaseBarrier(workers);

        Thread[] threads = new Thread[workers];

        Stopwatch stopwatch = Stopwatch.StartNew();

        // Create and start workers
        for (int i = 0; i < workers; i++)
        {
            int id = i;

            threads[i] = new Thread(Worker);
            threads[i].Start(id);
        }

        // Wait for all workers
        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        stopwatch.Stop();

        // Each matrix multiplication produces 500.
        // Phase 2 adds 1.
        // Therefore, expected final value = 501.
        int expected = N + 1;

        int mismatches = 0;

        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                if (result[i, j] != expected)
                {
                    mismatches++;
                }
            }
        }

        Console.WriteLine($"Workers: {workers}");
        Console.WriteLine($"Barrier: {useBarrier}");
        Console.WriteLine($"Correct: {(mismatches == 0 ? "Y" : "N")}");
        Console.WriteLine($"Mismatching cells: {mismatches}");
        Console.WriteLine($"Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        Console.WriteLine($"ProcessorCount: {Environment.ProcessorCount}");
    }
}

