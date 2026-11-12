
using System;
using System.Diagnostics;
using System.IO;
using MPI;

class Program
{
    const int TAG = 100;
    const int TRIALS = 5;

    static int GetIterations(int size)
    {
        if (size <= 1024) return 2000;
        if (size <= 16384) return 1000;
        if (size <= 65536) return 200;
        return 50;
    }

    static byte[] CreatePayload(int size)
    {
        byte[] data = new byte[size];

        for (int i = 0; i < size; i++)
            data[i] = (byte)(i % 256);

        return data;
    }

    static bool IsCorrect(byte[] received, byte[] expected)
    {
        if (received == null || received.Length != expected.Length)
            return false;

        for (int i = 0; i < expected.Length; i++)
        {
            if (received[i] != expected[i])
                return false;
        }

        return true;
    }

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;

            if (comm.Size != 2)
            {
                if (comm.Rank == 0)
                    Console.WriteLine("Please run with exactly 2 MPI ranks.");

                return;
            }

            int[] sizes = {
                0, 1, 8, 64, 256,
                1024, 4096, 16384, 65536, 262144
            };

            if (comm.Rank == 0)
            {
                Console.WriteLine("Two-Rank Ping-Pong Benchmark");
                Console.WriteLine("Ranks: 2");
                Console.WriteLine();

                Console.WriteLine(
                    "{0,-15} {1,-12} {2,-18} {3,-18} {4}",
                    "Message Size", "Iterations",
                    "Mean RTT (us)", "RTT/2 (us)", "Correct");

                using (StreamWriter csv = new StreamWriter("pingpong_results.csv"))
                {
                    csv.WriteLine("MessageSize,Iterations,MeanRTT_us,RTT2_us,Correct");

                    foreach (int size in sizes)
                    {
                        byte[] payload = CreatePayload(size);
                        int iterations = GetIterations(size);
                        bool correct = true;

                        // Warm-up exchanges
                        for (int i = 0; i < 10; i++)
                        {
                            comm.Send<byte[]>(payload, 1, TAG);
                            byte[] returned = comm.Receive<byte[]>(1, TAG);

                            if (!IsCorrect(returned, payload))
                                correct = false;
                        }

                        double totalTrialRTT = 0.0;

                        // Five measurement trials
                        for (int trial = 0; trial < TRIALS; trial++)
                        {
                            comm.Barrier();

                            Stopwatch stopwatch = Stopwatch.StartNew();

                            for (int i = 0; i < iterations; i++)
                            {
                                comm.Send<byte[]>(payload, 1, TAG);

                                byte[] returned = comm.Receive<byte[]>(1, TAG);

                                if (!IsCorrect(returned, payload))
                                    correct = false;
                            }

                            stopwatch.Stop();

                            double trialRTT =
                                (stopwatch.Elapsed.TotalMilliseconds * 1000.0)
                                / iterations;

                            totalTrialRTT += trialRTT;
                        }

                        double meanRTT = totalTrialRTT / TRIALS;
                        double oneWayEstimate = meanRTT / 2.0;

                        string result = correct ? "Y" : "N";

                        Console.WriteLine(
                            "{0,-15} {1,-12} {2,-18:F3} {3,-18:F3} {4}",
                            size, iterations, meanRTT,
                            oneWayEstimate, result);

                        csv.WriteLine(
                            "{0},{1},{2:F3},{3:F3},{4}",
                            size, iterations, meanRTT,
                            oneWayEstimate, result);
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Results saved to pingpong_results.csv");
            }
            else if (comm.Rank == 1)
            {
                foreach (int size in sizes)
                {
                    byte[] payload = CreatePayload(size);
                    int iterations = GetIterations(size);

                    // Warm-up exchanges
                    for (int i = 0; i < 10; i++)
                    {
                        byte[] received = comm.Receive<byte[]>(0, TAG);
                        comm.Send<byte[]>(received, 0, TAG);
                    }

                    // Measurement exchanges
                    for (int trial = 0; trial < TRIALS; trial++)
                    {
                        comm.Barrier();

                        for (int i = 0; i < iterations; i++)
                        {
                            byte[] received = comm.Receive<byte[]>(0, TAG);
                            comm.Send<byte[]>(received, 0, TAG);
                        }
                    }
                }
            }
        }
    }
}
