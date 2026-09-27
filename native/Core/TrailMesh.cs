namespace Reticle.Core;

// Fixed birth-time buckets keep segments in the same retained path as they age.
// Only opacity changes between input/expiry updates, not path membership.
public sealed class TrailMesh
{
    public sealed class Band
    {
        public List<(double x,double y)> Points { get; } = new();
        public long FadeAt { get; internal set; }
        public double Opacity(long now,double life) => Points.Count < 2 ? 0 :
            Math.Clamp(1 - Math.Max(0,now-FadeAt) / Math.Max(1,life),0,1);
    }
    public Band[] Bands { get; } = Enumerable.Range(0,12).Select(_=>new Band()).ToArray();
    long revision = -1;
    double priorLife;
    public bool Update(Trail trail,double life)
    {
        if (revision == trail.Revision && priorLife == life) return false;
        revision=trail.Revision; priorLife=life;
        foreach(var band in Bands) { band.Points.Clear(); band.FadeAt=0; }
        double span=Math.Max(1,life/(Bands.Length-1));
        int previousBand=-1;
        for(int i=1;i<trail.Points.Count;i++) {
            var a=trail.Points[i-1];var b=trail.Points[i];
            long bucket=(long)Math.Floor(b.at/span);
            int index=(int)((bucket%Bands.Length+Bands.Length)%Bands.Length);
            var band=Bands[index];
            if(index!=previousBand)band.Points.Add(trail.Project(a.x,a.y));
            band.Points.Add(trail.Project(b.x,b.y));
            band.FadeAt=(long)Math.Ceiling((bucket+1)*span);
            previousBand=index;
        }
        return true;
    }
}
