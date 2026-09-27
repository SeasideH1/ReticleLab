using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Reticle.Widget;

// One retained visual tree per view. Never shares XAML objects between UI threads.
sealed class FrameBatch(Canvas canvas)
{
    readonly List<UIElement> desired=new();
    readonly HashSet<UIElement> used=new();
    public void Begin(){desired.Clear();used.Clear();}
    public void Use(UIElement element,double x=0,double y=0) {
        if(Canvas.GetLeft(element)!=x)Canvas.SetLeft(element,x);
        if(Canvas.GetTop(element)!=y)Canvas.SetTop(element,y);
        if(used.Add(element))desired.Add(element);
    }
    public void Commit() {
        for(int i=canvas.Children.Count-1;i>=0;i--)if(!used.Contains(canvas.Children[i]))canvas.Children.RemoveAt(i);
        for(int i=0;i<desired.Count;i++) {
            var child=desired[i];
            if(i<canvas.Children.Count&&ReferenceEquals(canvas.Children[i],child))continue;
            if(canvas.Children.Contains(child))canvas.Children.Remove(child);
            canvas.Children.Insert(i,child);
        }
    }
}
