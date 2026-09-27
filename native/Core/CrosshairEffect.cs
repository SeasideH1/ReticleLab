namespace Reticle.Core;

public readonly record struct CrosshairEffect(long StartsAt, bool Kill, bool Head)
{
    public static CrosshairEffect Schedule(long now, double delay, bool kill, bool head) =>
        new(now + (long)Math.Round(double.IsFinite(delay) ? Math.Clamp(delay,0,100) : 0),kill,head);
    public bool Expired(long now,double duration) => now-StartsAt>duration;
    // Pending effects must stay invisible, rather than clamping a negative phase to zero.
    public double? Progress(long now,double duration) => now<StartsAt || Expired(now,duration)
        ? null : Math.Clamp((now-StartsAt)/Math.Max(1,duration),0,1);
    public double Opacity(long now,double enter,double hold,double exit) {
        double elapsed=now-StartsAt;
        if(elapsed<0 || elapsed>=enter+hold+exit)return 0;
        if(enter>0 && elapsed<enter)return Geometry.QuartOut(elapsed/enter);
        if(elapsed<enter+hold)return 1;
        // Preserve the existing linear fading appearance when old duration settings migrate.
        return exit>0?1-Math.Clamp((elapsed-enter-hold)/exit,0,1):0;
    }
}
