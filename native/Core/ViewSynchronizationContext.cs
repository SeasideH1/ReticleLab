namespace Reticle.Core;

// Bound to one view's dispatcher, never shared between Game Bar windows.
public sealed class ViewSynchronizationContext(Action<Action> enqueue, Func<bool> hasAccess) : SynchronizationContext
{
    public override void Post(SendOrPostCallback callback, object? state) => enqueue(() => {
        var previous = Current;
        try { SetSynchronizationContext(this); callback(state); }
        finally { SetSynchronizationContext(previous); }
    });
    public override void Send(SendOrPostCallback callback, object? state) {
        if (!hasAccess()) throw new NotSupportedException("Synchronous cross-view dispatch is not supported.");
        callback(state);
    }
    public override SynchronizationContext CreateCopy() => this;
}
