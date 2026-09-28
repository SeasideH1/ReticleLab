using Geometry = Reticle.Core.Geometry;
using Microsoft.Gaming.XboxGameBar;
using Microsoft.Gaming.XboxGameBar.Input;
using Reticle.Core;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Reticle.Widget;
sealed partial class OverlayPage : Page
{
    static int nextViewId;
    readonly int viewId = Interlocked.Increment(ref nextViewId);
    readonly XboxGameBarWidget widget;
    readonly CoreDispatcher viewDispatcher;
    // Canvas does not let the unscaled child's desired size enlarge the viewport.
    readonly Canvas surface = new();
    readonly Grid root = new();
    CoreCursor? savedCursor;
    bool cursorHidden;
    double contentScale = 1;
    double layoutWidth=double.NaN,layoutHeight=double.NaN;
    Preferences? layoutPreferences;
    readonly ScaleTransform canvasTransform = new();
    readonly Canvas crosshairLayer = new() {IsHitTestVisible=false,UseLayoutRounding=false};
    readonly record struct CrosshairKey(Point Center,double Dpi,double Scale,double X,double Y,double Length,double Gap,double Width,bool Dot,bool Antialias,string Style,double Radius,string Color);
    CrosshairKey? crosshairKey;
    readonly Canvas canvas = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Background = new SolidColorBrush(Colors.Transparent), UseLayoutRounding = false };
    readonly StackPanel tools = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4 };
    readonly TextBlock status = new() { FontSize = 10, Foreground = new SolidColorBrush(Colors.LightGray), VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
    readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromMilliseconds(100) };
    readonly DispatcherTimer animation = new() { Interval = TimeSpan.FromMilliseconds(16) };
    readonly HashSet<string> pressed = new();
    readonly List<(XboxGameBarHotkeyWatcher watcher, TypedEventHandler<XboxGameBarHotkeyWatcher, HotkeySetStateChangedArgs> handler)> watchers = new();
    readonly Trail trail = new();
    readonly MouseTrailPlayback mousePlayback = new();
    readonly FeedbackCursor feedback = new();
    readonly List<CrosshairEffect> effects = new();
    readonly List<KillNotice> feeds = new();
    readonly Dictionary<KillNotice, Visuals.FeedVisual> feedVisuals = new();
    readonly KillNotice feedPreview = new() {Weapon="AK-47",WeaponSource="布局预览",Count=1,Streak=2};
    Preferences p = Store.Load(); Snapshot snapshot = new();
    DisplayInformation? display;
    Tile? selected, dragging;
    Point origin, tileOrigin; Point? priorPointer;
    bool edit, disposed, suspended, polling, animationSubscribed,wasAnimated;
    bool restoringWindow=true;
    bool left, right, middle; long wheelAt, demoUntil;
    DateTime settingsStamp; long lastDraw,lastSettingsCheck;
    MouseState? mouseState; long mouseLeaseAt,mouseLaunchAt; string mouseStatus="等待鼠标接收器";
    double DpiScale => display?.RawPixelsPerViewPixel ?? 1;
    long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    List<Tile> Tiles => widget.AppExtensionId == "Stats" ? p.Stats : p.Inputs;
    bool IsForeground => widget.GameBarDisplayMode.ToString() == "Foreground";
    public OverlayPage(XboxGameBarWidget widget)
    {
        viewDispatcher=Dispatcher;
        UiThread.Attach(viewDispatcher);
        animation.Tick += Animate;
        this.widget = widget; IsTabStop = true;
        canvas.RenderTransform=canvasTransform;frame=new FrameBatch(canvas);
        animation.Interval=FramePacer.Interval(p.TargetFps);
        HorizontalContentAlignment=HorizontalAlignment.Stretch; VerticalContentAlignment=VerticalAlignment.Stretch;
        App.Views[viewId] = new WeakReference<OverlayPage>(this);
        surface.Children.Add(canvas); root.Children.Add(surface);
        // Keep a long toolbar from enlarging the centered content's layout width.
        var toolbar = new ScrollViewer { Content=tools, VerticalAlignment=VerticalAlignment.Top,
            HorizontalScrollMode=ScrollMode.Enabled, HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,
            VerticalScrollMode=ScrollMode.Disabled, VerticalScrollBarVisibility=ScrollBarVisibility.Disabled };
        root.Children.Add(toolbar); root.Children.Add(status); Content = root;
        Button Add(string text, Action action) { var b = new Button { Content = text, FontSize = 11, Padding = new Thickness(6, 2, 6, 2) }; b.Click += (_, _) => { try { action(); } catch(Exception e) { Store.Log(e); status.Text=e.Message; } }; tools.Children.Add(b); return b; }
        Add("编辑", () => { edit = !edit; ClearInput(); ConfigureWatchers(); Draw(); });
        Add("设置", () => Run(async () => await widget.ActivateSettingsAsync()));
        if (widget.AppExtensionId == "Crosshair") Add("居中", () => Run(Center));
        Add("演示", Demo);
        if (widget.AppExtensionId == "Crosshair") {
            Add("命中演示", () => DemoHit(false, false)); Add("头部命中演示", () => DemoHit(false, true));
            Add("头部击杀", () => DemoHit(true, true));
        }
        Loaded += (_, args) => Run(() => LoadedPage(this, args)); Unloaded += UnloadedPage;
        surface.SizeChanged += (_, _) => Draw();
        KeyDown += KeyPressed; KeyUp += KeyReleased;
        canvas.PointerPressed += PointerDown; canvas.PointerMoved += PointerMove;
        canvas.PointerReleased += PointerUp; canvas.PointerCanceled += PointerCancelled;
        canvas.PointerCaptureLost += PointerCancelled; canvas.PointerWheelChanged += Wheel;
    }
    async Task LoadedPage(object sender, RoutedEventArgs args)
    {
        if (disposed) return;
        // Apply only to this app view; Game Bar/Windows still own capture policy.
        try { Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().IsScreenCaptureEnabled=true; }
        catch(Exception e) { Store.Log(e); }
        display = DisplayInformation.GetForCurrentView(); display.DpiChanged += DpiChanged;
        widget.SettingsClicked += SettingsClicked; widget.GameBarDisplayModeChanged += ModeChanged;
        widget.VisibleChanged += VisibilityChanged; widget.RequestedOpacityChanged += OpacityChanged;
        widget.ClickThroughEnabledChanged += ModeChanged;
        widget.WindowBoundsChanged += WindowBoundsChanged;
        Window.Current.CoreWindow.Activated += Activated; Window.Current.CoreWindow.Closed += Closed;
        poll.Interval=TimeSpan.FromMilliseconds(widget.AppExtensionId is "Crosshair" or "Feed" ? 16 : widget.AppExtensionId=="Input" ? 33 : 100);
        poll.Tick += Poll; poll.Start(); ConfigureWatchers();
        widget.MinWindowSize = new Size(160, 120); widget.MaxWindowSize = new Size(760, 520);
        try {
            // Game Bar owns screen position. Do not overwrite its restored bounds
            // or re-center the crosshair on every activation.
            var saved=Store.LoadWindowSize(widget.AppExtensionId);
            if(saved.HasValue && (Math.Abs(widget.WindowBounds.Width-saved.Value.Width)>.5 || Math.Abs(widget.WindowBounds.Height-saved.Value.Height)>.5)) {
                if(!await widget.TryResizeWindowAsync(saved.Value))status.Text="Game Bar 未接受已保存尺寸，请检查屏幕空间";
            }
        } catch (Exception e) { Store.Log(e); status.Text = e.Message; }
        restoringWindow=false;
        if(!disposed)RememberWindowSize();
        ApplyPointerMode();Draw();
    }
    void Run(Func<Task> action) => _ = UiThread.Run(viewDispatcher, action, e => { if(!disposed)status.Text=e.Message; });
    void LayoutCanvas() {
        double width=Math.Max(1,surface.ActualWidth), height=Math.Max(1,surface.ActualHeight);
        if(width==layoutWidth&&height==layoutHeight&&ReferenceEquals(p,layoutPreferences))return;
        layoutWidth=width;layoutHeight=height;layoutPreferences=p;
        if(widget.AppExtensionId=="Crosshair") {
            contentScale=1;canvas.Width=width;canvas.Height=height;
        } else {
            double designWidth,designHeight;bool fit;double manual;
            if(widget.AppExtensionId=="Feed") {designWidth=660;designHeight=360;fit=p.FeedAutoFit;manual=p.FeedScale;}
            else {
                var tiles=widget.AppExtensionId=="Stats"?p.Stats:p.Inputs;
                designWidth=Math.Max(widget.AppExtensionId=="Stats"?390:440,tiles.Count==0?1:tiles.Max(t=>t.X+t.Width)+12);
                designHeight=Math.Max(widget.AppExtensionId=="Stats"?300:210,tiles.Count==0?1:tiles.Max(t=>t.Y+t.Height)+50);
                fit=widget.AppExtensionId=="Stats"?p.StatsAutoFit:p.InputAutoFit;manual=widget.AppExtensionId=="Stats"?p.StatsScale:p.InputScale;
            }
            contentScale=Geometry.LayoutScale(width,height,designWidth,designHeight,fit,manual);
            canvas.Width=width/contentScale;canvas.Height=height/contentScale;
        }
        canvasTransform.ScaleX=contentScale;canvasTransform.ScaleY=contentScale;
    }
    async Task Center() {
        try { await CenterHostedContent(); if(disposed)return; p.OffsetX = p.OffsetY = 0; await Save(); Draw(); }
        catch (Exception e) { Store.Log(e); status.Text = "居中失败：" + e.Message; }
    }
    async Task CenterHostedContent() {
        // Resize is asynchronous; settle XAML layout before the host centers its content.
        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => UpdateLayout());
        if(disposed)return;
        await widget.CenterWindowAsync();
    }
    Point CrosshairCenter() {
        // Center the SDK's hosted Frame, rather than a potentially oversized descendant.
        if (Window.Current.Content is FrameworkElement host && host.ActualWidth>0 && host.ActualHeight>0)
            return host.TransformToVisual(canvas).TransformPoint(new Point(host.ActualWidth/2,host.ActualHeight/2));
        return new Point(canvas.Width/2,canvas.Height/2);
    }
    void DpiChanged(DisplayInformation sender, object args) => Run(()=>{Draw();return Task.CompletedTask;});
    void SettingsClicked(XboxGameBarWidget sender, object args) => Run(async () => await sender.ActivateSettingsAsync());
    void ModeChanged(XboxGameBarWidget sender, object args) => Run(async () => {
        if(!IsForeground) {
            bool save=dragging!=null;dragging=null;canvas.ReleasePointerCaptures();
            if(save)await Save();
        }
        ApplyPointerMode();ClearInput(preserveBackground:true);Draw();
    });
    void ApplyPointerMode() {
        // A transparent background still receives XAML pointer events. Pinned
        // overlays must not focus themselves on a game click.
        root.IsHitTestVisible=IsForeground;IsTabStop=IsForeground;
        bool hide=widget.AppExtensionId=="Crosshair" && !IsForeground && widget.Visible && !suspended;
        var window=Window.Current.CoreWindow;
        if(hide&&!cursorHidden) {savedCursor=window.PointerCursor;window.PointerCursor=null;cursorHidden=true;}
        else if(!hide&&cursorHidden) {window.PointerCursor=savedCursor;cursorHidden=false;savedCursor=null;}
    }
    void VisibilityChanged(XboxGameBarWidget sender, object args) => Run(() => {
        ApplyPointerMode();
        if (widget.Visible) { poll.Start(); ConfigureWatchers(); Draw(); } else { poll.Stop(); StopWatchers(); ClearInput(); StopAnimation(); effects.Clear(); feeds.Clear(); feedback.Reset(); }
        return Task.CompletedTask;
    });
    void OpacityChanged(XboxGameBarWidget sender, object args) => Run(() => { Draw(); return Task.CompletedTask; });
    void WindowBoundsChanged(XboxGameBarWidget sender,object args)=>Run(()=>{if(!restoringWindow&&!disposed)RememberWindowSize();Draw();return Task.CompletedTask;});
    void RememberWindowSize(){try{Store.SaveWindowSize(widget.AppExtensionId,widget.WindowBounds);}catch(Exception e){Store.Log(e);status.Text="窗口尺寸保存失败："+e.Message;}}
    void Activated(CoreWindow sender, WindowActivatedEventArgs args) { if (args.WindowActivationState == CoreWindowActivationState.Deactivated) ClearInput(preserveBackground:true); }
    void Closed(CoreWindow sender, CoreWindowEventArgs args) => Dispose();
    void UnloadedPage(object sender, RoutedEventArgs args) => Dispose();
    internal void Suspend() {
        if(disposed)return;
        suspended=true;ApplyPointerMode();poll.Stop();StopWatchers();StopAnimation();effects.Clear();feeds.Clear();feedback.Reset();ClearInput();
    }
    internal void Resume() {
        if(disposed || !suspended)return;
        suspended=false;ApplyPointerMode();
        if(widget.Visible) { poll.Start();ConfigureWatchers();Draw(); }
    }
    internal void Dispose() {
        if(cursorHidden){Window.Current.CoreWindow.PointerCursor=savedCursor;cursorHidden=false;savedCursor=null;}
        if (disposed) return; disposed = true; poll.Stop(); poll.Tick -= Poll; StopAnimation(); animation.Tick-=Animate;StopWatchers();feedVisuals.Clear();tileVisuals.Clear();hitPool.Clear();
        App.Views.TryRemove(viewId, out _);
        if (display != null) display.DpiChanged -= DpiChanged;
        widget.SettingsClicked -= SettingsClicked; widget.GameBarDisplayModeChanged -= ModeChanged;
        widget.VisibleChanged -= VisibilityChanged; widget.RequestedOpacityChanged -= OpacityChanged;
        widget.ClickThroughEnabledChanged -= ModeChanged;
        widget.WindowBoundsChanged -= WindowBoundsChanged;
        Window.Current.CoreWindow.Activated -= Activated; Window.Current.CoreWindow.Closed -= Closed;
    }
    void Poll(object? sender, object args) => Run(PollCore);
    async Task PollCore()
    {
        if (polling || disposed || suspended) return; polling = true;
        try {
            if(Now-lastSettingsCheck>=200) {
                lastSettingsCheck=Now;
                var stamp = File.GetLastWriteTimeUtc(System.IO.Path.Combine(Store.Root, "preferences.json"));
                if (!edit && stamp != settingsStamp) { p = Store.Load(); settingsStamp = stamp; feedVisuals.Clear();tileVisuals.Clear();animation.Interval=FramePacer.Interval(p.TargetFps);ConfigureWatchers(); Draw(); }
            }
            if(widget.AppExtensionId=="Input")await PollMouse();
            var next = await Store.ReadSnapshot(); if (disposed || suspended || !widget.Visible) return;
            if (next != null) {
                bool fresh = next.Fresh(Now), changed = next.ReceivedAt != snapshot.ReceivedAt || fresh != snapshot.Fresh(Now - 100);
                foreach (var notice in feedback.Take(next, Now)) {
                    if(widget.AppExtensionId=="Crosshair")QueueEffect(notice.At,true,notice.Headshots>0);
                    if(widget.AppExtensionId=="Feed")feeds.Add(notice);
                    if(widget.AppExtensionId is "Crosshair" or "Feed")StartAnimation();
                    if(widget.AppExtensionId is "Crosshair" or "Feed")Store.Feedback(widget.AppExtensionId,next.Events.First(e=>e.Id==notice.Id).At,notice.At);
                }
                if(snapshot.Session!=next.Session) {effects.Clear();feeds.Clear();}
                snapshot = next;
                if (!fresh) { effects.Clear(); feeds.Clear(); }
                if (changed && widget.AppExtensionId!="Input") Draw();
            }
            if (trail.Tick(Now, p.TrailLife, p.IdleCenter)) { if (trail.LastMove == 0) priorPointer = null; Draw(); }
        } catch (Exception e) { Store.Log(e); } finally { polling = false; }
    }
    async Task PollMouse() {
        if(!p.BackgroundMouse || edit) {
            if(mouseState!=null){mouseState=null;left=right=middle=false;wheelAt=0;mousePlayback.Reset();trail.Reset();Draw();}
            return;
        }
        long now=Now;
        if(now-mouseLeaseAt>=1000) {
            mouseLeaseAt=now;
            // Only the visible input widget renews this lease; receiver stops within three seconds.
            await File.WriteAllTextAsync(System.IO.Path.Combine(Store.Root,"mouse-request.txt"),"visible");
            string heartbeat=System.IO.Path.Combine(Store.Root,"bridge-status.json");
            if(now-mouseLaunchAt>10000 && (!File.Exists(heartbeat) || DateTime.UtcNow-File.GetLastWriteTimeUtc(heartbeat)>TimeSpan.FromSeconds(4))) {
                mouseLaunchAt=now;
                if(!File.Exists(System.IO.Path.Combine(Store.Root,"preferences.json")))await Store.Save(p);
                await Windows.ApplicationModel.FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            }
        }
        var next=await Store.ReadMouse();if(disposed || suspended)return;
        // A transient file read miss must not throw away a still-fresh baseline.
        if(next==null && mouseState?.Fresh(Now)==true)return;
        if(next==null || !next.Fresh(Now)) {
            bool changed=left||right||middle||mouseState!=null;
            mouseStatus=next?.Error.Length>0?"鼠标接收失败："+next.Error:"等待后台鼠标接收";
            mouseState=null;mousePlayback.Reset();left=right=middle=false;
            if(changed)Draw();return;
        }
        if(mouseState!=null && mouseState.Session!=next.Session)trail.Reset();
        bool moved=mousePlayback.Accept(next);
        bool l=next.Left || Now-next.LeftAt<80,r=next.Right || Now-next.RightAt<80,m=next.Middle || Now-next.MiddleAt<80;
        bool dirty=moved||l!=left||r!=right||m!=middle||next.WheelAt!=wheelAt||mouseState==null;
        left=l;right=r;middle=m;wheelAt=next.WheelAt;mouseState=next;mouseStatus="后台鼠标 · Windows Raw Input";
        if(dirty){StartAnimation();Draw();}
    }
    void StartAnimation() { if (animationSubscribed || disposed || suspended || !widget.Visible) return; animation.Start(); animationSubscribed = true; }
    void StopAnimation() { animation.Stop(); animationSubscribed = false; }
    void Animate(object? sender, object args) => Run(()=>{AnimateCore();return Task.CompletedTask;});
    void AnimateCore() {
        if(disposed || suspended || !widget.Visible){StopAnimation();return;}
        long now = Now;
        if(widget.AppExtensionId=="Input" && p.BackgroundMouse && !edit && mousePlayback.Advance(trail,now,p.Sensitivity))framePacer.Request();
        if (demoUntil != 0 && now >= demoUntil) { pressed.Remove("W"); demoUntil = 0; framePacer.Request(); }
        for(int i=effects.Count-1;i>=0;i--)if(effects[i].Expired(now,p.EffectDuration)){effects.RemoveAt(i);framePacer.Request();}
        for(int i=feeds.Count-1;i>=0;i--)if(now-feeds[i].At>p.Enter+p.Hold+p.Exit){feedVisuals.Remove(feeds[i]);feeds.RemoveAt(i);framePacer.Request();}
        bool trailChanged=trail.Tick(now, p.TrailLife, p.IdleCenter);
        if(trail.LastMove==0)priorPointer=null;
        if(trailChanged)framePacer.Request();
        bool active=effects.Count>0||feeds.Count>0||trail.Points.Count>0||mousePlayback.Pending||now-wheelAt<=180||demoUntil!=0;
        if(wasAnimated&&!active)framePacer.Request();
        wasAnimated=active;
        // A final frame removes expired visuals, even when no active animation remains.
        if(framePacer.Take(System.Diagnostics.Stopwatch.GetTimestamp()*1000d/System.Diagnostics.Stopwatch.Frequency,p.TargetFps,active))RenderFrame();
        if(!active&&!framePacer.Pending)StopAnimation();
    }
    void QueueEffect(long received,bool kill,bool head) {
        var effect=CrosshairEffect.Schedule(received,p.EffectDelay,kill,head);
        if(effects.Count>0)effect=effect with {StartsAt=Math.Max(effect.StartsAt,effects[^1].StartsAt+(long)Math.Ceiling(p.EffectDuration))};
        effects.Add(effect);
    }
    void DemoHit(bool kill, bool head) { QueueEffect(Now,kill,head); Draw(); }
    void Demo() {
        long now = Now;
        if(widget.AppExtensionId=="Crosshair")QueueEffect(now,true,false);
        if(widget.AppExtensionId=="Feed")feeds.Add(new KillNotice { At = now, Weapon = "AK-47", WeaponSource = "演示", Damage = 100, DamageLabel="更新增量",Count = 1 });
        if (widget.AppExtensionId == "Stats") snapshot = new Snapshot { ReceivedAt = now, IsSelf = true, Status = "模拟演示", Kills = 12, Deaths = 5, Assists = 3, RoundKills = 2, RoundHeadshots = 1, RoundDamage = 178, RecordedDamage = 845, Money = 3800, NetMoney = 1450, Phase = "freezetime", LastRound = new RoundSummary { Kills = 2, Headshots = 1, Damage = 178, NetMoney = 1450 } };
        if (widget.AppExtensionId == "Input") { pressed.Add("W"); demoUntil = now + 600; trail.Move(25, -10, now); }
        if(widget.AppExtensionId!="Stats")StartAnimation(); Draw();
    }
    void Draw() {
        if(!viewDispatcher.HasThreadAccess){Run(()=>{Draw();return Task.CompletedTask;});return;}
        if(disposed||suspended)return;
        framePacer.Request();StartAnimation();
    }
    void RenderFrame()
    {
        if(!viewDispatcher.HasThreadAccess){Run(()=>{Draw();return Task.CompletedTask;});return;}
        if (disposed || dragging != null) return;
        LayoutCanvas();
        lastDraw = Now; frame.Begin();
        tools.Visibility = IsForeground ? Visibility.Visible : Visibility.Collapsed;
        status.Visibility = IsForeground ? Visibility.Visible : Visibility.Collapsed;
        bool fresh = snapshot.Fresh(Now), freeze = fresh && snapshot.Phase == "freezetime";
        status.Text = widget.AppExtensionId == "Input" ? (edit ? "拖动吸附 · Alt 暂停吸附 · 方向键 1 px" : p.OfficialKeys ? "官方热键检测 · 鼠标仅控件焦点内" : "键鼠仅控件焦点内 · 点击空白处开始") : fresh ? snapshot.Status : "GSI 等待/已过期 · 无可用实时数据";
        if (widget.AppExtensionId == "Crosshair") status.Text = fresh
            ? "准心独立显示 · " + snapshot.Status
            : "准心不依赖 GSI · 未连接时仅无实时击杀反馈";
        if(widget.AppExtensionId=="Input" && p.BackgroundMouse && !edit)status.Text=mouseStatus;
        canvas.Opacity = Geometry.OverlayOpacity(widget.RequestedOpacity, IsForeground);
        switch (widget.AppExtensionId) {
            case "Crosshair":
                double crosshairScale=Geometry.LayoutScale(canvas.Width,canvas.Height,400,400,p.CrosshairAutoFit,p.CrosshairScale);
                var center=CrosshairCenter();
                var key=new CrosshairKey(center,DpiScale,crosshairScale,p.OffsetX,p.OffsetY,p.CrosshairSize,p.CrosshairGap,p.CrosshairThickness,p.Dot,p.Antialias,p.CrosshairStyle,p.CrosshairRadius,p.CrosshairColor);
                if(crosshairKey!=key) {
                    crosshairLayer.Width=canvas.Width;crosshairLayer.Height=canvas.Height;
                    crosshairLayer.Children.Clear();Visuals.Crosshair(crosshairLayer,p,DpiScale,crosshairScale,center);crosshairKey=key;
                }
                frame.Use(crosshairLayer);
                int hitIndex=0;
                foreach(var e in effects)if(e.Progress(lastDraw,p.EffectDuration)!=null) {
                    if(hitIndex==hitPool.Count)hitPool.Add(new HitVisual());
                    var visual=hitPool[hitIndex++];visual.Update(p,DpiScale,crosshairScale,center,e,lastDraw);frame.Use(visual.Root);
                }
                break;
            case "Feed":
                for (int i = 0; i < Math.Min(feeds.Count, 5); i++) DrawFeed(feeds[feeds.Count - 1 - i], Now - feeds[feeds.Count - 1 - i].At, 45 + i * (p.FeedHeight + 8));
                if (edit && feeds.Count == 0) DrawFeed(feedPreview,p.Enter,45);
                break;
            case "Stats":
                if(fresh && freeze) status.Text += snapshot.LastRound == null ? " · 上回合未记录" : " · 回合项：上回合 / 金钱与 KDA：当前";
                foreach (var t in p.Stats) {
                    if (!Geometry.Visible(t, edit, p.StatsRoundOnly, freeze, false, false)) continue;
                    var visual=GetTileVisual(t,true);var box=visual.Box;
                    string value=fresh?Visuals.Stat(t.Id,snapshot):"—";
                    if(visual.Value!.Text!=value)visual.Value.Text=value;
                    visual.Update(p,edit,selected?.Id==t.Id,false,false,false,false,false,trail,lastDraw);
                    box.Opacity=p.DimDuringFreeze&&freeze&&!edit?p.DimOpacity:1;
                    ToolTipService.SetToolTip(box, t.Id switch {
                        "kda"=>"本人 match_stats 本局累计，不是本回合",
                        "hskill"=>"本人 round_killhs / round_kills；社区地图可能在重生时重置",
                        "hshit"=>"GSI 不提供可靠的逐次命中部位，无法计算",
                        "damage"=>"本人 round_totaldmg；— 表示游戏未提供该字段",
                        "totalDamage"=>"连续接收期间观察到的伤害增量；— 表示伤害字段缺失",
                        "cash"=>"本人 GSI money 原值；部分社区/练习模式为 0",
                        "net"=>"准备阶段显示上回合结算余额减其开始余额，含购买支出；比赛中显示本回合；无基线时为 —",
                        "kills"=>"本人 GSI round_kills；不是 match_stats 总击杀", _=>"本人 GSI match_stats"});
                    frame.Use(box,t.X,t.Y+38);
                }
                break;
            case "Input": DrawInput(); break;
        }
        frame.Commit();
    }
    void DrawFeed(KillNotice notice,double elapsed,double y) {
        if(!feedVisuals.TryGetValue(notice,out var visual))feedVisuals[notice]=visual=new Visuals.FeedVisual(p,notice);
        visual.Draw(canvas,frame,p,elapsed,y);
    }
    Border TileBox(Tile t, UIElement child) => new() {
        Width = t.Width, Height = t.Height, Child = child, Tag = t, UseLayoutRounding = false,
        Background = Visuals.Brush("#17181B", widget.AppExtensionId == "Stats" ? p.StatsOpacity : p.InputOpacity), BorderBrush = Visuals.Brush(selected?.Id == t.Id ? "#F3CF77" : "#FFFFFF", edit ? .7 : .12),
        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(t.Style == "rounded" ? 5 : 0)
    };
    void DrawInput() {
        foreach (var t in p.Inputs) {
            bool down = t.Id == "mouse" ? left || right || middle || Now - wheelAt < 180 : pressed.Contains(t.Id);
            if (!Geometry.Visible(t, edit, false, false, down, trail.Points.Count > 0)) continue;
            var visual=GetTileVisual(t,false);
            visual.Update(p,edit,selected?.Id==t.Id,down,left,right,middle,Now-wheelAt<180,trail,lastDraw);
            frame.Use(visual.Box,t.X,t.Y+38);
        }
    }
    void KeyPressed(object sender, KeyRoutedEventArgs e) {
        bool arrow = e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down;
        if (arrow && (widget.AppExtensionId == "Crosshair" || edit)) {
            int dx = e.Key == VirtualKey.Left ? -1 : e.Key == VirtualKey.Right ? 1 : 0, dy = e.Key == VirtualKey.Up ? -1 : e.Key == VirtualKey.Down ? 1 : 0;
            if (widget.AppExtensionId == "Crosshair") { p.OffsetX += dx; p.OffsetY += dy; }
            else if (selected != null) { selected.X += dx / (DpiScale * contentScale); selected.Y += dy / (DpiScale * contentScale); }
            e.Handled = true; Run(Save); Draw(); return;
        }
        if (!edit && widget.AppExtensionId == "Input") { pressed.Add(KeyName(e.Key, e.KeyStatus)); e.Handled = true; Draw(); }
    }
    void KeyReleased(object sender, KeyRoutedEventArgs e) { pressed.Remove(KeyName(e.Key, e.KeyStatus)); Draw(); }
    internal static string KeyName(VirtualKey key, CorePhysicalKeyStatus status) => key switch {
        VirtualKey.Shift => status.ScanCode == 54 ? "RightShift" : "LeftShift",
        VirtualKey.Control => status.IsExtendedKey ? "RightControl" : "LeftControl",
        VirtualKey.Menu => status.IsExtendedKey ? "RightMenu" : "LeftMenu", _ => key.ToString()
    };
    async Task Save() {
        try {
            string? id=selected?.Id;double x=selected?.X??0,y=selected?.Y??0,offsetX=p.OffsetX,offsetY=p.OffsetY;
            await Store.Update(latest=>{
                if (widget.AppExtensionId == "Crosshair") { latest.OffsetX = offsetX; latest.OffsetY = offsetY; }
                else {
                    var target = widget.AppExtensionId == "Stats" ? latest.Stats : latest.Inputs;
                    foreach (var item in target) if (item.Id == id) { item.X = x; item.Y = y; }
                }
            });
        } catch (Exception e) { Store.Log(e);status.Text="配置保存失败："+e.Message; }
    }
    void PointerDown(object sender, PointerRoutedEventArgs e) {
        if(!IsForeground)return;
        Focus(FocusState.Pointer); var point = e.GetCurrentPoint(canvas);
        if (edit) {
            var node = e.OriginalSource as DependencyObject;
            while (node != null && node is not Border { Tag: Tile }) node = VisualTreeHelper.GetParent(node);
            if (node is Border { Tag: Tile t }) { selected = t; dragging = t; origin = point.Position; tileOrigin = new Point(t.X, t.Y); canvas.CapturePointer(e.Pointer); e.Handled = true; }
        } else { UpdateMouse(point.Properties); Draw(); }
    }
    void PointerMove(object sender, PointerRoutedEventArgs e) {
        var point = e.GetCurrentPoint(canvas);
        if (dragging != null) {
            double x = tileOrigin.X + point.Position.X - origin.X, y = tileOrigin.Y + point.Position.Y - origin.Y;
            bool alt = Window.Current.CoreWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            if (p.Snap && !alt) {
                x = Geometry.SnapAxis(x, dragging.Width, canvas.Width, Tiles.Where(t=>t!=dragging).Select(t=>(t.X,t.Width)),p.Grid,6/(DpiScale*contentScale));
                y = Geometry.SnapAxis(y, dragging.Height, canvas.Height-38, Tiles.Where(t=>t!=dragging).Select(t=>(t.Y,t.Height)),p.Grid,6/(DpiScale*contentScale));
            }
            dragging.X = Math.Clamp(x,0,Math.Max(0,canvas.Width-dragging.Width)); dragging.Y = Math.Clamp(y,0,Math.Max(0,canvas.Height-38-dragging.Height));
            foreach (var b in canvas.Children.OfType<Border>()) if (b.Tag == dragging) { Canvas.SetLeft(b,dragging.X); Canvas.SetTop(b,dragging.Y+38); }
            foreach (var guide in canvas.Children.OfType<Line>().Where(l=>Equals(l.Tag,"guide")).ToArray()) canvas.Children.Remove(guide);
            if (p.Snap && !alt) {
                var gx = new Line { X1=dragging.X, X2=dragging.X, Y1=38, Y2=canvas.Height, Stroke=Visuals.Brush("#F3CF77",.65), StrokeThickness=1/(DpiScale*contentScale), IsHitTestVisible=false, Tag="guide" };
                var gy = new Line { X1=0, X2=canvas.Width, Y1=dragging.Y+38, Y2=dragging.Y+38, Stroke=gx.Stroke, StrokeThickness=gx.StrokeThickness, IsHitTestVisible=false, Tag="guide" };
                canvas.Children.Add(gx); canvas.Children.Add(gy);
            }
            return;
        }
        if (!edit && !p.BackgroundMouse && widget.AppExtensionId == "Input" && FocusState != FocusState.Unfocused) {
            if (priorPointer.HasValue) trail.Move((point.Position.X-priorPointer.Value.X)*p.Sensitivity,(point.Position.Y-priorPointer.Value.Y)*p.Sensitivity,Now);
            priorPointer = point.Position; UpdateMouse(point.Properties); StartAnimation(); Draw();
        }
    }
    void UpdateMouse(Windows.UI.Input.PointerPointProperties props) { if(p.BackgroundMouse && !edit)return;left = props.IsLeftButtonPressed; right = props.IsRightButtonPressed; middle = props.IsMiddleButtonPressed; }
    void PointerUp(object sender, PointerRoutedEventArgs e) => Run(async () => { var wasDragging = dragging != null; dragging = null; canvas.ReleasePointerCaptures(); UpdateMouse(e.GetCurrentPoint(canvas).Properties); if (wasDragging) await Save(); Draw(); });
    void PointerCancelled(object sender, PointerRoutedEventArgs e) { bool save=dragging!=null;dragging = null;if(save)Run(Save);ClearInput(preserveBackground:true); }
    void Wheel(object sender, PointerRoutedEventArgs e) { if (!edit && !p.BackgroundMouse) { wheelAt = Now; StartAnimation(); Draw(); } }
    void ClearInput(bool preserveBackground=false) {
        pressed.Clear();priorPointer=null;
        if(!(preserveBackground && widget.AppExtensionId=="Input" && p.BackgroundMouse && !edit && widget.Visible && !suspended)) {
            left = right = middle = false;wheelAt=0;mouseState=null;mousePlayback.Reset();trail.Reset();
        }
        Draw();
    }
    void StopWatchers() { foreach (var pair in watchers) { try { pair.watcher.HotkeySetStateChanged -= pair.handler; pair.watcher.Stop(); } catch (Exception e) { Store.Log(e); } } watchers.Clear(); }
    void ConfigureWatchers() {
        StopWatchers();
        if (widget.AppExtensionId != "Input" || !p.OfficialKeys || edit) return;
        foreach (var t in p.Inputs.Where(t => t.Id != "mouse" && t.Id != "trace" && t.Mode != "hidden")) {
            if (!Enum.TryParse<VirtualKey>(t.Id, out var key)) continue;
            try {
                var watcher = XboxGameBarHotkeyWatcher.CreateWatcher(widget, new[] { key });
                string id = t.Id;
                TypedEventHandler<XboxGameBarHotkeyWatcher, HotkeySetStateChangedArgs> handler = async (s, args) => {
                    try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { if (disposed || edit) return; if (args.HotkeySetDown) pressed.Add(id); else pressed.Remove(id); Draw(); }); }
                    catch (Exception e) { Store.Log(e); }
                };
                watcher.HotkeySetStateChanged += handler;
                watchers.Add((watcher, handler)); watcher.Start();
            } catch (Exception e) { Store.Log(e); }
        }
    }
}
