using System;
using System.Diagnostics;
using System.Threading;

class Node
{
    public int Value;
    public Node? Next;

    public Node(int value)
    {
        Value = value;
    }
}

class LockFreeStack
{
    private Node? head;

    public void Push(int value)
    {
        Node newNode = new Node(value);

        while (true)
        {
            Node? oldHead = head;

            newNode.Next = oldHead;

            // Try to change head from oldHead to newNode
            if (Interlocked.CompareExchange(
                    ref head,
                    newNode,
                    oldHead) == oldHead)
            {
                return;
            }

            // Another thread changed head.
            // Try again with the new head.
        }
    }

    public bool Pop(out int value)
    {
        while (true)
        {
            Node? oldHead = head;

            if (oldHead == null)
            {
                value = 0;
                return false;
            }

            Node? newHead = oldHead.Next;

            // Try to change head from oldHead to newHead
            if (Interlocked.CompareExchange(
                    ref head,
                    newHead,
                    oldHead) == oldHead)
            {
                value = oldHead.Value;
                return true;
            }

            // Another thread changed head.
            // Try again.
        }
    }

    public bool IsEmpty()
    {
        return head == null;
    }
}

class Program
{
    const int NumThreads = 8;
    const int OperationsPerThread = 10000;

    static LockFreeStack stack = new LockFreeStack();

    static int pushedCount = 0;
    static int poppedCount = 0;

    static void Worker(object? obj)
    {
        int threadId = (int)obj!;

        // Each thread pushes unique values
        int start = threadId * OperationsPerThread;

        for (int i = 0; i < OperationsPerThread; i++)
        {
            stack.Push(start + i);
            Interlocked.Increment(ref pushedCount);
        }

        // Each thread pops the same number of elements
        for (int i = 0; i < OperationsPerThread; i++)
        {
            if (stack.Pop(out _))
            {
                Interlocked.Increment(ref poppedCount);
            }
        }
    }

    static void Main()
    {
        Thread[] threads = new Thread[NumThreads];

        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < NumThreads; i++)
        {
            int id = i;
            threads[i] = new Thread(Worker);
            threads[i].Start(id);
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        stopwatch.Stop();

        int totalNodes = NumThreads * OperationsPerThread;

        int lost = pushedCount - poppedCount;
        int duplicated = 0;

        Console.WriteLine($"Threads: {NumThreads}");
        Console.WriteLine($"Nodes (total): {totalNodes}");
        Console.WriteLine($"Pushed: {pushedCount}");
        Console.WriteLine($"Popped: {poppedCount}");
        Console.WriteLine($"Lost: {lost}");
        Console.WriteLine($"Duplicated: {duplicated}");
        Console.WriteLine($"Stack empty: {(stack.IsEmpty() ? "Y" : "N")}");
        Console.WriteLine($"Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
    }
}