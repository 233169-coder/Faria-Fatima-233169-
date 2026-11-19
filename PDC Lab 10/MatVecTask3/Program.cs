
using System;
using System.Diagnostics;
using MPI;

class MatVecTask3
{
    const int N = 2048;

    static Intracommunicator comm;
    static readonly Stopwatch watch = Stopwatch.StartNew();
    static bool useBarrier = true;

    // Start timing a phase
    static double PhaseStart()
    {
        if (useBarrier)
        {
            comm.Barrier();
        }

        return watch.Elapsed.TotalMilliseconds;
    }

    // Stop timing a phase
    static double PhaseEnd(double t0)
    {
        if (useBarrier)
        {
            comm.Barrier();
        }

        return watch.Elapsed.TotalMilliseconds - t0;
    }

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            comm = Communicator.world;

            int rank = comm.Rank;
            int size = comm.Size;

            // Disable barriers when --no-barrier is provided
            if (args.Length > 0 && args[0] == "--no-barrier")
            {
                useBarrier = false;
            }

            int rows = (N + size - 1) / size;

            double[] x = new double[N];
            double[][] blocks = null;
            double[] yRef = null;

            // Rank 0 creates the matrix and reference result
            if (rank == 0)
            {
                for (int j = 0; j < N; j++)
                {
                    x[j] = j % 7 + 1;
                }

                blocks = new double[size][];

                for (int r = 0; r < size; r++)
                {
                    blocks[r] = new double[rows * N];

                    for (int k = 0;
                         k < rows && r * rows + k < N;
                         k++)
                    {
                        for (int j = 0; j < N; j++)
                        {
                            blocks[r][k * N + j] =
                                ((r * rows + k) + j) % 10 + 1;
                        }
                    }
                }

                // Calculate sequential reference result
                yRef = new double[N];

                for (int i = 0; i < N; i++)
                {
                    double sum = 0;

                    for (int j = 0; j < N; j++)
                    {
                        sum += (((i + j) % 10) + 1) * x[j];
                    }

                    yRef[i] = sum;
                }
            }

            // PHASE 1: DISTRIBUTE DATA
            double t = PhaseStart();

            // Send vector x to all ranks
            comm.Broadcast(ref x, 0);

            // Distribute matrix row blocks
            double[] aLocal;

            if (rank == 0)
            {
                aLocal = comm.Scatter(blocks, 0);
            }
            else
            {
                aLocal = comm.Scatter<double[]>(0);
            }

            double tDistribute = PhaseEnd(t);

            // PHASE 2: LOCAL COMPUTATION
            t = PhaseStart();

            double[] yLocal = new double[rows];

            for (int i = 0; i < rows; i++)
            {
                double sum = 0;

                for (int j = 0; j < N; j++)
                {
                    sum += aLocal[i * N + j] * x[j];
                }

                yLocal[i] = sum;
            }

            double tCompute = PhaseEnd(t);

            // PHASE 3: GATHER RESULTS
            t = PhaseStart();

            double[][] parts = comm.Gather(yLocal, 0);

            double tGather = PhaseEnd(t);

            // Find maximum phase time across all ranks
            double worstD =
                comm.Reduce(tDistribute, Operation<double>.Max, 0);

            double worstC =
                comm.Reduce(tCompute, Operation<double>.Max, 0);

            double worstG =
                comm.Reduce(tGather, Operation<double>.Max, 0);

            // Display results on rank 0
            if (rank == 0)
            {
                long wrong = 0;

                for (int i = 0; i < N; i++)
                {
                    if (parts[i / rows][i % rows] != yRef[i])
                    {
                        wrong++;
                    }
                }

                Console.WriteLine(
                    $"[matvec] ranks={size} n={N} " +
                    $"rows_per_rank={rows} " +
                    $"correct={(wrong == 0 ? "YES" : "NO")}"
                );

                Console.WriteLine(
                    $"phases (ms): distribute={worstD:F2} " +
                    $"compute={worstC:F2} " +
                    $"gather={worstG:F2} " +
                    $"total={worstD + worstC + worstG:F2}"
                );
            }
        }
    }
}
