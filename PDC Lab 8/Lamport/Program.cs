
using System;
using System.Collections.Generic;
using System.Linq;
using MPI;

class EventRecord
{
    public int Rank;
    public int Clock;
    public string Type;
    public int Peer;
    public int MessageId;

    public override string ToString()
    {
        return "Rank " + Rank +
               " | Clock " + Clock +
               " | " + Type +
               " | Peer " + Peer +
               " | Message " + MessageId;
    }
}

class Program
{
    const int LogTag = 100;
    const int MessageTag = 200;

    static int logicalClock = 0;
    static List<EventRecord> localLog = new List<EventRecord>();

    static void Record(int rank, string type, int peer, int messageId)
    {
        localLog.Add(new EventRecord
        {
            Rank = rank,
            Clock = logicalClock,
            Type = type,
            Peer = peer,
            MessageId = messageId
        });
    }

    static void LocalEvent(int rank)
    {
        logicalClock++;
        Record(rank, "LOCAL", -1, -1);
    }

    static void SendMessage(Intracommunicator comm, int destination,
                            int messageId)
    {
        // Lamport send event: increment the clock before sending.
        logicalClock++;
        Record(comm.Rank, "SEND", destination, messageId);

        comm.Send(logicalClock, destination, MessageTag);
    }

    static void ReceiveMessage(Intracommunicator comm, int source,
                               int messageId)
    {
        int senderClock = comm.Receive<int>(source, MessageTag);

        // Lamport receive rule: max(local clock, sender clock) + 1.
        logicalClock = Math.Max(logicalClock, senderClock) + 1;
        Record(comm.Rank, "RECEIVE", source, messageId);
    }

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;
            int rank = comm.Rank;
            int size = comm.Size;

            if (size < 2)
            {
                if (rank == 0)
                    Console.WriteLine("Run with at least 2 ranks.");
                return;
            }

            // Create local events on every rank.
            LocalEvent(rank);

            // Rank 0 sends a message to each other rank.
            if (rank == 0)
            {
                for (int destination = 1; destination < size; destination++)
                    SendMessage(comm, destination, destination);
            }
            else
            {
                ReceiveMessage(comm, 0, rank);
                LocalEvent(rank);
            }

            // Each rank serializes its log as a string.
            string serializedLog = string.Join("\n",
                localLog.Select(e =>
                    e.Rank + "|" + e.Clock + "|" + e.Type + "|" +
                    e.Peer + "|" + e.MessageId));

            if (rank == 0)
            {
                List<EventRecord> merged = new List<EventRecord>();
                merged.AddRange(localLog);

                for (int source = 1; source < size; source++)
                {
                    string receivedLog = comm.Receive<string>(source, LogTag);

                    foreach (string line in receivedLog.Split(
                        new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] parts = line.Split('|');
                        merged.Add(new EventRecord
                        {
                            Rank = int.Parse(parts[0]),
                            Clock = int.Parse(parts[1]),
                            Type = parts[2],
                            Peer = int.Parse(parts[3]),
                            MessageId = int.Parse(parts[4])
                        });
                    }
                }

                // Merge and sort logs by logical clock, then rank.
                List<EventRecord> sorted = merged
                    .OrderBy(e => e.Clock)
                    .ThenBy(e => e.Rank)
                    .ToList();

                int violations = 0;

                foreach (EventRecord send in sorted.Where(e => e.Type == "SEND"))
                {
                    EventRecord receive = sorted.FirstOrDefault(e =>
                        e.Type == "RECEIVE" &&
                        e.MessageId == send.MessageId &&
                        e.Rank == send.Peer &&
                        e.Peer == send.Rank);

                    if (receive == null || send.Clock >= receive.Clock)
                        violations++;
                }

                Console.WriteLine();
                Console.WriteLine("Lamport Logical Clock Results");
                Console.WriteLine("Ranks: " + size);
                Console.WriteLine("Events: " + sorted.Count);
                Console.WriteLine("Messages: " + sorted.Count(e => e.Type == "SEND"));
                Console.WriteLine("Causal violations: " + violations);
                Console.WriteLine("Consistent: " + (violations == 0 ? "YES" : "NO"));
                Console.WriteLine();
                Console.WriteLine("Merged event log:");

                foreach (EventRecord e in sorted)
                    Console.WriteLine(e);

                EventRecord chosenSend = sorted.First(e => e.Type == "SEND");
                EventRecord chosenReceive = sorted.First(e =>
                    e.Type == "RECEIVE" &&
                    e.MessageId == chosenSend.MessageId);

                Console.WriteLine();
                Console.WriteLine("Example message:");
                Console.WriteLine("Sender rank: " + chosenSend.Rank);
                Console.WriteLine("Send clock: " + chosenSend.Clock);
                Console.WriteLine("Receiver rank: " + chosenReceive.Rank);
                Console.WriteLine("Receive clock: " + chosenReceive.Clock);
            }
            else
            {
                comm.Send(serializedLog, 0, LogTag);
            }
        }
    }
}
