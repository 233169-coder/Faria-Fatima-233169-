
using System;
using System.Diagnostics;
using MPI;

class Program
{
    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;

            int rank = comm.Rank;
            int peer = 1 - rank;

            int messageSize = 8 * 1024 * 1024;

            byte[] data = new byte[messageSize];

            // Each rank fills its message with a different value
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(rank + 1);
            }

            byte[] received;

            comm.Barrier();

            Stopwatch stopwatch = Stopwatch.StartNew();

            if (rank == 0)
            {
                // Rank 0: Send first, then Receive
                Console.WriteLine("Rank 0: Sending to Rank 1");

                comm.Send(data, 1, 0);

                Console.WriteLine("Rank 0: Send completed");

                received = comm.Receive<byte[]>(1, 0);
            }
            else
            {
                // Rank 1: Receive first, then Send
                Console.WriteLine("Rank 1: Receiving from Rank 0");

                received = comm.Receive<byte[]>(0, 0);

                Console.WriteLine("Rank 1: Receive completed");

                comm.Send(data, 0, 0);

                Console.WriteLine("Rank 1: Send completed");
            }

            stopwatch.Stop();

            // Check received data
            bool correct = received.Length == messageSize;

            for (int i = 0; i < received.Length && correct; i++)
            {
                if (received[i] != (byte)(peer + 1))
                {
                    correct = false;
                }
            }

            Console.WriteLine(
                "Rank " + rank +
                ": Data correct = " + correct +
                ", Time = " + stopwatch.ElapsedMilliseconds + " ms"
            );
        }
    }
}
