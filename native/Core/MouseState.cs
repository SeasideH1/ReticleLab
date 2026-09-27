namespace Reticle.Core;

public readonly record struct MouseMotion(long Sequence,long At,long X,long Y);
public sealed class MouseState
{
    public string Session { get; set; } = Guid.NewGuid().ToString("N");
    public long At { get; set; }
    public bool Enabled { get; set; }
    public string Error { get; set; } = "";
    public long X { get; set; }
    public long Y { get; set; }
    public bool Left { get; set; }
    public bool Right { get; set; }
    public bool Middle { get; set; }
    public long LeftAt { get; set; }
    public long RightAt { get; set; }
    public long MiddleAt { get; set; }
    public long WheelAt { get; set; }
    public List<MouseMotion> Motion { get; set; } = new();
    public bool Fresh(long now) => Enabled && now>=At && now-At<1000;
}

public sealed class MouseAccumulator
{
    long motionSequence;
    public MouseState State { get; } = new();
    public void Apply(int dx, int dy, ushort motionFlags, ushort buttons, long now)
    {
        // Relative HID motion works even when a game confines/recenters the desktop cursor.
        // Absolute tablet/touch coordinates are not relative mouse movement.
        if((motionFlags & 1)==0 && (dx!=0 || dy!=0)) {
            State.X+=dx;State.Y+=dy;
            var motion=new MouseMotion(++motionSequence,now,State.X,State.Y);
            // Bound high-polling-rate mice to a 125 Hz path history without losing
            // cumulative displacement; publishing remains 30 Hz, not disk I/O per frame.
            if(State.Motion.Count>0 && now/8==State.Motion[^1].At/8)State.Motion[^1]=motion;
            else State.Motion.Add(motion);
            if(State.Motion.Count>128)State.Motion.RemoveRange(0,State.Motion.Count-128);
        }
        if((buttons & 1)!=0) {State.Left=true;State.LeftAt=now;}
        if((buttons & 2)!=0) State.Left=false;
        if((buttons & 4)!=0) {State.Right=true;State.RightAt=now;}
        if((buttons & 8)!=0) State.Right=false;
        if((buttons & 16)!=0) {State.Middle=true;State.MiddleAt=now;}
        if((buttons & 32)!=0) State.Middle=false;
        if((buttons & 0xC00)!=0) State.WheelAt=now;
    }
}
