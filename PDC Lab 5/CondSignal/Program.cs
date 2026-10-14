using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int DelayMs = 1000;

    static string mode = "cond";
    static int numWorkers = 1;
    static bool signalFirst = false;

    static readonly object gate = new object();

    static bool dataReady = false;
    static int sharedData = 0;

    static int[] waitCalls = null!;
    static int[] received = null!;

    static void Worker(int id)
    {
        if (mode == "spin")
        {
            while (true)
            {
                bool ready;

                lock (gate)
                {
                    ready = dataReady;
                }

                if (ready)
                    break;
            }

            lock (gate)
            {
                received[id] = sharedData;
            }
        }
        else
        {
            lock (gate)
            {
                while (!dataReady)
                {
                    waitCalls[id]++;
                    Monitor.Wait(gate);
                }

                received[id] = sharedData;
            }
        }
    }

    static void Publish()
    {
        lock (gate)
        {
            sharedData = 42;
            dataReady = true;

            if (numWorkers == 1)
                Monitor.Pulse(gate);
            else
                Monitor.PulseAll(gate);
        }
    }

    static void Main(string[] args)
    {
        if (args.Length > 0)
            mode = args[0];

        if (args.Length > 1)
            numWorkers = int.Parse(args[1]);

        if (args.Length > 2)
            signalFirst = args[2] == "signal-first";

        waitCalls = new int[numWorkers];
        received = new int[numWorkers];

        Process proc = Process.GetCurrentProcess();

        TimeSpan cpu0 = proc.TotalProcessorTime;

        Stopwatch sw = Stopwatch.StartNew();

        Thread[] threads = new Thread[numWorkers];

        if (signalFirst)
        {
            Publish();
        }

        for (int i = 0; i < numWorkers; i++)
        {
            int id = i;

            threads[i] = new Thread(() => Worker(id));
            threads[i].Start();
        }

        if (!signalFirst)
        {
            Thread.Sleep(DelayMs);
            Publish();
        }

        foreach (Thread t in threads)
            t.Join();

        sw.Stop();

        proc.Refresh();

        double cpuMs =
            (proc.TotalProcessorTime - cpu0).TotalMilliseconds;

        for (int i = 0; i < numWorkers; i++)
        {
            Console.WriteLine(
                $"[worker {i}] blocked in wait {waitCalls[i]} time(s), " +
                $"received {received[i]}"
            );
        }

        Console.WriteLine(
            $"mode={mode} workers={numWorkers} delay={DelayMs} ms " +
            $"wall={sw.Elapsed.TotalMilliseconds:F2} ms " +
            $"cpu={cpuMs:F2} ms"
        );
    }
}