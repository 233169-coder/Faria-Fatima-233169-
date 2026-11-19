
using System;
using System.Diagnostics;
using MPI;

class ArraySum
{
    const int N = 10_000_003;

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;

            int rank = comm.Rank;
            int size = comm.Size;

            // Calculate equal chunk size and padding
            int chunk = (N + size - 1) / size;
            int padded = chunk * size;

            int[][] chunks = null;

            long refSum = 0;
            int refMax = int.MinValue;
            double sequentialTime = 0;

            // Rank 0 creates the array and calculates reference results
            if (rank == 0)
            {
                int[] data = new int[padded];

                for (int i = 0; i < N; i++)
                {
                    data[i] = (int)(((long)i * 7919) % 100003);
                }

                Stopwatch seqWatch = Stopwatch.StartNew();

                for (int i = 0; i < N; i++)
                {
                    refSum += data[i];

                    if (data[i] > refMax)
                    {
                        refMax = data[i];
                    }
                }

                seqWatch.Stop();
                sequentialTime = seqWatch.Elapsed.TotalMilliseconds;

                // Divide the array into equal chunks
                chunks = new int[size][];

                for (int r = 0; r < size; r++)
                {
                    chunks[r] = new int[chunk];

                    Array.Copy(
                        data,
                        r * chunk,
                        chunks[r],
                        0,
                        chunk
                    );
                }
            }

            // Synchronize before starting parallel timing
            comm.Barrier();

            Stopwatch sw = Stopwatch.StartNew();

            // 1. Scatter: distribute chunks among all ranks
            int[] mine;

            if (rank == 0)
            {
                mine = comm.Scatter(chunks, 0);
            }
            else
            {
                mine = comm.Scatter<int[]>(0);
            }

            // 2. Calculate local sum and maximum
            long localSum = 0;
            int localMax = int.MinValue;

            for (int i = 0; i < mine.Length; i++)
            {
                localSum += mine[i];

                if (mine[i] > localMax)
                {
                    localMax = mine[i];
                }
            }

            // 3. Reduce: combine local sums on root
            long total = comm.Reduce(
                localSum,
                Operation<long>.Add,
                0
            );

            // 4. Reduce: find global maximum on root
            int gmax = comm.Reduce(
                localMax,
                Operation<int>.Max,
                0
            );

            // Synchronize before stopping the timer
            comm.Barrier();

            sw.Stop();

            // Display results only on rank 0
            if (rank == 0)
            {
                Console.WriteLine(
                    $"[array-sum] ranks={size} n={N} " +
                    $"chunk={chunk} padding={padded - N}"
                );

                bool sumCorrect = total == refSum;
                bool maxCorrect = gmax == refMax;

                Console.WriteLine(
                    $"sum={total} reference={refSum} " +
                    $"max={gmax} reference_max={refMax}"
                );

                Console.WriteLine(
                    $"sum_correct={(sumCorrect ? "YES" : "NO")} " +
                    $"max_correct={(maxCorrect ? "YES" : "NO")}"
                );

                Console.WriteLine(
                    $"parallel_time={sw.Elapsed.TotalMilliseconds:F2} ms"
                );

                Console.WriteLine(
                    $"sequential_time={sequentialTime:F2} ms"
                );
            }
        }
    }
}
