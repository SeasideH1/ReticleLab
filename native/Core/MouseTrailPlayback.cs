namespace Reticle.Core;

// Display only: preserves ordered sample turns, interpolates between real samples,
// never predicts movement or generates OS input. The 40 ms buffer absorbs 30 Hz IPC.
public sealed class MouseTrailPlayback
{
    public const int BufferMilliseconds=40;
    readonly Queue<MouseMotion> pending=new();
    string? session;
    long sequence;
    MouseMotion anchor;
    double x,y;
    bool provisional;
    public bool Pending=>pending.Count>0;
    public void Reset(){pending.Clear();session=null;sequence=0;anchor=default;x=y=0;provisional=false;}
    public bool Accept(MouseState next) {
        if(session!=next.Session) {
            Reset();session=next.Session;
            sequence=next.Motion.Count>0?next.Motion[^1].Sequence:0;
            anchor=new MouseMotion(sequence,next.At,next.X,next.Y);x=next.X;y=next.Y;
            return false; // Never replay movement from before widget activation.
        }
        bool added=false;
        foreach(var sample in next.Motion) {
            if(sample.Sequence<=sequence)continue;
            sequence=sample.Sequence;
            if(sample.At<anchor.At)continue;
            pending.Enqueue(sample);added=true;
        }
        while(pending.Count>256)pending.Dequeue();
        // A hot-updated receiver may temporarily still publish the older schema.
        if(next.Motion.Count==0 && (next.X!=anchor.X || next.Y!=anchor.Y) && !Pending && next.At>anchor.At) {
            pending.Enqueue(new MouseMotion(++sequence,next.At,next.X,next.Y));added=true;
        }
        return added;
    }
    public bool Advance(Trail trail,long now,double sensitivity) {
        bool changed=false;long target=now-BufferMilliseconds;
        bool Move(double nextX,double nextY) {
            if(nextX==x && nextY==y)return false;
            trail.Move((nextX-x)*sensitivity,(nextY-y)*sensitivity,now,provisional);
            x=nextX;y=nextY;changed=true;return true;
        }
        while(pending.TryPeek(out var sample) && sample.At<=target) {
            pending.Dequeue();Move(sample.X,sample.Y);anchor=sample;provisional=false;
        }
        if(pending.TryPeek(out var next) && target>=anchor.At && next.At>anchor.At) {
            double progress=Math.Clamp((target-anchor.At)/(double)(next.At-anchor.At),0,1);
            if(Move(anchor.X+(next.X-anchor.X)*progress,anchor.Y+(next.Y-anchor.Y)*progress))provisional=true;
        }
        return changed;
    }
}
