
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

            // Create send and receive buffers
            byte[] data = new byte[messageSize];
            byte[] received = new byte[messageSize];

            // Fill send buffer with rank-specific data
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(rank + 1);
            }

            comm.Barrier();

            Stopwatch stopwatch = Stopwatch.StartNew();

            Console.WriteLine(
                "Rank " + rank + ": Posting non-blocking Receive"
            );

            // Post non-blocking receive into the provided buffer
            ReceiveRequest receiveRequest =
                comm.ImmediateReceive<byte>(peer, 0, received);

            Console.WriteLine(
                "Rank " + rank + ": Starting non-blocking Send"
            );

            // Start non-blocking send
            Request sendRequest =
                comm.ImmediateSend(data, peer, 0);

            Console.WriteLine(
                "Rank " + rank + ": Waiting for requests"
            );

            // Wait until receiving completes
            receiveRequest.Wait();

            // Wait until sending completes
            sendRequest.Wait();

            stopwatch.Stop();

            // Verify received data
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
