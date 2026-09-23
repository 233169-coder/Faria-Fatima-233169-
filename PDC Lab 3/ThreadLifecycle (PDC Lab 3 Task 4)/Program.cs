using System;
using System.Threading;

class Program
{
    static void Worker()
    {
        Thread.Sleep(200);
    }

    static void Main()
    {
        Thread t = new Thread(Worker);

        Console.WriteLine(
            $"After creation: {t.ThreadState}");

        t.Start();

        Console.WriteLine(
            $"Immediately after Start(): {t.ThreadState}");
   
        Thread.Sleep(50);

        Console.WriteLine(
            $"While worker is sleeping: {t.ThreadState}");
        
        t.Join();

        Console.WriteLine(
            $"After Join() completes: {t.ThreadState}");
       
    }
}