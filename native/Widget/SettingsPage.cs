using System.Text.Json;
using Microsoft.Gaming.XboxGameBar;
using Reticle.Core;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Reticle.Widget;
sealed partial class SettingsPage : Page
{
    Preferences p = Store.Load();
    string savedPreferences = "";
    readonly StackPanel content = new() { Spacing = 12, Padding = new Thickness(20), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBlock message = new() { TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 3, MaxHeight = 58, FontSize = 11, Margin = new Thickness(16, 8, 16, 8) };
    readonly Grid shell = new();
    readonly Grid previewHost = new() { Height = 178, Background = Visuals.Brush("#1B2530"), Margin = new Thickness(20, 8, 20, 0) };
    readonly Canvas preview = new() { IsHitTestVisible = false, UseLayoutRounding = false };
    readonly List<Button> tabs = new();
    readonly ScrollViewer scroller = new();
    readonly XboxGameBarWidget? settingsWidget;
    bool building;
    static readonly SemaphoreSlim folderLaunchGate=new(1,1);
    int page;
    string previewBackground = "#1B2530";
    public SettingsPage(XboxGameBarWidget? widget = null) {
        savedPreferences=JsonSerializer.Serialize(p);
        UiThread.Attach(Dispatcher);
        settingsWidget = widget;
        RequestedTheme = ElementTheme.Dark;
        Background = Visuals.Brush("#13161B");
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition());
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new StackPanel { Margin = new Thickness(20, 14, 20, 8) };
        heading.Children.Add(Visuals.Text("RETICLE LAB", 22, "#E6F0C3", p));
        heading.Children.Add(Visuals.Text("修改即时保存 · 各控件可独立固定", 11, "#A4ADBA", p));
        shell.Children.Add(heading);
        var navigation = new Grid { Margin = new Thickness(16, 4, 16, 4) };
        string[] labels = { "准心", "击杀反馈", "统计", "键鼠", "GSI", "性能" };
        for (int i = 0; i < labels.Length; i++) {
            navigation.ColumnDefinitions.Add(new ColumnDefinition());
            int target = i;
            var tab = new Button { Content = labels[i], HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(2), Padding = new Thickness(4, 8, 4, 8), FontSize = 13 };
            tab.Click += (_, _) => { page = target; Build(); scroller.ChangeView(null, 0, null); };
            Grid.SetColumn(tab, i); navigation.Children.Add(tab); tabs.Add(tab);
        }
        Grid.SetRow(navigation, 1); shell.Children.Add(navigation);
        previewHost.Children.Add(preview);
        var previewLabel = Visuals.Text("实时预览 · 实际大小", 10, "#A4ADBA", p);
        previewLabel.Margin = new Thickness(10); previewLabel.VerticalAlignment = VerticalAlignment.Top;
        previewHost.Children.Add(previewLabel);
        previewHost.SizeChanged += (_, _) => RedrawPreview();
        Grid.SetRow(previewHost, 2); shell.Children.Add(previewHost);
        scroller.Content = content; scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Grid.SetRow(scroller, 3); shell.Children.Add(scroller);
        Grid.SetRow(message, 4); shell.Children.Add(message);
        Content = shell;
        message.Text = "Win + G 中分别打开并固定所需控件。";
        Build();
    }
    void Build() {
        building = true; content.Children.Clear();
        previewHost.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (int i = 0; i < tabs.Count; i++) tabs[i].Background = Visuals.Brush(i == page ? "#43513D" : "#242B34");
        switch (page) {
            case 0: CrosshairSettings(); break;
            case 1:
                Title("击杀反馈", "有伤害数据时显示相邻 GSI 更新增量；无可靠伤害增量时显示本条命的连杀序号：1ST、2ND、3RD…，死亡或换回合后重置。同次更新多杀分别编号；有效伤害为 0 时仍显示 0。中途连接从之后观察到的击杀开始计数。");
                ScaleSettings("Feed");
                Color("矩形颜色",p.FeedColor,v=>p.FeedColor=v);
                Number("宽度",p.FeedWidth,80,320,v=>p.FeedWidth=v); Number("高度",p.FeedHeight,24,64,v=>p.FeedHeight=v);
                Number("不透明度",p.FeedOpacity,0,1,v=>p.FeedOpacity=v); Number("圆角",p.FeedRadius,0,20,v=>p.FeedRadius=v);
                Number("展开（ms）",p.Enter,100,2000,v=>p.Enter=v); Number("停留（ms）",p.Hold,0,10000,v=>p.Hold=v); Number("淡出（ms）",p.Exit,100,2000,v=>p.Exit=v);
                break;
            case 2:
                Title("实时统计", "K/D/A 是本局累计；HS KILL 是 GSI 当前回合计数的比例。社区/练习图可能在重生时重置回合计数；缺失伤害和命中部位显示 —，不作估算。"); ScaleSettings("Stats");
                Toggle("默认只在回合准备阶段显示",p.StatsRoundOnly,v=>p.StatsRoundOnly=v);
                Toggle("回合准备阶段降低透明度",p.DimDuringFreeze,v=>p.DimDuringFreeze=v);
                Number("准备阶段不透明度",p.DimOpacity,0,1,v=>p.DimOpacity=v);
                Color("统计强调色",p.Accent,v=>p.Accent=v); Number("统计背景不透明度",p.StatsOpacity,0,1,v=>p.StatsOpacity=v);
                EditTiles(p.Stats,false); LayoutSettings(); break;
            case 3:
                Title("键鼠", "后台鼠标使用 Windows Raw Input，仅在键鼠控件显示时启用；无需游戏获得控件焦点。每项单独配置 A / B 模式。"); ScaleSettings("Input");
                Toggle("游戏内检测鼠标（Windows Raw Input）",p.BackgroundMouse,v=>p.BackgroundMouse=v);
                Toggle("通过 Game Bar 官方热键 API 检测配置键",p.OfficialKeys,v=>p.OfficialKeys=v);
                Color("键鼠高亮颜色",p.InputColor,v=>p.InputColor=v); Number("键鼠背景不透明度",p.InputOpacity,0,1,v=>p.InputOpacity=v);
                Number("轨迹保留（ms）",p.TrailLife,100,5000,v=>p.TrailLife=v); Number("静止归中（ms）",p.IdleCenter,500,5000,v=>p.IdleCenter=v);
                Number("轨迹灵敏度",p.Sensitivity,.05,10,v=>p.Sensitivity=v);
                Title("固定面板 · 自适应轨迹", "大幅移动时轨迹自动等比缩小，窗口与面板大小不变；连续移动中不突然放大，静止归中后恢复原比例。"); EditTiles(p.Inputs,true);
                var add = Row(); var keyBox = new TextBox { PlaceholderText = "点击后按要检测的键", Width = 180 };
                keyBox.KeyDown += (_,e) => { keyBox.Text=OverlayPage.KeyName(e.Key,e.KeyStatus); e.Handled=true; }; add.Children.Add(keyBox);
                Button(add,"添加", async () => {
                    if (!Enum.TryParse<VirtualKey>(keyBox.Text,out _) || p.Inputs.Any(t=>t.Id==keyBox.Text) || p.Inputs.Count>=18) { message.Text="请选择未添加的有效按键；最多 16 个键帽。"; return; }
                    p.Inputs.Add(new Tile { Id=keyBox.Text,Label=keyBox.Text.ToUpperInvariant(),X=10,Y=150,Width=48,Height=40,Mode="background" }); await Save(); Build();
                }); LayoutSettings(); break;
            case 4: ConnectionSettings(); break;
            case 5:
                Title("显示性能", "全局目标帧率 · 静止时按需刷新。目标不等于实测 FPS，实际受 Game Bar、UI 调度和显示器刷新率限制；不改变 GSI 发送或鼠标采样频率。");
                Number("目标帧率（FPS，1–240）",p.TargetFps,1,240,v=>p.TargetFps=v);
                var rates=Row();
                foreach(int fps in new[]{30,60,120,144,165,240}) {
                    int value=fps;Button(rates,fps.ToString(),async()=>{p.TargetFps=value;await Save();Build();});
                }
                Title("按需渲染", "同一帧合并数据与输入变化；缓存文字、键帽与图标，复用反馈和轨迹对象；控件隐藏后停止刷新。");
                break;
        }
        building=false; RedrawPreview();
    }
    void CrosshairSettings() {
        var presets = Row();
        void Preset(string label,double length,double gap,double width,bool dot) => Button(presets,label,async()=>{p.CrosshairStyle="cross";p.CrosshairSize=length;p.CrosshairGap=gap;p.CrosshairThickness=width;p.Dot=dot;await Save();Build();});
        Preset("经典十字",7,5,1.5,false); Preset("紧凑",4,2,1,false); Preset("圆点",0,0,2,true);
        Button(presets,"预览背景",()=>{previewBackground=previewBackground=="#1B2530"?"#BCB39C":"#1B2530";previewHost.Background=Visuals.Brush(previewBackground);return Task.CompletedTask;});
        var circles=Row();
        void CirclePreset(string label,string style,bool dot,double radius)=>Button(circles,label,async()=>{p.CrosshairStyle=style;p.Dot=dot;p.CrosshairRadius=radius;await Save();Build();});
        CirclePreset("圆环","circle",false,6);CirclePreset("圆环 + 圆心","circle",true,6);CirclePreset("实心圆","dot",false,2);
        var shape=new ComboBox{Header="准心样式",HorizontalAlignment=HorizontalAlignment.Stretch};
        foreach(var label in new[]{"十字","圆环","实心圆"})shape.Items.Add(label);
        shape.SelectedIndex=p.CrosshairStyle=="circle"?1:p.CrosshairStyle=="dot"?2:0;
        shape.SelectionChanged+=(_,_)=>{if(building)return;Run(async()=>{p.CrosshairStyle=shape.SelectedIndex==1?"circle":shape.SelectedIndex==2?"dot":"cross";await Save();});};content.Children.Add(shape);
        Number("圆形半径（物理 px）",p.CrosshairRadius,.5,60,v=>p.CrosshairRadius=v);
        Number("长度（物理 px）",p.CrosshairSize,0,60,v=>p.CrosshairSize=v);
        Number("间隙（物理 px）",p.CrosshairGap,0,60,v=>p.CrosshairGap=v);
        Number("线宽（物理 px）",p.CrosshairThickness,.5,12,v=>p.CrosshairThickness=v);
        Toggle("中心点",p.Dot,v=>p.Dot=v); Color("准心颜色",p.CrosshairColor,v=>p.CrosshairColor=v);
        Title("位置微调", "每次移动 1 物理像素；归零仅重置偏移");
        var arrows=Row();
        void Nudge(string label,double dx,double dy)=>Button(arrows,label,async()=>{p.OffsetX+=dx;p.OffsetY+=dy;await Save();Build();});
        Nudge("←",-1,0);Nudge("↑",0,-1);Nudge("↓",0,1);Nudge("→",1,0);
        Button(arrows,"归零",async()=>{p.OffsetX=p.OffsetY=0;await Save();Build();});
        Number("水平偏移（px）",p.OffsetX,-200,200,v=>p.OffsetX=v);Number("垂直偏移（px）",p.OffsetY,-200,200,v=>p.OffsetY=v);
        Toggle("几何中心抗锯齿",p.Antialias,v=>p.Antialias=v);
        Toggle("准心随窗口自适应（默认关闭）",p.CrosshairAutoFit,v=>p.CrosshairAutoFit=v);
        Number("准心尺寸比例（偏移仍为 1 px）",p.CrosshairScale,.5,3,v=>p.CrosshairScale=v);
        Title("命中与击杀动效", "击杀由本人累计击杀增加触发。GSI 无可靠的逐次命中/命中部位事件，命中按钮仅用于演示。击杀默认为红色。");
        Number("准心动效额外延迟（ms，0 表示不额外等待）",p.EffectDelay,0,100,v=>p.EffectDelay=v);
        Color("命中颜色",p.HitColor,v=>p.HitColor=v); Color("头部命中颜色",p.HeadColor,v=>p.HeadColor=v);
        Color("击杀颜色",p.KillColor,v=>p.KillColor=v); Color("爆头击杀颜色",p.HeadKillColor,v=>p.HeadKillColor=v);
        Number("反馈标记尺寸（px）",p.EffectSize,8,35,v=>p.EffectSize=v); Number("反馈标记线宽（px）",p.EffectWidth,1,5,v=>p.EffectWidth=v);
        Title("准心反馈时间", "命中演示与实际击杀共用；与击杀提示条独立。三个阶段全为 0 时不显示动效。");
        Number("出现（ms）",p.EffectEnter,0,2000,v=>p.EffectEnter=v);
        Number("停留（ms）",p.EffectHold,0,5000,v=>p.EffectHold=v);
        Number("淡出（ms）",p.EffectExit??p.EffectDuration,0,2000,v=>p.EffectExit=v);
    }
    void ScaleSettings(string id) {
        bool fit=id=="Feed"?p.FeedAutoFit:id=="Stats"?p.StatsAutoFit:p.InputAutoFit;
        double scale=id=="Feed"?p.FeedScale:id=="Stats"?p.StatsScale:p.InputScale;
        Toggle("内容随控件窗口等比例缩放",fit,v=>{if(id=="Feed")p.FeedAutoFit=v;else if(id=="Stats")p.StatsAutoFit=v;else p.InputAutoFit=v;});
        Number("内容比例",scale,.5,3,v=>{if(id=="Feed")p.FeedScale=v;else if(id=="Stats")p.StatsScale=v;else p.InputScale=v;});
    }
    void LayoutSettings() {
        Toggle("拖拽吸附（Alt 临时停用）",p.Snap,v=>p.Snap=v); Number("网格间距",p.Grid,1,40,v=>p.Grid=v);
    }
    void ConnectionSettings() {
        Title("GSI 连接", "仅接收本人官方 GSI；重复启动会保留现有接收器");
        Title("准心无需连接 GSI", "GSI 仅用于实时击杀反馈和统计。这里的 cfg 是额外新增的一份数据输出配置，无需修改原有准心、按键或 autoexec 配置。");
        Title("首次连接步骤", "1. 点击“启动 / 检查接收”。\n2. 打开配置目录，复制 gamestate_integration_reticlelab.cfg。\n3. 将此文件放入 CS2 安装目录的 game\\csgo\\cfg 文件夹。\n4. 完全退出并重新启动 CS2，保持接收器运行。文件内容无需手动编辑。");
        var bar=Row();
        Button(bar,"启动 / 检查接收",async()=>{
            string heartbeat=System.IO.Path.Combine(Store.Root,"bridge-status.json");
            if(File.Exists(heartbeat) && DateTime.UtcNow-File.GetLastWriteTimeUtc(heartbeat)<TimeSpan.FromSeconds(4)) { message.Text="接收器已在运行，无需重复启动。"; return; }
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            message.Text="接收启动请求已发送；等待状态确认…";
            for(int i=0;i<12;i++){
                await Task.Delay(250);
                try {
                    if(File.Exists(heartbeat) && DateTime.UtcNow-File.GetLastWriteTimeUtc(heartbeat)<TimeSpan.FromSeconds(4)) {
                        message.Text="接收器已运行。首次连接请按上方步骤复制 gamestate_integration_reticlelab.cfg，然后重启 CS2。"; return;
                    }
                } catch(IOException) { }
            }
            string error=System.IO.Path.Combine(Store.Root,"bridge-error.txt");
            message.Text=File.Exists(error)?"接收器未就绪："+await File.ReadAllTextAsync(error):"暂未确认接收器就绪，请查看配置目录或诊断日志。";
        });
        Button(bar,"打开配置目录",OpenConfigFolder);
        content.Children.Add(new TextBox { Header="配置目录（可复制到资源管理器地址栏）", Text=Store.Root, IsReadOnly=true, TextWrapping=TextWrapping.Wrap, MaxHeight=85 });
        var damageStatus=new TextBlock {Text="正在检查伤害数据…",TextWrapping=TextWrapping.Wrap,FontSize=12};
        content.Children.Add(damageStatus);
        async Task CheckDamage() {
            var current=await Store.ReadSnapshot();
            damageStatus.Text=current?.Fresh(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())!=true?"伤害数据：暂无新鲜的本人 GSI，请先连接游戏。":
                current.RoundDamage.HasValue?"伤害数据：已收到回合伤害；相邻基线有效时显示更新增量。无法计算增量时显示连杀序号，真实零增量仍显示 0。":
                current.DamageStatus=="missing_state"?"伤害数据：当前 GSI 缺少玩家状态，请确认游戏配置中 player_state 为 1，并重启 CS2。":
                current.DamageStatus=="invalid_value"?"伤害数据：收到了伤害字段，但数值无效；击杀提示改为显示连杀序号。":
                "伤害数据：GSI 当前未提供 round_totaldmg；击杀提示显示连杀序号，伤害统计保持未知。";
        }
        Button(Row(),"检查伤害数据",CheckDamage);Run(CheckDamage);
        var files=Row();Button(files,"导出配置",Export);Button(files,"导入配置",Import);
        Button(files,"重新载入",()=>{p=Store.Load();savedPreferences=JsonSerializer.Serialize(p);Build();message.Text="已重新载入配置";return Task.CompletedTask;});
        var font=new ComboBox{Header="字体",HorizontalAlignment=HorizontalAlignment.Stretch};font.Items.Add("CS2 Noto Sans SC（含中文）");font.Items.Add("Stratum2（需标准 TTF 文件）");font.SelectedIndex=p.FontFamily=="Stratum2"?1:0;
        font.SelectionChanged+=(_,_)=>{if(building)return;Run(async()=>{if(font.SelectedIndex==1&&!File.Exists(System.IO.Path.Combine(Package.Current.InstalledLocation.Path,"Fonts","Stratum2.ttf"))){message.Text="未打包标准 Stratum2.ttf；使用游戏自带 Noto 字体。";font.SelectedIndex=0;return;}p.FontFamily=font.SelectedIndex==1?"Stratum2":"Noto Sans SC";await Save();});};content.Children.Add(font);
    }
    async Task OpenConfigFolder() {
        if(!await folderLaunchGate.WaitAsync(0)){message.Text="正在打开配置目录，请稍候。";return;}
        try {
            message.Text="正在打开配置目录…";
            // The ordinary UWP launcher has no Game Bar host context. Use the
            // package's fixed desktop command from a widget, not an arbitrary URI.
            if(settingsWidget==null) {
                try {
                    if(await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder).AsTask().WaitAsync(TimeSpan.FromSeconds(3))) {
                        message.Text="已请求打开配置目录；若窗口在后台，请切换到资源管理器。";return;
                    }
                } catch(Exception e) {Store.Log(e);}
            }
            string id=Guid.NewGuid().ToString("N");
            await SharedFile.WriteTextAsync(Path.Combine(Store.Root,ConfigFolderLaunch.RequestFile),JsonSerializer.Serialize(new FolderLaunchRequest(id,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("OpenConfigFolder").AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            for(int i=0;i<25;i++) {
                try {
                    var result=JsonSerializer.Deserialize<FolderLaunchResult>(await SharedFile.ReadTextAsync(Path.Combine(Store.Root,ConfigFolderLaunch.ResultFile)));
                    if(result?.Id==id) {
                        message.Text=result.Succeeded?"已请求打开配置目录。若未看到资源管理器，请按 Win+G 收起 Game Bar。":"目录打开失败："+result.Error+"；可复制上方路径手动打开。";
                        return;
                    }
                } catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) { }
                await Task.Delay(200);
            }
            message.Text="尚未收到打开结果。请按 Win+G 查看资源管理器，或复制上方路径手动打开；可再次点击重试。";
        } catch(Exception e) {Store.Log(e);message.Text="无法打开配置目录："+e.Message+"；可复制上方路径手动打开。";}
        finally {folderLaunchGate.Release();}
    }
    void RedrawPreview() {
        if(page!=0 || previewHost.ActualWidth<1 || previewHost.ActualHeight<1)return;
        preview.Width=previewHost.ActualWidth;preview.Height=previewHost.ActualHeight;
        preview.Children.Clear();
        double dpi=Windows.Graphics.Display.DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
        Visuals.Line(preview,preview.Width/2,28,preview.Width/2,preview.Height-12,.5,"#FFFFFF",.15);
        Visuals.Line(preview,12,preview.Height/2,preview.Width-12,preview.Height/2,.5,"#FFFFFF",.15);
        Visuals.Crosshair(preview,p,dpi,p.CrosshairScale);
    }
    void Run(Func<Task> action) { _=UiThread.Run(Dispatcher,action,e=>message.Text=e.Message); }
    void EditTiles(List<Tile> tiles,bool input) {
        var selector=new ComboBox { Header="选择编辑项",HorizontalAlignment=HorizontalAlignment.Stretch };
        foreach(var t in tiles) selector.Items.Add(t.Label+" · "+t.Id);
        var editor=new StackPanel { Spacing=7 }; content.Children.Add(selector); content.Children.Add(editor);
        selector.SelectionChanged+=(_,_)=> {
            editor.Children.Clear(); if(selector.SelectedIndex<0||selector.SelectedIndex>=tiles.Count)return;
            Tile t=tiles[selector.SelectedIndex];
            var label=new TextBox { Header="标签",Text=t.Label }; label.LostFocus+=(_,_)=>Run(async()=>{t.Label=label.Text;await Save();});editor.Children.Add(label);
            var modes=input?(t.Id=="trace"?new[]{"always","moving","hidden"}:new[]{"background","pressed","hidden"}):new[]{"inherit","always","round-start","hidden"};
            var mode=new ComboBox { Header="独立显示模式（background=B / pressed=A）",HorizontalAlignment=HorizontalAlignment.Stretch };
            foreach(var value in modes)mode.Items.Add(value);mode.SelectedItem=t.Mode;
            mode.SelectionChanged+=(_,_)=>Run(async()=>{if(mode.SelectedItem is string v){t.Mode=v;await Save();}});editor.Children.Add(mode);
            if(input) { var style=new ComboBox { Header="样式",ItemsSource=new[]{"solid","outline","rounded"},SelectedItem=t.Style };style.SelectionChanged+=(_,_)=>Run(async()=>{if(style.SelectedItem is string v){t.Style=v;await Save();}});editor.Children.Add(style); }
            TileNumber(editor,"X",t.X,0,640,v=>t.X=v);TileNumber(editor,"Y",t.Y,0,400,v=>t.Y=v);
            TileNumber(editor,"宽度",t.Width,24,320,v=>t.Width=v);TileNumber(editor,"高度",t.Height,24,160,v=>t.Height=v);
            if(input&&t.Id!="mouse"&&t.Id!="trace")Button(editor,"删除此键",async()=>{p.Inputs.Remove(t);await Save();Build();});
        };
        if(tiles.Count>0)selector.SelectedIndex=0;
    }
    void Title(string title,string description) { content.Children.Add(Visuals.Text(title,20,"#E6F0C3",p));var t=Visuals.Text(description,11,"#A4ADBA",p);t.TextWrapping=TextWrapping.Wrap;content.Children.Add(t); }
    StackPanel Row(){var r=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};content.Children.Add(r);return r;}
    void Button(Panel parent,string label,Func<Task> action){var b=new Button{Content=label,Padding=new Thickness(9,6,9,6),FontSize=12};b.Click+=(_,_)=>Run(async()=>{b.IsEnabled=false;try{await action();}finally{b.IsEnabled=true;}});parent.Children.Add(b);}
    void Number(string name,double value,double min,double max,Action<double> set)=>TileNumber(content,name,value,min,max,set);
    void TileNumber(Panel parent,string name,double value,double min,double max,Action<double> set){
        var group=new StackPanel{Spacing=4};group.Children.Add(Visuals.Text(name,12,"#CAD3DF",p));
        var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(78)});
        double step=max<=3?.01:name.Contains("线宽")?.5:1;
        var slider=new Slider{Minimum=min,Maximum=max,Value=Math.Clamp(value,min,max),StepFrequency=step,Margin=new Thickness(0,0,12,0)};
        var box=new TextBox{Text=value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture),VerticalAlignment=VerticalAlignment.Center};
        bool updating=false;
        slider.ValueChanged+=(_,_)=>{if(updating||building)return;box.Text=slider.Value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);set(slider.Value);RedrawPreview();Run(Save);};
        box.LostFocus+=(_,_)=>Run(async()=>{if(double.TryParse(box.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double v)&&double.IsFinite(v)){updating=true;v=Math.Clamp(v,min,max);slider.Value=v;box.Text=v.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);updating=false;set(v);await Save();}else message.Text="请输入有效数字";});
        row.Children.Add(slider);Grid.SetColumn(box,1);row.Children.Add(box);group.Children.Add(row);parent.Children.Add(group);
    }
    void Toggle(string name,bool value,Action<bool> set){var box=new CheckBox{Content=name,IsChecked=value};box.Click+=(_,_)=>Run(async()=>{set(box.IsChecked==true);await Save();});content.Children.Add(box);}
    void Color(string name,string value,Action<string> set){
        var group=new StackPanel{Spacing=5};var box=new TextBox{Header=name+"（#RRGGBB）",Text=value};
        box.LostFocus+=(_,_)=>Run(async()=>{if(System.Text.RegularExpressions.Regex.IsMatch(box.Text,"^#[0-9a-fA-F]{6}$")){set(box.Text);await Save();}else message.Text="颜色需为 #RRGGBB";});group.Children.Add(box);
        var palette=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};
        foreach(string color in new[]{"#FFFFFF","#E6F0C3","#55EF8D","#49DCEB","#EF554B","#F3CF77","#DF72BD"}){var swatch=new Button{Width=28,Height=25,Padding=new Thickness(0),Background=Visuals.Brush(color)};swatch.Click+=(_,_)=>Run(async()=>{box.Text=color;set(color);await Save();});palette.Children.Add(swatch);}
        group.Children.Add(palette);content.Children.Add(group);
    }
    async Task Save(){
        p.Normalize();RedrawPreview();
        string before=savedPreferences,edited=JsonSerializer.Serialize(p);savedPreferences=edited;
        try {await Store.SaveEdits(before,edited);}
        catch {if(savedPreferences==edited)savedPreferences=before;throw;}
    }
    async Task Export(){var picker=new FileSavePicker{SuggestedFileName="reticle-native"};picker.FileTypeChoices.Add("JSON",new[]{".json"});var file=await picker.PickSaveFileAsync();if(file!=null)await FileIO.WriteTextAsync(file,JsonSerializer.Serialize(p,new JsonSerializerOptions{WriteIndented=true}));}
    async Task Import(){var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".json");var file=await picker.PickSingleFileAsync();if(file==null)return;var text=await FileIO.ReadTextAsync(file);if(text.Length>128*1024)throw new InvalidDataException("配置过大");using var doc=JsonDocument.Parse(text);if(!doc.RootElement.TryGetProperty("Schema",out var schema)||schema.GetInt32()!=1)throw new InvalidDataException("请选择原生 schema 1 配置；HTML 配置请使用转换脚本。");p=JsonSerializer.Deserialize<Preferences>(text)??Preferences.Default();p.Normalize();await Save();Build();}
}
