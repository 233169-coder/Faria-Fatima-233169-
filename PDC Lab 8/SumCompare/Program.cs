
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MPI;

class Program
{
    const int N = 10000000;
    const int Repetitions = 3;
    const int DataTag = 100;
    const int SumTag = 200;

    static readonly long ExpectedSum = 50000005000000L;

    static long SumArray(int[] data)
    {
        long sum = 0;

        for (int i = 0; i < data.Length; i++)
            sum += data[i];

        return sum;
    }

    static int[] GetChunk(int[] data, int rank, int chunkSize)
    {
        int[] chunk = new int[chunkSize];
        Array.Copy(data, rank * chunkSize, chunk, 0, chunkSize);
        return chunk;
    }

    static long CollectiveSum(Intracommunicator comm, int[] data,
                              int chunkSize)
    {
        int[][] chunks = null;

        if (comm.Rank == 0)
        {
            chunks = new int[comm.Size][];

            for (int r = 0; r < comm.Size; r++)
                chunks[r] = GetChunk(data, r, chunkSize);
        }

        int[] localData = comm.Scatter(chunks, 0);
        long localSum = SumArray(localData);

        return comm.Reduce(localSum, Operation<long>.Add, 0);
    }

    static long PointToPointSum(Intracommunicator comm, int[] data,
                                int chunkSize)
    {
        int rank = comm.Rank;
        int size = comm.Size;

        if (rank == 0)
        {
            // Send chunk elements individually.
            for (int dest = 1; dest < size; dest++)
            {
                int[] chunk = GetChunk(data, dest, chunkSize);

                for (int i = 0; i < chunkSize; i++)
                    comm.Send(chunk[i], dest, DataTag);
            }

            long total = SumArray(GetChunk(data, 0, chunkSize));

            for (int source = 1; source < size; source++)
            {
                long partial = comm.Receive<long>(source, SumTag);
                total += partial;
            }

            return total;
        }
        else
        {
            int[] localData = new int[chunkSize];

            for (int i = 0; i < chunkSize; i++)
                localData[i] = comm.Receive<int>(0, DataTag);

            long localSum = SumArray(localData);
            comm.Send(localSum, 0, SumTag);

            return 0;
        }
    }

    static long SharedMemorySum(int[] data, int threadCount)
    {
        long total = 0;

        ParallelOptions options = new ParallelOptions();
        options.MaxDegreeOfParallelism = threadCount;

        Parallel.For(
            0, data.Length, options,
            () => 0L,
            (i, state, localSum) => localSum + data[i],
            localSum => Interlocked.Add(ref total, localSum)
        );

        return total;
    }

    static double AverageCollective(Intracommunicator comm, int[] data,
                                    int chunkSize, out long result)
    {
        double elapsed = 0;
        result = 0;

        for (int rep = 0; rep < Repetitions; rep++)
        {
            comm.Barrier();
            Stopwatch sw = Stopwatch.StartNew();

            long current = CollectiveSum(comm, data, chunkSize);

            sw.Stop();

            if (comm.Rank == 0)
            {
                elapsed += sw.Elapsed.TotalMilliseconds;
                result = current;
            }
        }

        return elapsed / Repetitions;
    }

    static double AveragePointToPoint(Intracommunicator comm, int[] data,
                                      int chunkSize, out long result)
    {
        double elapsed = 0;
        result = 0;

        for (int rep = 0; rep < Repetitions; rep++)
        {
            comm.Barrier();
            Stopwatch sw = Stopwatch.StartNew();

            long current = PointToPointSum(comm, data, chunkSize);

            sw.Stop();

            if (comm.Rank == 0)
            {
                elapsed += sw.Elapsed.TotalMilliseconds;
                result = current;
            }
        }

        return elapsed / Repetitions;
    }

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;

            int rank = comm.Rank;
            int size = comm.Size;

            if (N % size != 0)
            {
                if (rank == 0)
                    Console.WriteLine("The number of ranks must divide " + N);
                return;
            }

            int chunkSize = N / size;
            int[] data = null;

            if (rank == 0)
            {
                data = new int[N];

                for (int i = 0; i < N; i++)
                    data[i] = i + 1;
            }

            long collectiveResult;
            long pointResult;

            double collectiveMs =
                AverageCollective(comm, data, chunkSize, out collectiveResult);

            double pointMs =
                AveragePointToPoint(comm, data, chunkSize, out pointResult);

            double sharedElapsed = 0;
            long sharedResult = 0;

            for (int rep = 0; rep < Repetitions; rep++)
            {
                comm.Barrier();

                if (rank == 0)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    sharedResult = SharedMemorySum(data, size);
                    sw.Stop();

                    sharedElapsed += sw.Elapsed.TotalMilliseconds;
                }

                comm.Barrier();
            }

            if (rank == 0)
            {
                Console.WriteLine();
                Console.WriteLine("SumCompare Results");
                Console.WriteLine("Ranks / threads: " + size);
                Console.WriteLine("Expected sum: " + ExpectedSum);
                Console.WriteLine();

                Console.WriteLine("MPI collective: " +
                    collectiveMs.ToString("F2") + " ms");
                Console.WriteLine("MPI point-to-point: " +
                    pointMs.ToString("F2") + " ms");
                Console.WriteLine("Shared memory TPL: " +
                    (sharedElapsed / Repetitions).ToString("F2") + " ms");

                Console.WriteLine();
                Console.WriteLine("Collective correct: " +
                    (collectiveResult == ExpectedSum ? "YES" : "NO"));
                Console.WriteLine("Point-to-point correct: " +
                    (pointResult == ExpectedSum ? "YES" : "NO"));
                Console.WriteLine("Shared memory correct: " +
                    (sharedResult == ExpectedSum ? "YES" : "NO"));
            }
        }
    }
}
