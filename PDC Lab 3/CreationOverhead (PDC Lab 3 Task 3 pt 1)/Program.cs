using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int Iterations = 50;

    static void Main()
    {
        string childPath = Path.GetFullPath(
            @"..\ProcessChild\bin\Debug\net9.0\ProcessChild.exe");

        // Measure process creation
        Stopwatch processStopwatch = Stopwatch.StartNew();

        for (int i = 0; i < Iterations; i++)
        {
            Process p = Process.Start(childPath)!;
            p.WaitForExit();
        }

        processStopwatch.Stop();

        // Measure thread creation
        Stopwatch threadStopwatch = Stopwatch.StartNew();

        for (int i = 0; i < Iterations; i++)
        {
            Thread t = new Thread(() =>
            {
                // Trivial work
            });

            t.Start();
            t.Join();
        }

        threadStopwatch.Stop();

        double avgProcessMs =
            processStopwatch.Elapsed.TotalMilliseconds / Iterations;

        double avgThreadMs =
            threadStopwatch.Elapsed.TotalMilliseconds / Iterations;

        double ratio = avgProcessMs / avgThreadMs;

        Console.WriteLine("Process vs Thread Creation Overhead");
        Console.WriteLine("-----------------------------------");
        Console.WriteLine($"Iterations: {Iterations}");
        Console.WriteLine();
        Console.WriteLine($"Average process creation time: {avgProcessMs:F3} ms");
        Console.WriteLine($"Average thread creation time:  {avgThreadMs:F3} ms");
        Console.WriteLine($"Process / Thread ratio:         {ratio:F1}x");
        Console.WriteLine();
        Console.WriteLine(
            $"Process creation was {ratio:F1}x more expensive than thread creation.");
    }
}