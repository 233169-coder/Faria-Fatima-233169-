
using System;
using System.Collections.Generic;
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
            string processor = MPI.Environment.ProcessorName;

            string report =
                "Hello from rank " + rank +
                " of " + size + " on " + processor;

            // Each rank prints its own report
            Console.WriteLine(report);

            // Collect reports at rank 0
            if (rank == 0)
            {
                List<string> reports = new List<string>();
                reports.Add(report);

                for (int source = 1; source < size; source++)
                {
                    string received =
                        comm.Receive<string>(source, 100);

                    reports.Add(received);
                }

                Console.WriteLine("\nOrdered report:");

                foreach (string line in reports)
                {
                    Console.WriteLine(line);
                }
            }
            else
            {
                comm.Send(report, 0, 100);
            }

            Console.WriteLine(
                "Logical cores: " +
                System.Environment.ProcessorCount);
        }
    }
}
