using Reticle.Core;
using Windows.UI.Core;

namespace Reticle.Widget;
static class UiThread
{
    public static void Attach(CoreDispatcher dispatcher) {
        if (!dispatcher.HasThreadAccess) throw new InvalidOperationException("Attach the context on its own view thread.");
        SynchronizationContext.SetSynchronizationContext(new ViewSynchronizationContext(
            callback => { _ = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => callback()); },
            () => dispatcher.HasThreadAccess));
    }
    public static async Task Run(CoreDispatcher dispatcher, Func<Task> action, Action<Exception>? report = null) {
        async Task Execute() {
            Attach(dispatcher);
            try { await action(); }
            catch (Exception e) { Store.Log(e); report?.Invoke(e); }
        }
        if (dispatcher.HasThreadAccess) { await Execute(); return; }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                try { await Execute(); completion.TrySetResult(); }
                catch (Exception e) { completion.TrySetException(e); }
            });
            await completion.Task;
        } catch (Exception e) { Store.Log(e); }
    }
}
