using Reticle.Core;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Geometry=Reticle.Core.Geometry;

namespace Reticle.Widget;

sealed partial class OverlayPage
{
    readonly Dictionary<Tile,TileVisual> tileVisuals=new();
    readonly List<HitVisual> hitPool=new();
    readonly FramePacer framePacer=new();
    FrameBatch frame=null!;

    sealed class HitVisual {
        public Canvas Root {get;}=new(){IsHitTestVisible=false,UseLayoutRounding=false};
        readonly Line[] arms=new Line[4];
        readonly SolidColorBrush brush=new();
        public HitVisual() {
            for(int i=0;i<4;i++){arms[i]=new Line{Stroke=brush,IsHitTestVisible=false,UseLayoutRounding=false};Root.Children.Add(arms[i]);}
        }
        public void Update(Preferences p,double dpi,double scale,Point center,CrosshairEffect effect,long now) {
            double progress=effect.Progress(now,p.EffectDuration)??0;
            double x=center.X+p.OffsetX/dpi,y=center.Y+p.OffsetY/dpi;
            double a=(p.EffectSize+5*Geometry.QuartOut(progress))*scale/dpi,b=a*.47;
            brush.Color=Visuals.Color(effect.Kill?(effect.Head?p.HeadKillColor:p.KillColor):(effect.Head?p.HeadColor:p.HitColor));
            Root.Opacity=effect.Opacity(now,p.EffectEnter,p.EffectHold,p.EffectExit??p.EffectDuration);
            for(int i=0;i<4;i++) {
                int sx=i<2?-1:1,sy=i%2==0?-1:1;var line=arms[i];
                line.X1=x+sx*a;line.Y1=y+sy*a;line.X2=x+sx*b;line.Y2=y+sy*b;line.StrokeThickness=p.EffectWidth*scale/dpi;
            }
        }
    }

    sealed class TileVisual {
        public Border Box {get;}
        readonly Tile tile;
        readonly SolidColorBrush background,border;
        readonly SolidColorBrush? leftBrush,rightBrush,middleBrush;
        public TextBlock? Value {get;}
        readonly Canvas? trace;
        readonly Ellipse? cursor;
        // Each band is one retained polyline instead of one XAML element per sample.
        readonly Polyline[] bands=new Polyline[12];
        readonly TrailMesh mesh=new();
        public TileVisual(OverlayPage owner,Tile t,bool stats) {
            tile=t;var p=owner.p;UIElement child;
            if(stats) {
                var body=new StackPanel{Margin=new Thickness(8,2,8,2)};
                body.Children.Add(Visuals.Text(t.Id=="hskill"&&t.Label=="HS KILL"?"回合 HS KILL":t.Label,10,p.Accent,p));
                Value=Visuals.Text("—",t.Id=="kda"?18:22,p.Accent,p);body.Children.Add(Value);child=body;
            } else if(t.Id=="trace") {
                trace=new Canvas{Width=90,Height=96,Clip=new RectangleGeometry{Rect=new Rect(0,0,90,96)},IsHitTestVisible=false};
                Visuals.Line(trace,35,48,55,48,.5,"#FFFFFF",.2);Visuals.Line(trace,45,38,45,58,.5,"#FFFFFF",.2);
                var brush=Visuals.Brush(p.InputColor);
                for(int i=0;i<bands.Length;i++) {
                    bands[i]=new Polyline{Points=new PointCollection(),Stroke=brush,StrokeThickness=1.5,Opacity=(i+.5)/bands.Length,IsHitTestVisible=false,UseLayoutRounding=false};
                    trace.Children.Add(bands[i]);
                }
                cursor=new Ellipse{Width=4,Height=4,Fill=brush,IsHitTestVisible=false};trace.Children.Add(cursor);child=new Viewbox{Child=trace};
            } else if(t.Id=="mouse") {
                var mouse=new Grid();mouse.ColumnDefinitions.Add(new ColumnDefinition());mouse.ColumnDefinitions.Add(new ColumnDefinition());
                leftBrush=Visuals.Brush(p.InputColor);rightBrush=Visuals.Brush(p.InputColor);middleBrush=Visuals.Brush("#EEEEEE");
                var l=new Border{Height=t.Height*.43,VerticalAlignment=VerticalAlignment.Top,Background=leftBrush};mouse.Children.Add(l);
                var r=new Border{Height=t.Height*.43,VerticalAlignment=VerticalAlignment.Top,Background=rightBrush};Grid.SetColumn(r,1);mouse.Children.Add(r);
                var m=new Border{Width=4,Height=15,CornerRadius=new CornerRadius(2),Background=middleBrush,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};Grid.SetColumnSpan(m,2);mouse.Children.Add(m);child=mouse;
            } else {var text=Visuals.Text(t.Label,13,"#FFFFFF",p);text.HorizontalAlignment=HorizontalAlignment.Center;child=text;}
            Box=owner.TileBox(t,child);background=(SolidColorBrush)Box.Background;border=(SolidColorBrush)Box.BorderBrush;
            if(t.Id=="mouse")Box.CornerRadius=new CornerRadius(24);
        }
        public void Update(Preferences p,bool edit,bool selected,bool down,bool left,bool right,bool middle,bool wheel,Trail trail,long now) {
            border.Color=Visuals.Color(selected?"#F3CF77":"#FFFFFF");border.Opacity=edit?.7:.12;
            if(Value!=null)return;
            background.Color=Visuals.Color(down&&tile.Id!="mouse"?p.InputColor:"#17181B");
            background.Opacity=down&&tile.Id!="mouse"?(tile.Style=="outline"?.18:.9):p.InputOpacity;
            if(tile.Style=="outline"){border.Color=Visuals.Color(down?p.InputColor:"#FFFFFF");border.Opacity=down?1:.4;}
            if(leftBrush!=null){leftBrush.Opacity=left?1:.05;rightBrush!.Opacity=right?1:.05;middleBrush!.Color=Visuals.Color(middle||wheel?p.InputColor:"#EEEEEE");}
            if(trace==null)return;
            bool geometryChanged=mesh.Update(trail,p.TrailLife);
            for(int i=0;i<bands.Length;i++) {
                var band=mesh.Bands[i];
                bands[i].Opacity=band.Opacity(now,p.TrailLife);
                if(!geometryChanged)continue;
                var points=bands[i].Points;
                for(int j=0;j<band.Points.Count;j++) {
                    var q=band.Points[j];var point=new Point(q.x,q.y);
                    if(j<points.Count) { if(points[j]!=point)points[j]=point; }
                    else points.Add(point);
                }
                while(points.Count>band.Points.Count)points.RemoveAt(points.Count-1);
            }
            var pointer=trail.Project(trail.X,trail.Y);
            Canvas.SetLeft(cursor!,pointer.x-2);Canvas.SetTop(cursor!,pointer.y-2);
        }
    }
    TileVisual GetTileVisual(Tile t,bool stats) {
        if(!tileVisuals.TryGetValue(t,out var visual))tileVisuals[t]=visual=new TileVisual(this,t,stats);
        return visual;
    }
}
