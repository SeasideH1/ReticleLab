namespace Reticle.Core;

// Coalesces changes; a late frame consumes the latest state without catch-up bursts.
public sealed class FramePacer
{
    double last=double.NegativeInfinity;
    public bool Pending {get;private set;}
    public static double NormalizeFps(double fps)=>double.IsFinite(fps)?Math.Clamp(Math.Round(fps),1,240):240;
    public static TimeSpan Interval(double fps)=>TimeSpan.FromMilliseconds(1000/NormalizeFps(fps));
    public void Request()=>Pending=true;
    public bool Take(double now,double fps,bool animated) {
        if((!Pending&&!animated)||now-last<1000/NormalizeFps(fps))return false;
        last=now;Pending=false;return true;
    }
}
