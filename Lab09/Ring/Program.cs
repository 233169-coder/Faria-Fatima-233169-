
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
            int size = comm.Size;

            // This program requires at least 2 ranks
            if (size < 2)
            {
                if (rank == 0)
                    Console.WriteLine("Please run with at least 2 ranks.");

                return;
            }

            // Calculate next and previous ranks
            int next = (rank + 1) % size;
            int previous = (rank - 1 + size) % size;

            const int tag = 10;

            const int originalToken = 2026;

            // Receive buffer: [token value, sequence number]
            int[] received = new int[2];

            // Rank 0 starts the token
            int[] outgoing = new int[2];

            Stopwatch stopwatch = new Stopwatch();

            comm.Barrier();

            if (rank == 0)
            {
                // Original token and sequence number
                outgoing[0] = originalToken;
                outgoing[1] = 0;

                Console.WriteLine(
                    "Rank 0: Previous = " + previous +
                    ", Next = " + next +
                    ", Sending token = " + outgoing[0] +
                    ", Sequence = " + outgoing[1]
                );

                stopwatch.Start();

                // Post receive from previous rank
                ReceiveRequest receiveRequest =
                    comm.ImmediateReceive<int>(
                        previous, tag, received
                    );

                // Send original token to next rank
                Request sendRequest =
                    comm.ImmediateSend(outgoing, next, tag);

                // Complete both communication requests
                receiveRequest.Wait();
                sendRequest.Wait();

                stopwatch.Stop();

                // Verify returned token
                bool correct =
                    received[0] == originalToken &&
                    received[1] == size - 1;

                Console.WriteLine();
                Console.WriteLine("Rank 0: Token returned!");
                Console.WriteLine(
                    "Original token = " + originalToken
                );
                Console.WriteLine(
                    "Returned token = " + received[0]
                );
                Console.WriteLine(
                    "Returned sequence = " + received[1]
                );
                Console.WriteLine(
                    "Data correct = " + correct
                );
                Console.WriteLine(
                    "Total forwards = " + size
                );
                Console.WriteLine(
                    "Completion time = " +
                    stopwatch.ElapsedMilliseconds + " ms"
                );
            }
            else
            {
                // Receive token from previous rank
                ReceiveRequest receiveRequest =
                    comm.ImmediateReceive<int>(
                        previous, tag, received
                    );

                // Wait until token arrives
                receiveRequest.Wait();

                // Increase sequence number
                received[1]++;

                Console.WriteLine(
                    "Rank " + rank +
                    ": Previous = " + previous +
                    ", Next = " + next +
                    ", Received token = " + received[0] +
                    ", Sequence = " + received[1]
                );

                // Forward token to next rank
                Request sendRequest =
                    comm.ImmediateSend(received, next, tag);

                // Wait until sending completes
                sendRequest.Wait();

                Console.WriteLine(
                    "Rank " + rank +
                    ": Forwarded token to Rank " + next
                );
            }
        }
    }
}
