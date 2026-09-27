using Geometry = Reticle.Core.Geometry;
using Reticle.Core;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Reticle.Widget;
static class Visuals
{
    static readonly string[] EmblemPaths = {
                "M3 1l9 7 18 21 7 10-11-8L7 10zM37 1l-9 7L10 29 3 39l11-8 19-21z",
                "F0 M20 6C11 6 6 13 8 21c1 5 5 7 8 8v5h3v-4h2v4h3v-5c4-1 7-4 8-8 2-8-3-15-12-15zm-8 10 7 4-3 4-5-3zm16 0 1 5-5 3-3-4zm-8 7 3 4h-6z"
    };
    static UIElement Emblem() {
        var canvas = new Canvas { Width=40, Height=40 };
        // Geometry belongs to its XAML owner. Cache only text, never reattach a parsed Path.Data.
        foreach(var path in EmblemPaths) canvas.Children.Add((Windows.UI.Xaml.Shapes.Path)Windows.UI.Xaml.Markup.XamlReader.Load(
            "<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Fill='White' Data='" + path + "'/>"));
        return new Viewbox { Width=25, Height=25, Child=canvas };
    }
    public static Color Color(string hex) {
        try { var s = hex.TrimStart('#'); return Windows.UI.Color.FromArgb(255, Convert.ToByte(s.Substring(0, 2), 16), Convert.ToByte(s.Substring(2, 2), 16), Convert.ToByte(s.Substring(4, 2), 16)); }
        catch { return Colors.White; }
    }
    public static SolidColorBrush Brush(string hex, double opacity = 1) => new(Color(hex)) { Opacity = opacity };
    public static FontFamily Font(Preferences p, string text, bool bold) => p.FontFamily == "Stratum2"
        ? new FontFamily("ms-appx:///Fonts/Stratum2.ttf#Stratum2")
        : text.All(c => c < 128)
            ? new FontFamily(bold ? "ms-appx:///Fonts/notosans-bold.ttf#Noto Sans" : "ms-appx:///Fonts/notosans-regular.ttf#Noto Sans")
            : new FontFamily(bold ? "ms-appx:///Fonts/notosanssc-bold.ttf#Noto Sans SC Bold" : "ms-appx:///Fonts/notosanssc-regular.ttf#Noto Sans SC Regular");
    public static TextBlock Text(string text, double size, string color, Preferences p) => new() {
        Text = text, FontSize = size, Foreground = Brush(color), FontFamily = Font(p,text,size>=18),
        TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false
    };
    public static void Place(Canvas canvas, UIElement child, double x, double y) { Canvas.SetLeft(child, x); Canvas.SetTop(child, y); canvas.Children.Add(child); }
    public static void Line(Canvas c, double x1, double y1, double x2, double y2, double thickness, string color, double opacity = 1) {
        c.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, StrokeThickness = thickness, Stroke = Brush(color, opacity), IsHitTestVisible = false, UseLayoutRounding = false });
    }
    public static string Stat(string id, Snapshot s) {
        string N(int? n) => n?.ToString() ?? "—";
        var round = s.DisplayRound();
        return id switch {
            "kda" => $"{N(s.Kills)} / {N(s.Deaths)} / {N(s.Assists)}", "cash" => s.Money.HasValue ? "$" + s.Money : "—",
            "kills" => N(round.Kills), "deaths" => N(s.Deaths), "assists" => N(s.Assists),
            "hskill" => round.Kills > 0 && round.Headshots.HasValue && round.Headshots <= round.Kills ? (100d * round.Headshots / round.Kills)?.ToString("0.0") + "%" : "—",
            "damage" => N(round.Damage), "totalDamage" => N(s.RecordedDamage),
            "net" => round.NetMoney.HasValue ? round.NetMoney.Value.ToString("+0;-0;0") : "—", _ => "—"
        };
    }
    public static void Crosshair(Canvas c, Preferences p, double scale, double sizeScale = 1, Windows.Foundation.Point? center = null)
    {
        double x = (double.IsNaN(c.Width) ? c.ActualWidth : c.Width) / 2 + p.OffsetX / scale, y = (double.IsNaN(c.Height) ? c.ActualHeight : c.Height) / 2 + p.OffsetY / scale;
        if(center.HasValue) { x=center.Value.X+p.OffsetX/scale; y=center.Value.Y+p.OffsetY/scale; }
        double w = p.CrosshairThickness * sizeScale / scale, size = p.CrosshairSize * sizeScale / scale, gap = p.CrosshairGap * sizeScale / scale;
        // Keep symmetry around the geometric center. Sharp mode snaps a center once, never the arms independently.
        if (!p.Antialias) {
            int thickness = (int)Math.Max(1, Math.Round(w * scale));
            x = Geometry.SharpCenter(x * scale, thickness) / scale; y = Geometry.SharpCenter(y * scale, thickness) / scale;
            w = thickness / scale; size = Math.Round(size * scale) / scale; gap = Math.Round(gap * scale) / scale;
        }
        if(p.CrosshairStyle is "circle" or "dot") {
            double radius=p.CrosshairRadius*sizeScale/scale;
            if(!p.Antialias)radius=Math.Max(.5,Math.Round(radius*scale*2)/2)/scale;
            var circle=new Ellipse {Width=radius*2,Height=radius*2,UseLayoutRounding=false,IsHitTestVisible=false};
            if(p.CrosshairStyle=="dot")circle.Fill=Brush(p.CrosshairColor);
            else {circle.Stroke=Brush(p.CrosshairColor);circle.StrokeThickness=Math.Min(w,radius);}
            Place(c,circle,x-radius,y-radius);
        } else {
            Line(c, x - gap - size, y, x - gap, y, w, p.CrosshairColor);
            Line(c, x + gap, y, x + gap + size, y, w, p.CrosshairColor);
            Line(c, x, y - gap - size, x, y - gap, w, p.CrosshairColor);
            Line(c, x, y + gap, x, y + gap + size, w, p.CrosshairColor);
        }
        if (p.Dot && p.CrosshairStyle!="dot") Place(c, new Ellipse { Width = w * 2, Height = w * 2, Fill = Brush(p.CrosshairColor), UseLayoutRounding=false }, x - w, y - w);
    }
    public static void Hit(Canvas c, Preferences p, double scale, double progress, bool kill, bool head, double sizeScale = 1, Windows.Foundation.Point? center = null, double? opacity = null)
    {
        double x = (double.IsNaN(c.Width) ? c.ActualWidth : c.Width) / 2 + p.OffsetX / scale, y = (double.IsNaN(c.Height) ? c.ActualHeight : c.Height) / 2 + p.OffsetY / scale;
        if(center.HasValue) { x=center.Value.X+p.OffsetX/scale; y=center.Value.Y+p.OffsetY/scale; }
        double a = (p.EffectSize + 5 * Geometry.QuartOut(progress)) * sizeScale / scale, b = a * .47;
        string color = kill ? head ? p.HeadKillColor : p.KillColor : head ? p.HeadColor : p.HitColor;
        foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 })
            Line(c, x + sx * a, y + sy * a, x + sx * b, y + sy * b, p.EffectWidth * sizeScale / scale, color, opacity ?? 1 - progress);
    }
    public sealed class FeedVisual
    {
        static readonly double[] times={0,.25,.5,.8,1},xs={.15,1.85,1.5,1.02,1},ys={.045,.045,.24,.92,1};
        readonly Border background;
        readonly Grid row;
        readonly ScaleTransform transform = new();
        readonly SolidColorBrush brush = new();
        public FeedVisual(Preferences p, KillNotice notice) {
            background = new Border { Width=p.FeedWidth, Height=p.FeedHeight, Background=brush,
                CornerRadius=new CornerRadius(p.FeedRadius), RenderTransformOrigin=new Windows.Foundation.Point(.5,.5),
                RenderTransform=transform, IsHitTestVisible=false };
        row = new Grid { Width = p.FeedWidth - 20, Height = p.FeedHeight, IsHitTestVisible = false };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var emblem=(FrameworkElement)Emblem();
        emblem.HorizontalAlignment=HorizontalAlignment.Left;emblem.VerticalAlignment=VerticalAlignment.Center;
        row.Children.Add(emblem);
        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(Text(notice.Weapon, 16, "#FFFFFF", p)); words.Children.Add(Text(notice.WeaponSource, 9, "#FFFFFF", p));
        Grid.SetColumn(words, 1); row.Children.Add(words);
        var damagePanel=new StackPanel{Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};
        damagePanel.Visibility=notice.Damage.HasValue?Visibility.Visible:Visibility.Collapsed;
        var damage = Text(notice.Damage?.ToString() ?? "—", 20, "#FFFFFF", p);
        damage.HorizontalAlignment=HorizontalAlignment.Right;damagePanel.Children.Add(damage);
        var label=Text(notice.DamageLabel,9,"#FFFFFF",p);label.HorizontalAlignment=HorizontalAlignment.Right;damagePanel.Children.Add(label);
        Grid.SetColumn(damagePanel, 2); row.Children.Add(damagePanel);

        }
        public void Draw(Canvas c, FrameBatch frame, Preferences p, double elapsed, double y) {
        double sx = 1, sy = 1, opacity = p.FeedOpacity;
        Color color = Color(p.FeedColor); double textAlpha = 1;
        if (elapsed < p.Enter) {
            double q = Geometry.QuartOut(elapsed / p.Enter);
            int i = 1; while (i < 4 && q > times[i]) i++;
            double u = (q - times[i-1]) / (times[i] - times[i-1]);
            sx = xs[i-1] + (xs[i] - xs[i-1]) * u; sy = ys[i-1] + (ys[i] - ys[i-1]) * u;
            color = Mix(Colors.White, color, q); opacity *= Math.Min(1, q * 4); textAlpha = Math.Clamp((q - .5) * 2, 0, 1);
        } else if (elapsed > p.Enter + p.Hold) {
            double q = Geometry.QuartOut((elapsed - p.Enter - p.Hold) / p.Exit);
            sy = 1 + (.025 - 1) * q; opacity *= 1 - q; textAlpha = 1 - q;
            color = Mix(color, Colors.White, q);
        }
            double x = ((double.IsNaN(c.Width) ? c.ActualWidth : c.Width) - p.FeedWidth) / 2;
            transform.ScaleX=sx;transform.ScaleY=sy;brush.Color=color;
            background.Opacity=opacity;row.Opacity=textAlpha;
            frame.Use(background,x,y);frame.Use(row,x+10,y);
        }
    }
    static Color Mix(Color a, Color b, double t) => Windows.UI.Color.FromArgb(255, (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
}
