using System.Collections.Concurrent;
using Reticle.Core;

static class ViewContextSmoke
{
    public static async Task Verify()
    {
        async Task TestView() {
            using var queue = new BlockingCollection<Action>();
            var done = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() => {
                int owner = Environment.CurrentManagedThreadId;
                var context = new ViewSynchronizationContext(queue.Add, () => Environment.CurrentManagedThreadId == owner);
                context.Post(async _ => {
                    try {
                        await Task.Delay(15);
                        if (Environment.CurrentManagedThreadId != owner || SynchronizationContext.Current != context) throw new Exception("First continuation escaped its view.");
                        await Task.Run(() => Thread.Sleep(10));
                        if (Environment.CurrentManagedThreadId != owner) throw new Exception("Second continuation escaped its view.");
                        try { await Task.FromException(new IOException("simulated save failure")); }
                        catch (IOException) { if (Environment.CurrentManagedThreadId != owner) throw new Exception("Error UI escaped its view."); }
                        done.TrySetResult(owner);
                    } catch (Exception e) { done.TrySetException(e); }
                    finally { queue.CompleteAdding(); }
                }, null);
                foreach (var action in queue.GetConsumingEnumerable()) action();
            }) { IsBackground = true };
            thread.Start();
            await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
            thread.Join();
        }
        await Task.WhenAll(TestView(), TestView());
        Console.WriteLine("PASS: two independent view dispatchers retain await continuations and exception UI on their owner thread.");
    }
}
