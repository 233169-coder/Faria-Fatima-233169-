
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

            // Two processes communicate with each other
            int peer = 1 - rank;

            // Create an 8 MiB message
            int messageSize = 8 * 1024 * 1024;

            byte[] data = new byte[messageSize];

            // Fill the message
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(rank + 1);
            }

            Console.WriteLine(
                "Rank " + rank + ": Starting blocking Send to Rank " + peer
            );

            Console.Out.Flush();

            Stopwatch stopwatch = Stopwatch.StartNew();

            // Both ranks send BEFORE receiving
            comm.Send(data, peer, 0);

            Console.WriteLine(
                "Rank " + rank + ": Send completed. Now waiting to receive."
            );

            Console.Out.Flush();

            // Receive message from the other rank
            byte[] received = comm.Receive<byte[]>(peer, 0);

            stopwatch.Stop();

            Console.WriteLine(
                "Rank " + rank +
                ": Receive completed. Message size: " +
                received.Length +
                " bytes. Time: " +
                stopwatch.ElapsedMilliseconds +
                " ms"
            );
        }
    }
}
