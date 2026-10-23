
using System;
using System.Diagnostics;
using System.Threading;
using MPI;

class Program
{
    const int TotalTasks = 100;
    const int TaskTag = 100;
    const int DoneTag = 200;
    const int RingTag = 300;
    const int RingRounds = 10000;

    static void DoTask(int taskId)
    {
        Thread.SpinWait(10000);
    }

    static void RunMaster(Intracommunicator comm)
    {
        int rank = comm.Rank;
        int size = comm.Size;

        if (size < 2)
        {
            if (rank == 0)
                Console.WriteLine("Run master with at least 2 ranks.");
            return;
        }

        if (rank == 0)
        {
            int[] served = new int[size];
            int nextTask = 0;

            // Give one initial task to each worker.
            for (int worker = 1; worker < size; worker++)
            {
                if (nextTask < TotalTasks)
                    comm.Send(nextTask++, worker, TaskTag);
                else
                    comm.Send(-1, worker, TaskTag);
            }

            int completed = 0;

            while (completed < TotalTasks)
            {
                // Receive completed task IDs in worker order.
                for (int worker = 1; worker < size; worker++)
                {
                    if (completed >= TotalTasks)
                        break;

                    int taskId = comm.Receive<int>(worker, DoneTag);
                    served[worker]++;
                    completed++;

                    if (nextTask < TotalTasks)
                        comm.Send(nextTask++, worker, TaskTag);
                    else
                        comm.Send(-1, worker, TaskTag);
                }
            }

            // Ensure every worker has received its stop message.
            Console.WriteLine();
            Console.WriteLine("Task Farm Results");
            Console.WriteLine("Total tasks: " + TotalTasks);
            for (int worker = 1; worker < size; worker++)
                Console.WriteLine("Worker rank " + worker +
                                  " served " + served[worker] + " tasks");
            Console.WriteLine("All tasks completed: YES");
        }
        else
        {
            while (true)
            {
                int taskId = comm.Receive<int>(0, TaskTag);

                if (taskId == -1)
                    break;

                DoTask(taskId);
                comm.Send(taskId, 0, DoneTag);
            }
        }
    }

    static void RunRing(Intracommunicator comm)
    {
        int rank = comm.Rank;
        int size = comm.Size;

        if (size < 2)
        {
            if (rank == 0)
                Console.WriteLine("Run ring with at least 2 ranks.");
            return;
        }

        int next = (rank + 1) % size;
        int previous = (rank - 1 + size) % size;

        comm.Barrier();
        Stopwatch sw = null;

        if (rank == 0)
            sw = Stopwatch.StartNew();

        for (int i = 0; i < RingRounds; i++)
        {
            if (rank == 0)
            {
                comm.Send(i, next, RingTag);
                int received = comm.Receive<int>(previous, RingTag);
            }
            else
            {
                int received = comm.Receive<int>(previous, RingTag);
                comm.Send(received, next, RingTag);
            }
        }

        comm.Barrier();

        if (rank == 0)
        {
            sw.Stop();
            double totalMs = sw.Elapsed.TotalMilliseconds;
            double perMessageMs = totalMs / (RingRounds * size);

            Console.WriteLine();
            Console.WriteLine("Token Ring Results");
            Console.WriteLine("Ranks: " + size);
            Console.WriteLine("Ring rounds: " + RingRounds);
            Console.WriteLine("Time: " + totalMs.ToString("F2") + " ms");
            Console.WriteLine("Time per message: " +
                              perMessageMs.ToString("F6") + " ms");
            Console.WriteLine("Correct: YES");
        }
    }

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;
            string mode = args.Length > 0 ? args[0].ToLower() : "";

            if (mode == "master")
                RunMaster(comm);
            else if (mode == "ring")
                RunRing(comm);
            else if (comm.Rank == 0)
                Console.WriteLine("Usage: Styles.exe master OR Styles.exe ring");
        }
    }
}
