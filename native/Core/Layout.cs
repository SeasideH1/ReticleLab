namespace Reticle.Core;

public sealed class Tile
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 96;
    public double Height { get; set; } = 48;
    public string Mode { get; set; } = "always";
    public string Style { get; set; } = "rounded";
}
public sealed class Preferences
{
    public int Schema { get; set; } = 1;
    public double TargetFps { get; set; } = 240;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double CrosshairSize { get; set; } = 7;
    public double CrosshairGap { get; set; } = 5;
    public double CrosshairThickness { get; set; } = 1.5;
    public string CrosshairStyle { get; set; } = "cross";
    public double CrosshairRadius { get; set; } = 6;
    public bool Dot { get; set; }
    public bool Antialias { get; set; } = true;
    public double CrosshairScale { get; set; } = 1;
    public bool CrosshairAutoFit { get; set; }
    public double FeedScale { get; set; } = 1;
    public double StatsScale { get; set; } = 1;
    public double InputScale { get; set; } = 1;
    public bool FeedAutoFit { get; set; } = true;
    public bool StatsAutoFit { get; set; } = true;
    public bool InputAutoFit { get; set; } = true;
    public string CrosshairColor { get; set; } = "#E6F0C3";
    public string KillColor { get; set; } = "#EF554B";
    public string HeadKillColor { get; set; } = "#EF554B";
    public string HitColor { get; set; } = "#FFFFFF";
    public string HeadColor { get; set; } = "#F3CF77";
    public double EffectSize { get; set; } = 15;
    public double EffectWidth { get; set; } = 2;
    public string FeedColor { get; set; } = "#F36E87";
    public string Accent { get; set; } = "#DF72BD";
    public string InputColor { get; set; } = "#D657AE";
    public double StatsOpacity { get; set; } = .3;
    public double InputOpacity { get; set; } = .32;
    public string FontFamily { get; set; } = "Noto Sans SC";
    public bool StatsRoundOnly { get; set; }
    public bool DimDuringFreeze { get; set; }
    public double DimOpacity { get; set; } = .2;
    public bool Snap { get; set; } = true;
    public bool OfficialKeys { get; set; }
    public bool BackgroundMouse { get; set; } = true;
    public double Grid { get; set; } = 10;
    public double FeedWidth { get; set; } = 210;
    public double FeedHeight { get; set; } = 40;
    public double FeedOpacity { get; set; } = .75;
    public double FeedRadius { get; set; } = 3;
    public double Enter { get; set; } = 260;
    public double Hold { get; set; } = 1500;
    public double Exit { get; set; } = 480;
    public double EffectDuration { get; set; } = 420;
    public double EffectDelay { get; set; }
    public double EffectEnter { get; set; }
    public double EffectHold { get; set; }
    // Null migrates legacy EffectDuration into the original fading phase.
    public double? EffectExit { get; set; }
    public double TrailLife { get; set; } = 1200;
    public double IdleCenter { get; set; } = 2000;
    public double Sensitivity { get; set; } = 1;
    public List<Tile> Stats { get; set; } = new();
    public List<Tile> Inputs { get; set; } = new();
    public static Preferences Default()
    {
        using var factory=typeof(Preferences).Assembly.GetManifestResourceStream("Reticle.Core.FactoryDefaults.json");
        if(factory!=null) {
            var defaults=System.Text.Json.JsonSerializer.Deserialize<Preferences>(factory);
            if(defaults!=null){defaults.Normalize();return defaults;}
        }
        var p = new Preferences();
        string[] ids = { "kda", "cash", "kills", "deaths", "assists", "hskill", "hshit", "damage", "totalDamage", "net" };
        string[] labels = { "K / D / A", "当前金钱", "回合击杀", "死亡", "助攻", "HS KILL", "命中 HS", "回合伤害", "记录期间伤害", "回合净增" };
        for (int i = 0; i < ids.Length; i++) p.Stats.Add(new Tile { Id = ids[i], Label = labels[i], X = i % 3 * 116, Y = i / 3 * 58, Width = 108, Mode = "inherit" });
        string[] keys = { "LeftShift", "W", "LeftControl", "A", "S", "D", "Space" };
        double[] xs = { 0, 122, 0, 72, 122, 172, 72 }, ys = { 0, 0, 50, 50, 50, 50, 100 };
        for (int i = 0; i < keys.Length; i++) p.Inputs.Add(new Tile { Id = keys[i], Label = keys[i].Replace("Left", "").ToUpperInvariant(), X = xs[i], Y = ys[i], Width = i == 6 ? 143 : i == 0 || i == 2 ? 65 : 43, Height = 40, Mode = "background" });
        p.Inputs.Add(new Tile { Id = "mouse", Label = "鼠标", X = 234, Width = 58, Height = 94, Mode = "background" });
        p.Inputs.Add(new Tile { Id = "trace", Label = "轨迹", X = 302, Width = 90, Height = 96, Mode = "always" });
        return p;
    }
    public void Normalize()
    {
        TargetFps=FramePacer.NormalizeFps(TargetFps);
        CrosshairSize = Finite(CrosshairSize, 0, 60, 7); CrosshairGap = Finite(CrosshairGap, 0, 60, 5);
        CrosshairThickness = Finite(CrosshairThickness, .5, 12, 1.5);
        CrosshairRadius = Finite(CrosshairRadius, .5, 60, 6);
        if(CrosshairStyle is not ("cross" or "circle" or "dot"))CrosshairStyle="cross";
        CrosshairScale = Finite(CrosshairScale, .5, 3, 1);
        FeedScale = Finite(FeedScale, .5, 3, 1); StatsScale = Finite(StatsScale, .5, 3, 1); InputScale = Finite(InputScale, .5, 3, 1);
        OffsetX = Finite(OffsetX, -200, 200, 0); OffsetY = Finite(OffsetY, -200, 200, 0);
        Grid = Finite(Grid, 1, 40, 10); DimOpacity = Finite(DimOpacity, 0, 1, .2);
        FeedWidth = Finite(FeedWidth, 80, 320, 210); FeedHeight = Finite(FeedHeight, 24, 64, 40);
        FeedOpacity = Finite(FeedOpacity, 0, 1, .75); FeedRadius = Finite(FeedRadius, 0, 20, 3);
        Enter = Finite(Enter, 100, 2000, 260); Hold = Finite(Hold, 0, 10000, 1500); Exit = Finite(Exit, 100, 2000, 480);
        EffectEnter=Finite(EffectEnter,0,2000,0);EffectHold=Finite(EffectHold,0,5000,0);
        EffectExit=Finite(EffectExit ?? Finite(EffectDuration,100,2000,420),0,2000,420);
        EffectDuration=EffectEnter+EffectHold+EffectExit.Value;
        TrailLife = Finite(TrailLife, 100, 5000, 1200);
        EffectDelay = Math.Round(Finite(EffectDelay, 0, 100, 0));
        EffectSize = Finite(EffectSize, 8, 35, 15); EffectWidth = Finite(EffectWidth, 1, 5, 2);
        StatsOpacity = Finite(StatsOpacity, 0, 1, .3); InputOpacity = Finite(InputOpacity, 0, 1, .32);
        IdleCenter = Finite(IdleCenter, 500, 5000, 2000); Sensitivity = Finite(Sensitivity, .05, 10, 1);
        Stats ??= new(); Inputs ??= new();
        Stats = Stats.Where(t => t != null).Take(16).ToList(); Inputs = Inputs.Where(t => t != null).Take(18).ToList();
        foreach (var t in Stats.Concat(Inputs)) {
            t.X = Finite(t.X, 0, 640, 0); t.Y = Finite(t.Y, 0, 400, 0);
            t.Width = Finite(t.Width, 24, 320, 96); t.Height = Finite(t.Height, 24, 160, 48);
            t.Label = (t.Label ?? "").Substring(0, Math.Min(t.Label?.Length ?? 0, 32));
        }
    }
    static double Finite(double v, double min, double max, double fallback) => double.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;
}
public static class Geometry
{
    // Game Bar's official sample assigns RequestedOpacity directly to XAML Opacity (0..1).
    public static double OverlayOpacity(double requested, bool foreground) =>
        foreground || !double.IsFinite(requested) ? 1 : Math.Clamp(requested, 0, 1);
    public static double LayoutScale(double width, double height, double designWidth, double designHeight, bool automatic, double manual) {
        double fit = automatic ? Math.Min(width / Math.Max(1, designWidth), height / Math.Max(1, designHeight)) : 1;
        return Math.Clamp(double.IsFinite(fit * manual) ? fit * manual : 1, .1, 6);
    }
    public static double QuartOut(double t) => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 4);
    public static double PhysicalOffset(double pixels, double scale) => pixels / (scale > 0 ? scale : 1);
    public static double SharpCenter(double centerPixels, int thicknessPixels) {
        double phase = thicknessPixels % 2 == 0 ? 0 : .5;
        return Math.Round(centerPixels - phase) + phase;
    }
    public static double SnapAxis(double position, double size, double extent, IEnumerable<(double start, double size)> peers, double grid, double tolerance)
    {
        var targets = new List<double> { 0, extent / 2, extent };
        foreach (var p in peers) targets.AddRange(new[] { p.start, p.start + p.size / 2, p.start + p.size });
        double best = Math.Round(position / grid) * grid, distance = tolerance;
        foreach (double target in targets) foreach (double anchor in new[] { 0d, size / 2, size })
        { double d = Math.Abs(position + anchor - target); if (d <= distance) { best = target - anchor; distance = d; } }
        return Math.Clamp(best, 0, Math.Max(0, extent - size));
    }
    public static bool Visible(Tile t, bool edit, bool roundOnly, bool freeze, bool pressed, bool moving) => edit || t.Mode switch {
        "hidden" => false, "round-start" => freeze, "inherit" => !roundOnly || freeze,
        "pressed" => pressed, "moving" => moving, _ => true
    };
}
public sealed class Trail
{
    public long Revision { get; private set; }
    public List<(double x, double y, long at)> Points { get; } = new();
    public double X { get; private set; } = 45;
    public double Y { get; private set; } = 48;
    public long LastMove { get; private set; }
    public double ViewScale {get;private set;}=1;
    public (double x,double y) Project(double x,double y)=>(45+(x-45)*ViewScale,48+(y-48)*ViewScale);
    public void Move(double dx, double dy, long now, bool replaceTail = false)
    {
        if (!double.IsFinite(dx) || !double.IsFinite(dy) || (dx == 0 && dy == 0)) return;
        if (Points.Count == 0) Points.Add((X, Y, now));
        if(!double.IsFinite(X+dx)||!double.IsFinite(Y+dy))return;
        X += dx; Y += dy; LastMove = now;
        if(replaceTail && Points.Count>1)Points[^1]=(X,Y,now);else Points.Add((X,Y,now));
        Revision++;
        double sx=Math.Abs(X-45),sy=Math.Abs(Y-48);
        if(sx>0)ViewScale=Math.Min(ViewScale,37/sx);
        if(sy>0)ViewScale=Math.Min(ViewScale,40/sy);
        if (Points.Count > 180) Points.RemoveRange(0, Points.Count - 180);
    }
    public bool Tick(long now, double life, double idle)
    {
        int before = Points.Count; Points.RemoveAll(p => now - p.at > life);
        if (LastMove != 0 && now - LastMove >= idle) { Reset(); return true; }
        if (before != Points.Count) Revision++;
        return before != Points.Count;
    }
    public void Reset() { Points.Clear(); X = 45; Y = 48; LastMove = 0; ViewScale=1; Revision++; }
}
