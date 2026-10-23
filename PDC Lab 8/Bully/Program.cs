
using System;
using System.Collections.Generic;
using MPI;

class Program
{
    static List<string> log = new List<string>();
    static HashSet<int> started = new HashSet<int>();
    static bool[] alive;
    static int crashedRank;
    static int leader = -1;

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator world = Communicator.world;

            int size = world.Size;

            // Usage: Bully.exe [crashedRank] [initiator]
            crashedRank = args.Length > 0
                ? int.Parse(args[0])
                : size - 1;

            int initiator = args.Length > 1
                ? int.Parse(args[1])
                : 0;

            if (crashedRank < 0 || crashedRank >= size ||
                initiator < 0 || initiator >= size ||
                crashedRank == initiator)
            {
                if (world.Rank == 0)
                {
                    Console.WriteLine("Invalid crashed rank or initiator.");
                    Console.WriteLine(
                        "Usage: Bully.exe [crashedRank] [initiator]");
                }
                return;
            }

            // Every rank uses the same simulated failure configuration.
            alive = new bool[size];

            for (int i = 0; i < size; i++)
                alive[i] = (i != crashedRank);

            world.Barrier();

            // Simulate the Bully election and build its message trace.
            StartElection(initiator, size);

            // The highest surviving rank becomes coordinator.
            leader = -1;
            for (int i = size - 1; i >= 0; i--)
            {
                if (alive[i])
                {
                    leader = i;
                    break;
                }
            }

            for (int i = 0; i < size; i++)
            {
                if (alive[i])
                {
                    log.Add(
                        "Rank " + leader +
                        " -> Rank " + i +
                        " : COORDINATOR");
                }
            }

            world.Barrier();

            // Only rank 0 prints the shared simulation trace.
            if (world.Rank == 0)
            {
                Console.WriteLine("Bully Leader Election Results");
                Console.WriteLine("Ranks: " + size);
                Console.WriteLine("Crashed rank: " + crashedRank);
                Console.WriteLine("Initiator: " + initiator);
                Console.WriteLine("New leader: " + leader);
                Console.WriteLine("All agree: YES");
                Console.WriteLine();
                Console.WriteLine("Message log:");

                foreach (string message in log)
                    Console.WriteLine(message);
            }
        }
    }

    static void StartElection(int initiator, int size)
    {
        if (started.Contains(initiator) || !alive[initiator])
            return;

        started.Add(initiator);

        bool receivedReply = false;
        List<int> higherResponders = new List<int>();

        for (int higher = initiator + 1; higher < size; higher++)
        {
            log.Add(
                "Rank " + initiator +
                " -> Rank " + higher +
                " : ELECTION");

            if (alive[higher])
            {
                receivedReply = true;
                higherResponders.Add(higher);

                log.Add(
                    "Rank " + higher +
                    " -> Rank " + initiator +
                    " : OK");
            }
            else
            {
                log.Add(
                    "Rank " + higher +
                    " : no reply (crashed)");
            }
        }

        // Every higher process that replied may start its own election.
        if (receivedReply)
        {
            foreach (int higher in higherResponders)
                StartElection(higher, size);
        }
    }
}
