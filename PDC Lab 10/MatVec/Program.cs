
using System;
using System.Diagnostics;
using MPI;

class MatVec
{
    const int N = 2048;

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;

            int rank = comm.Rank;
            int size = comm.Size;

            // Calculate rows assigned to each rank
            int rows = (N + size - 1) / size;

            // Vector x is needed by every rank
            double[] x = new double[N];

            // Matrix blocks are prepared by rank 0
            double[][] blocks = null;

            double[] yRef = null;

            // Rank 0 creates the matrix and vector
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

                    for (int k = 0; k < rows && r * rows + k < N; k++)
                    {
                        for (int j = 0; j < N; j++)
                        {
                            blocks[r][k * N + j] =
                                ((r * rows + k) + j) % 10 + 1;
                        }
                    }
                }

                // Sequential reference calculation
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

            Stopwatch sw = Stopwatch.StartNew();

            // STEP 1: Broadcast vector x to every rank
            comm.Broadcast(ref x, 0);

            // STEP 2: Scatter matrix blocks among ranks
            double[] aLocal;

            if (rank == 0)
            {
                aLocal = comm.Scatter(blocks, 0);
            }
            else
            {
                aLocal = comm.Scatter<double[]>(0);
            }

            // STEP 3: Local matrix-vector multiplication
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

            // STEP 4: Gather results at rank 0
            double[][] parts = comm.Gather(yLocal, 0);

            sw.Stop();

            // Rank 0 verifies the distributed result
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
                    $"[matvec] ranks={size} n={N} rows_per_rank={rows} " +
                    $"correct={(wrong == 0 ? "YES" : "NO")}"
                );

                Console.WriteLine(
                    $"total time={sw.Elapsed.TotalMilliseconds:F2} ms"
                );
            }
        }
    }
}
