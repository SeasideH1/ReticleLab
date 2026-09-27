using System.Text.Json;
using Reticle.Core;

if (args.Length == 2 && args[0] == "--entrypoint") { EntryPointAudit.Verify(args[1]); return; }
if (args.Length == 2 && args[0] == "--native-boundary") { NativeBoundaryAudit.Verify(args[1]); return; }
if (args.Length == 3 && args[0] == "--prepare-gamebar") { GameBarPackaging.Prepare(args[1], args[2]); return; }
if (args.Length == 3 && args[0] == "--audit-gamebar") { GameBarPackaging.Verify(args[1], args[2]); return; }

int count = 0;
await ViewContextSmoke.Verify();
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS " + name); count++; }
long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
Check(Geometry.OverlayOpacity(1,false)==1,"pinned full opacity remains visible, not one percent");
Check(Geometry.OverlayOpacity(.5,false)==.5 && Geometry.OverlayOpacity(0,false)==0,"pinned opacity follows Game Bar slider");
Check(Geometry.OverlayOpacity(0,true)==1,"foreground editor stays visible with transparent pinned setting");
Check(Geometry.OverlayOpacity(double.NaN,false)==1,"invalid host opacity cannot break XAML rendering");
Check(Geometry.LayoutScale(880,420,440,210,true,1)==2,"content fits a doubled widget window");
Check(Geometry.LayoutScale(880,420,440,210,false,1)==1,"fixed size ignores widget resize");
Check(Geometry.LayoutScale(440,210,440,210,true,1.5)==1.5,"manual multiplier combines with adaptive scale");
Check(Geometry.PhysicalOffset(1,1.5*2)*1.5*2==1,"scaled tile nudge remains one physical pixel");
Check(!Preferences.Default().CrosshairAutoFit && Preferences.Default().CrosshairScale==1,"crosshair defaults to physical size");
JsonElement Payload(int kills=0, int hs=0, int damage=0, int round=2, int money=3000, string phase="live", string player="self", bool state=true, long? timestamp=null, string weapon="weapon_ak47") {
    return JsonSerializer.SerializeToElement(new {
        provider=new{appid=730,steamid="self",timestamp=timestamp??now/1000},
        map=new{name="de_mirage",round,phase="live"}, round=new{phase},
        player=new{steamid=player,activity="playing",state=state?(object)new{round_kills=kills,round_killhs=hs,round_totaldmg=damage,money}:null,
            match_stats=new{kills=kills+4,deaths=2,assists=1},weapons=new{weapon_0=new{name=weapon,state="active"}}}
    });
}
var reducer=new GsiReducer();
Check(reducer.Accept(Payload(kills:2,damage:100),now),"accept valid self snapshot");
Check(reducer.Current.Events.Count==0,"initial baseline never replays prior kills");
Check(reducer.Current.NetMoney==null && reducer.Current.RecordedDamage==0,"mid-round join does not invent money baseline or prior damage");
reducer.Accept(Payload(kills:3,hs:1,damage:180),now+100);
Check(reducer.Current.Events.Count==1 && reducer.Current.Events[0].Headshots==1,"confirmed self counter increment yields head kill event");
Check(reducer.Current.Events[0].Weapon=="AK47" && reducer.Current.Events[0].WeaponSource=="当前手持" && reducer.Current.Events[0].Damage==80 && reducer.Current.Events[0].DamageLabel=="更新增量","active weapon fallback and update-scoped damage are labeled");
Check(reducer.Current.RecordedDamage==80,"damage delta recorded exactly once");
reducer.Accept(Payload(kills:3,hs:1,damage:180),now+200);
Check(reducer.Current.Events.Count==1 && reducer.Current.RecordedDamage==80,"duplicate update creates no extra kill/damage");
reducer.Accept(Payload(kills:3,hs:1,damage:190),now+300);
Check(reducer.Current.Events.Count==1,"damage increase is not misrepresented as a hit event");
reducer.Accept(Payload(round:3,money:4500,phase:"freezetime"),now+400);
Check(reducer.Current.NetMoney==0 && reducer.Current.Events.Count==0,"round boundary resets events and records observed freeze baseline");
reducer.Accept(Payload(round:3,money:1800,phase:"freezetime"),now+500);
Check(reducer.Current.NetMoney==-2700,"purchases count in net money");
reducer.Accept(Payload(kills:1,round:3,player:"spectated"),now+600);
Check(!reducer.Current.IsSelf && reducer.Current.Kills==null && reducer.Current.Events.Count==0,"spectated data clears all self statistics and events");
reducer.Accept(Payload(kills:2,round:3),now+700);
Check(reducer.Current.Events.Count==0,"return from spectating establishes fresh baseline");
reducer.Accept(Payload(kills:5,round:3),now+7000);
Check(reducer.Current.Events.Count==0,"reconnect never replays missed kills");
Check(!reducer.Current.Fresh(now+13000),"stale snapshot expires");
Check(!reducer.Accept(Payload(timestamp:now/1000-50),now+7100),"old provider timestamp rejected");
reducer.Accept(Payload(round:3,state:false),now+7200);
Check(reducer.Current.RoundKills==null && reducer.Current.Money==null,"missing state means unknown not zero");
reducer.Accept(Payload(kills:6,round:3),now+7300);
Check(reducer.Current.Events.Count==1 && reducer.Current.Events[0].Count==6,"valid match counter works even when prior round state was missing");
reducer.Accept(Payload(kills:8,hs:2,round:3),now+7400);
Check(reducer.Current.Events.Count==2 && reducer.Current.Events[^1].Count==2,"coalesced multikill keeps aggregated count without inventing timestamps");
var stranger=JsonSerializer.SerializeToElement(new{provider=new{appid=999,steamid="self",timestamp=now/1000}});
Check(!reducer.Accept(stranger,now+7500),"wrong game rejected");
var phaseReducer=new GsiReducer();phaseReducer.Accept(Payload(round:2,phase:"over"),now);
phaseReducer.Accept(Payload(round:3,phase:"over"),now+100);
phaseReducer.Accept(Payload(round:3,phase:"freezetime",money:5000),now+200);
Check(phaseReducer.Current.NetMoney==0,"freeze transition records baseline even if map round increment arrived earlier");
phaseReducer.Accept(Payload(round:3,phase:"live",kills:2),now+300);
phaseReducer.Accept(Payload(round:3,phase:"live",kills:1),now+400);
Check(phaseReducer.Current.Events.Count==0,"counter rollback establishes baseline instead of replaying old events");
foreach(double dpi in new[]{1,1.25,1.5,2,2.5})Check(Math.Abs(Geometry.PhysicalOffset(1,dpi)*dpi-1)<1e-10,"1 physical pixel at DPI "+dpi);
Check(Geometry.SharpCenter(250,2)==250 && Geometry.SharpCenter(250.5,1)==250.5,"sharp mode aligns even and odd stroke widths correctly");
Check(Math.Abs(Geometry.SharpCenter(250,1)-250)==.5,"pixel-sharp mode exposes unavoidable half-pixel tradeoff");
var t=new Tile{Mode="always"};Check(Geometry.Visible(t,false,true,false,false,false),"per-item always overrides global round-only");
t.Mode="hidden";Check(!Geometry.Visible(t,false,false,true,true,true)&&Geometry.Visible(t,true,false,false,false,false),"hidden items remain editable");
t.Mode="pressed";Check(!Geometry.Visible(t,false,false,false,false,false)&&Geometry.Visible(t,false,false,false,true,false),"independent A mode");
Check(Geometry.SnapAxis(98,40,400,new[]{(100d,40d)},10,6)==100,"drag aligns peer edges");
Check(Geometry.SnapAxis(183,40,400,Array.Empty<(double,double)>(),10,6)==180,"drag aligns canvas center");
Check(Math.Abs(Geometry.QuartOut(.5)-.9375)<1e-10,"quartic ease-out at half time");
var trail=new Trail();trail.Move(10,5,now);var previous=trail.Points[^1];trail.Move(500,-300,now+10);
var a=trail.Points[^2];var b=trail.Points[^1];
Check(Math.Abs(b.x-a.x-500)<1e-10&&Math.Abs(b.y-a.y+300)<1e-10,"large mouse motion preserves raw displacement before view scaling");
Check(trail.X!=45&&trail.Y!=48,"large motion does not reset to center");
trail.Move(0,0,now+1500);trail.Tick(now+2100,1200,2000);
Check(trail.X==45&&trail.Y==48&&trail.Points.Count==0,"idle recenter not postponed by zero-motion samples");
var preferences=Preferences.Default();preferences.OffsetX=double.NaN;preferences.Grid=-1;preferences.Normalize();
Check(preferences.OffsetX==0&&preferences.Grid==1,"malformed numeric settings bounded");
JsonElement Community(int total,int local,int round=1,string phase="live",int? damage=null) {
    var node=System.Text.Json.Nodes.JsonNode.Parse(Payload(kills:local,round:round,phase:phase).GetRawText())!;
    node["map"]!["name"]="5e_1v1_inferno";
    node["player"]!["match_stats"]!["kills"]=total;
    node["player"]!["state"]!["round_totaldmg"]=damage;
    return JsonSerializer.SerializeToElement(node);
}
var community=new GsiReducer();community.Accept(Community(23,1),now);
var delivery=new FeedbackCursor();Check(delivery.Take(community.Current,now).Count==0,"widget opens without replaying historical kills");
community.Accept(Community(24,0),now+100);
Check(community.Current.Events.Count==1 && community.Current.Events[0].Count==1,"community respawn resets round counter without losing match kill");
Check(community.Current.RoundDamage==null && community.Current.RecordedDamage==null,"community missing damage remains unknown");
community.Accept(Community(24,0,2,"freezetime"),now+200);
Check(community.Current.Events.Count==1,"last kill survives immediate round transition before widget polls");
var visible=delivery.Take(community.Current,now+900);
Check(visible.Count==1 && visible[0].At==now+900 && community.Current.Events[0].At==now+100,"delayed UI starts full animation at presentation time without mutating source time");
Check(delivery.Take(community.Current,now+950).Count==0,"same event cannot replay in subsequent polls");
community.Accept(Community(26,0,2),now+1000);
Check(community.Current.Events[^1].Count==2,"match multi-kill survives unchanged community round counter");
Check(delivery.Take(community.Current,now+4500).Count==0,"too-old feedback is consumed without late animation");
community.Accept(Community(30,0,2),now+9000);
Check(community.Current.Events.Count==0,"reconnection establishes new match-counter baseline");
var dmg=new GsiReducer();dmg.Accept(Community(5,0,damage:0),now);
dmg.Accept(Community(6,1,damage:80),now+100);dmg.Accept(Community(7,1,2,damage:40),now+200);
Check(dmg.Current.RecordedDamage==120,"continuous new-round live packet retains already-reported new damage");
var mouse=new MouseAccumulator();mouse.Apply(1000,-800,0,1,now);mouse.Apply(-50,40,0,2,now+1);
Check(mouse.State.X==950&&mouse.State.Y==-760&&!mouse.State.Left&&mouse.State.LeftAt==now,"raw relative motion accumulates and short click survives a polling interval");
mouse.Apply(65535,65535,1,4,now+2);
Check(mouse.State.X==950&&mouse.State.Right,"absolute device input does not become a huge relative trajectory");
mouse.Apply(0,0,0,8|16|0x400,now+3);mouse.Apply(0,0,0,32,now+4);
Check(!mouse.State.Right&&!mouse.State.Middle&&mouse.State.WheelAt==now+3,"raw button releases and wheel state are independent");
var legacy=JsonSerializer.Deserialize<Preferences>("{\"EffectDuration\":836}")!;legacy.Normalize();
Check(legacy.EffectDelay==0 && legacy.EffectEnter==0 && legacy.EffectHold==0 && legacy.EffectExit==836,"legacy feedback keeps its complete fade duration");
var delayed=CrosshairEffect.Schedule(now,100,true,true);
Check(delayed.Progress(now+99,350)==null && delayed.Opacity(now+99,50,100,200)==0,"delayed feedback stays invisible throughout the wait");
Check(delayed.Progress(now+100,350)==0 && !delayed.Expired(now+100,350),"100ms delay starts a full timeline rather than consuming duration");
Check(delayed.Opacity(now+125,50,100,200)>.9 && delayed.Opacity(now+200,50,100,200)==1,"feedback enters and holds at full opacity");
Check(Math.Abs(delayed.Opacity(now+350,50,100,200)-.5)<1e-9 && delayed.Opacity(now+450,50,100,200)==0,"feedback exits after the complete enter and hold phases");
Check(CrosshairEffect.Schedule(now,0,false,false).Opacity(now,0,0,420)==1,"zero extra delay presents the hit demo immediately");
Check(delayed.Opacity(now+100,0,0,0)==0,"all zero phases intentionally hide the effect");
legacy.EffectDelay=150;legacy.EffectEnter=80;legacy.EffectHold=120;legacy.EffectExit=200;legacy.Normalize();
Check(legacy.EffectDelay==100&&legacy.EffectDuration==400,"time normalization clamps delay and derives the full duration");
var fileDir=Path.Combine(AppContext.BaseDirectory,"shared-file-smoke",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fileDir);
string filePath=Path.Combine(fileDir,"state.json");await SharedFile.WriteTextAsync(filePath,"old");
using(var open=new FileStream(filePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
    await SharedFile.WriteTextAsync(filePath,"new");
    Check(await SharedFile.ReadTextAsync(filePath)=="new","atomic snapshot replacement works while a delete-sharing reader is open");
}
bool busy=false;
using(var blocked=new FileStream(filePath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
    try{await SharedFile.WriteTextAsync(filePath,"blocked");}catch(Exception e) when(e is IOException or UnauthorizedAccessException){busy=true;}
}
await SharedFile.WriteTextAsync(filePath,"recovered");
Check(busy && SharedFile.ReadText(filePath)=="recovered" && !Directory.GetFiles(fileDir,"*.tmp").Any(),"busy snapshot fails boundedly, cleans its temporary file and can recover");
var pacer=new FramePacer();
for(int i=0;i<100;i++)pacer.Request();
Check(pacer.Take(0,240,false)&&!pacer.Take(0,240,false)&&!pacer.Pending,"100 invalidations merge into one frame");
pacer.Request();Check(!pacer.Take(4,240,false)&&pacer.Pending&&pacer.Take(4.167,240,false),"240 FPS keeps sub-millisecond precision and retains early invalidation");
Check(pacer.Take(1000,240,true)&&!pacer.Take(1000.1,240,true),"slow frames skip backlog instead of bursting to catch up");
Check(!pacer.Take(2000,240,false),"idle state emits no frames");
pacer.Request();Check(!pacer.Take(1010,30,false)&&pacer.Take(1034,30,false),"custom frame limit applies to pending changes");
Check(Math.Abs(FramePacer.Interval(240).TotalMilliseconds-4.1666667)<.001&&FramePacer.NormalizeFps(double.NaN)==240&&FramePacer.NormalizeFps(0)==1&&FramePacer.NormalizeFps(999)==240,"frame target bounds and 240 FPS interval");
var prefsRoundtrip=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences{TargetFps=144}))!;
prefsRoundtrip.Normalize();Check(prefsRoundtrip.TargetFps==144,"custom FPS survives save and reload");
var pairCursor=new FeedbackCursor();var pairSnapshot=new Snapshot{IsSelf=true,ReceivedAt=now};pairCursor.Take(pairSnapshot,now);
pairSnapshot.Events.Add(new KillNotice{Id=1,At=now+1,Count=2,Headshots=1,Weapon="AK-47",Damage=100});pairSnapshot.ReceivedAt=now+1;
var pair=pairCursor.Take(pairSnapshot,now+2);
Check(pair.Count==2&&pair.All(e=>e.Count==1&&e.Headshots==0&&e.Damage==null)&&!ReferenceEquals(pair[0],pair[1]),"two merged kills produce two independent notices without fabricated individual attribution");
Check(pairCursor.Take(pairSnapshot,now+3).Count==0,"split kill notices still deliver exactly once");
pairSnapshot.Events.Add(new KillNotice{Id=2,At=now+4,Count=2,Headshots=2});pairSnapshot.ReceivedAt=now+4;
Check(pairCursor.Take(pairSnapshot,now+5).All(e=>e.Headshots==1),"all-headshot aggregate can label both independent notices");
var fitTrail=new Trail();fitTrail.Move(10000,-8000,now);fitTrail.Move(-20000,16000,now+1);
Check(fitTrail.ViewScale<.004&&fitTrail.Points.All(v=>{var q=fitTrail.Project(v.x,v.y);return q.x>=8-1e-9&&q.x<=82+1e-9&&q.y>=8-1e-9&&q.y<=88+1e-9;}),"large bidirectional trajectory and origin fit inside unchanged 90x96 viewport");
var v0=fitTrail.Project(fitTrail.Points[0].x,fitTrail.Points[0].y);var v1=fitTrail.Project(fitTrail.Points[1].x,fitTrail.Points[1].y);
Check(Math.Abs((v1.x-v0.x)/(v1.y-v0.y)+1.25)<1e-9,"auto-fit uses one scale and preserves trajectory aspect and direction");
double fitted=fitTrail.ViewScale;fitTrail.Tick(now+1300,1200,2000);fitTrail.Move(1,1,now+1400);
Check(fitTrail.ViewScale==fitted,"expired samples do not cause a sudden zoom-in within a gesture");
fitTrail.Tick(now+3500,1200,2000);Check(fitTrail.ViewScale==1&&fitTrail.X==45&&fitTrail.Y==48,"idle recenters origin and restores normal scale");
for(int i=0;i<1000;i++)fitTrail.Move(30*Math.Sin(i),20*Math.Cos(i),now+4000+i);
Check(fitTrail.Points.Count==180&&fitTrail.Points.All(v=>{var q=fitTrail.Project(v.x,v.y);return q.x>=8-1e-9&&q.x<=82+1e-9&&q.y>=8-1e-9&&q.y<=88+1e-9;}),"bounded history stays within the fixed viewport during continuous movement");
var deltaReducer=new GsiReducer();var deltaCursor=new FeedbackCursor();deltaReducer.Accept(Community(0,0,damage:20),now);deltaCursor.Take(deltaReducer.Current,now);
deltaReducer.Accept(Community(2,2,damage:140),now+10);var splitDelta=deltaCursor.Take(deltaReducer.Current,now+11);
Check(splitDelta.Count==2&&splitDelta.All(e=>e.Damage==120&&e.DamageLabel=="更新增量"),"split multikill preserves one labeled update total without dividing target damage");
deltaReducer.Accept(Community(3,3,damage:140),now+20);Check(deltaReducer.Current.Events[^1].Damage==0,"present unchanged damage counter reports a real zero update");
deltaReducer.Accept(Community(4,4),now+30);Check(deltaReducer.Current.Events[^1].Damage==null,"missing damage never becomes a fabricated zero");
deltaReducer.Accept(Community(5,5,damage:180),now+40);Check(deltaReducer.Current.Events[^1].Damage==null,"first value after missing damage establishes baseline only");
deltaReducer.Accept(Community(6,0,damage:0),now+50);Check(deltaReducer.Current.Events[^1].Damage==null,"respawn rollback does not generate negative or reassigned damage");
deltaReducer.Accept(Community(7,1,round:2,damage:40),now+60);Check(deltaReducer.Current.Events[^1].Damage==null,"round boundary cannot attribute cross-round damage to a kill update");
var history = new GsiReducer();
JsonElement MatchRound(int total, int local, int round, string phase, int money, int? damage, int hs=0) {
    var node=System.Text.Json.Nodes.JsonNode.Parse(Community(total,local,round,phase,damage).GetRawText())!;
    node["player"]!["state"]!["money"]=money;
    node["player"]!["state"]!["round_killhs"]=hs;
    return JsonSerializer.SerializeToElement(node);
}
history.Accept(MatchRound(4,1,1,"over",3000,80),now);
history.Accept(MatchRound(4,0,2,"freezetime",5000,0),now+10);
history.Accept(MatchRound(4,0,2,"freezetime",2300,0),now+20);
history.Accept(MatchRound(6,2,2,"live",2900,180,1),now+30);
history.Accept(MatchRound(6,2,3,"over",6000,190,1),now+40);
Check(history.Current.RecordedDamage==190,"final over-phase damage is counted even when map round advances first");
history.Accept(MatchRound(6,0,3,"over",6000,0),now+50);
history.Accept(MatchRound(6,0,3,"freezetime",6250,0),now+60);
Check(history.Current.LastRound is {Round:2,Kills:2,Headshots:1,Damage:190,NetMoney:1250},"previous round survives over-phase round advance and counter reset, including final settlement");
history.Accept(MatchRound(6,0,3,"freezetime",3500,0),now+70);
Check(history.Current.DisplayRound().Damage==190 && history.Current.DisplayRound().NetMoney==1250 && history.Current.Money==3500,"freeze purchases cannot overwrite last-round statistics; current cash stays live");
var historyWire=JsonSerializer.Deserialize<Snapshot>(JsonSerializer.Serialize(history.Current))!;
Check(historyWire.DisplayRound().Kills==2 && historyWire.DisplayRound().Headshots==1,"previous-round values survive bridge-to-widget serialization");
history.Accept(MatchRound(7,1,3,"live",3800,90),now+80);
Check(history.Current.DisplayRound().Kills==1 && history.Current.DisplayRound().Damage==90,"live phase displays current round again");
history.Accept(MatchRound(7,1,3,"live",3800,90),now+6000);
Check(history.Current.LastRound==null,"connection gap clears unverified historical summary");
var lateJoin=new GsiReducer();lateJoin.Accept(MatchRound(7,0,3,"freezetime",5000,0),now);
Check(lateJoin.Current.DisplayRound().Kills==null,"joining during freeze does not invent a zero-kill previous round");
var previousDamage=new GsiReducer();previousDamage.Accept(Community(0,0),now);
JsonElement WithPrevious(int total, int damage, int prior, string? previousOwner=null) {
    var node=System.Text.Json.Nodes.JsonNode.Parse(Community(total,total,damage:damage).GetRawText())!;
    node["previously"]=JsonSerializer.SerializeToNode(new {player=new {steamid=previousOwner,state=new {round_totaldmg=prior}}});
    return JsonSerializer.SerializeToElement(node);
}
previousDamage.Accept(WithPrevious(1,160,60),now+10);
Check(previousDamage.Current.Events[^1].Damage==100 && previousDamage.Current.RecordedDamage==100,"acknowledged previous self damage supplies missing adjacent baseline");
previousDamage.Accept(WithPrevious(1,160,60),now+20);
Check(previousDamage.Current.Events.Count==1 && previousDamage.Current.RecordedDamage==100,"duplicate previously block cannot double count damage");
previousDamage.Accept(Community(2,2),now+30);
Check(previousDamage.Current.Events[^1].DamageLabel=="GSI 未提供伤害","missing source damage is explicitly explained rather than implied arithmetic failure");
previousDamage.Accept(WithPrevious(3,250,160,"spectated"),now+40);
Check(previousDamage.Current.Events[^1].Damage==null,"previous damage from another player is rejected");
var initialPrevious=new GsiReducer();initialPrevious.Accept(WithPrevious(1,100,0),now);
Check(initialPrevious.Current.Events.Count==0 && initialPrevious.Current.RecordedDamage==0,"first packet previously data cannot replay kills or invent recording history");
var lateRoundIndex=new GsiReducer();lateRoundIndex.Accept(MatchRound(1,1,1,"over",3000,90),now);
lateRoundIndex.Accept(MatchRound(1,0,1,"freezetime",5000,0),now+10);
lateRoundIndex.Accept(MatchRound(1,0,2,"freezetime",2000,0),now+20);
Check(lateRoundIndex.Current.NetMoney==-3000 && lateRoundIndex.Current.LastRound?.Kills==1,"late map round update in freeze cannot replace baseline with post-purchase balance");
var stableTrail=new Trail();var stableMesh=new TrailMesh();
stableTrail.Move(5,3,1000);stableTrail.Move(7,-2,1030);
Check(stableMesh.Update(stableTrail,1200),"trail geometry is generated on new input");
var activeBand=stableMesh.Bands.First(b=>b.Points.Count>1);
var shape=activeBand.Points.ToArray();
var alpha=activeBand.Opacity(1150,1200);
Check(!stableMesh.Update(stableTrail,1200) && activeBand.Points.SequenceEqual(shape),"aging alone never rebuilds or migrates retained trajectory segments");
Check(activeBand.Opacity(1151,1200)<=alpha && Math.Abs(activeBand.Opacity(1151,1200)-alpha)<.001,"trail fades continuously instead of twelve visible opacity steps");
long fadeAnchor=activeBand.FadeAt;stableTrail.Move(1,1,1040);stableMesh.Update(stableTrail,1200);
Check(activeBand.FadeAt==fadeAnchor && activeBand.Opacity(1150,1200)==alpha,"new point in same band cannot brighten older trajectory");
stableTrail.Move(20000,-18000,1080);stableMesh.Update(stableTrail,1200);
Check(stableMesh.Bands.SelectMany(b=>b.Points).All(q=>q.x>=8-1e-9&&q.x<=82+1e-9&&q.y>=8-1e-9&&q.y<=88+1e-9),"retained trail still auto-fits extreme motion inside fixed viewport");
stableTrail.Reset();stableMesh.Update(stableTrail,1200);
Check(stableMesh.Bands.All(b=>b.Points.Count==0&&b.Opacity(1200,1200)==0),"idle or explicit reset clears all retained trail paths");
for(int i=0;i<500;i++){stableTrail.Move(2,-1,2000+i*16);stableTrail.Tick(2000+i*16,1200,2000);stableMesh.Update(stableTrail,1200);}
Check(stableMesh.Bands.Sum(b=>Math.Max(0,b.Points.Count-1))==stableTrail.Points.Count-1,"wrapped time buckets draw every live segment once with no spurious connections");
var motionHistory=new MouseAccumulator();
for(int i=0;i<2000;i++)motionHistory.Apply(1,-1,0,0,1000+i);
Check(motionHistory.State.Motion.Count==128 && motionHistory.State.X==2000 && motionHistory.State.Motion[^1].X==2000,"high-rate mouse history is bounded without losing cumulative displacement");
Check(motionHistory.State.Motion.Zip(motionHistory.State.Motion.Skip(1)).All(pair=>pair.First.Sequence<pair.Second.Sequence&&pair.First.At<pair.Second.At),"coalesced motion samples preserve strict sequence and time order");
var smooth=new MouseTrailPlayback();var smoothTrail=new Trail();
var smoothState=new MouseState {Session="test",Enabled=true,At=1000,X=0,Y=0};
Check(!smooth.Accept(smoothState),"mouse playback starts at current position without replaying old movement");
smoothState.At=1032;smoothState.X=16;smoothState.Y=8;
smoothState.Motion.AddRange(new[]{new MouseMotion(1,1008,8,0),new MouseMotion(2,1016,8,8),new MouseMotion(3,1032,16,8)});
Check(smooth.Accept(smoothState)&&!smooth.Accept(smoothState),"all intermediate turns cross one IPC update exactly once");
smooth.Advance(smoothTrail,1044,1);double halfway=smoothTrail.X;
smooth.Advance(smoothTrail,1048,1);smooth.Advance(smoothTrail,1052,1);
Check(halfway==49 && smoothTrail.X==53 && smoothTrail.Y==52,"display interpolates real intermediate positions between receiver packets");
smooth.Advance(smoothTrail,1056,1);smooth.Advance(smoothTrail,1060,1);smooth.Advance(smoothTrail,1072,1);
Check(smoothTrail.X==61&&smoothTrail.Y==56&&!smooth.Pending&&smoothTrail.Points.Any(q=>q.x==53&&q.y==48)&&smoothTrail.Points.Any(q=>q.x==53&&q.y==56),"smoothed path preserves right-angle turns and lands on the exact sample endpoint");
Check(smoothTrail.Points.Count==4&&!smooth.Advance(smoothTrail,1100,1),"provisional head replacement avoids history growth and predicts no extra movement");
smoothState.Session="restart";smoothState.X=-900;smoothState.Motion.Clear();smoothState.At=1200;
Check(!smooth.Accept(smoothState)&&!smooth.Advance(smoothTrail,1300,1),"receiver restart rebases interpolation without a spurious jump");
var folderFixture=Path.Combine(AppContext.BaseDirectory,"folder launch smoke",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folderFixture);
string requestId=Guid.NewGuid().ToString("N");int opens=0;
await SharedFile.WriteTextAsync(Path.Combine(folderFixture,ConfigFolderLaunch.RequestFile),JsonSerializer.Serialize(new FolderLaunchRequest(requestId,now)));
using(var receiverLock=new FileStream(Path.Combine(folderFixture,"bridge.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
    await ConfigFolderLaunch.ExecuteAsync(folderFixture,path=>{opens++;return path==Path.GetFullPath(folderFixture);},now);
}
var launchResult=JsonSerializer.Deserialize<FolderLaunchResult>(await SharedFile.ReadTextAsync(Path.Combine(folderFixture,ConfigFolderLaunch.ResultFile)))!;
Check(launchResult.Id==requestId&&launchResult.Succeeded&&opens==1,"config directory command succeeds while receiver lock is held, using only its own folder");
await ConfigFolderLaunch.ExecuteAsync(folderFixture,_=>{opens++;return true;},now);
Check(opens==1,"duplicate folder launch request is not executed twice");
bool expiredFolder=false;
try{await ConfigFolderLaunch.ExecuteAsync(folderFixture,_=>{opens++;return true;},now+11000);}catch(InvalidOperationException){expiredFolder=true;}
Check(expiredFolder&&opens==1,"expired folder request cannot launch Explorer");
requestId=Guid.NewGuid().ToString("N");await SharedFile.WriteTextAsync(Path.Combine(folderFixture,ConfigFolderLaunch.RequestFile),JsonSerializer.Serialize(new FolderLaunchRequest(requestId,now)));
await ConfigFolderLaunch.ExecuteAsync(folderFixture,_=>throw new IOException("simulated launch failure"),now);
launchResult=JsonSerializer.Deserialize<FolderLaunchResult>(await SharedFile.ReadTextAsync(Path.Combine(folderFixture,ConfigFolderLaunch.ResultFile)))!;
Check(launchResult.Id==requestId&&!launchResult.Succeeded&&launchResult.Error.Contains("simulated"),"folder launch failure is returned to UI with correlated request ID");
var numericDamage=new GsiReducer();
JsonElement NumericDamage(string value,int kills) {
    string json=Community(kills,kills,damage:0).GetRawText();
    return JsonDocument.Parse(json.Replace("\"round_totaldmg\":0","\"round_totaldmg\":"+value)).RootElement.Clone();
}
numericDamage.Accept(NumericDamage("20.0",0),now);numericDamage.Accept(NumericDamage("120.0",1),now+10);
Check(numericDamage.Current.RoundDamage==120&&numericDamage.Current.Events[^1].Damage==100&&numericDamage.Current.DamageStatus=="available","whole-valued decimal JSON damage is parsed exactly instead of silently dropped");
numericDamage.Accept(NumericDamage("120.5",2),now+20);
Check(numericDamage.Current.DamageStatus=="invalid_value"&&numericDamage.Current.RoundDamage==null,"fractional invalid damage is diagnosed, never rounded into an invented statistic");
numericDamage.Accept(Community(3,3),now+30);
Check(numericDamage.Current.DamageStatus=="not_sent","missing source damage is distinguished from a parsing failure");
var factory=Preferences.Default();
Check(factory.CrosshairSize==3&&factory.CrosshairGap==1&&factory.CrosshairThickness==2&&Math.Abs(factory.InputScale-1.13)<1e-9&&factory.Sensitivity==.05,"factory appearance defaults match the user's captured configuration");
factory.Inputs[0].X=500;Check(Preferences.Default().Inputs[0].X==0,"default layouts are independent objects, never shared mutable state");
var editorBase=Preferences.Default();string beforeEdit=JsonSerializer.Serialize(editorBase);
var editorChanged=JsonSerializer.Deserialize<Preferences>(beforeEdit)!;
editorChanged.FeedWidth=240;editorChanged.Stats[0].Label="Edited label";
var concurrent=JsonSerializer.Deserialize<Preferences>(beforeEdit)!;concurrent.InputScale=1.7;concurrent.Stats[0].X=50;
var mergedEdit=PreferenceEdits.Merge(concurrent,beforeEdit,JsonSerializer.Serialize(editorChanged));
Check(mergedEdit.InputScale==1.7&&mergedEdit.Stats[0].X==50&&mergedEdit.Stats[0].Label=="Edited label"&&mergedEdit.FeedWidth==240,"stale settings view cannot overwrite another widget's saved scale or tile position");
editorChanged.Stats.RemoveAll(t=>t.Id=="assists");
mergedEdit=PreferenceEdits.Merge(mergedEdit,beforeEdit,JsonSerializer.Serialize(editorChanged));
Check(!mergedEdit.Stats.Any(t=>t.Id=="assists")&&mergedEdit.Stats[0].X==50,"explicit tile deletion is saved without resetting unrelated positions");
string intermediate=JsonSerializer.Serialize(editorChanged);var reverted=JsonSerializer.Deserialize<Preferences>(intermediate)!;reverted.FeedWidth=editorBase.FeedWidth;
mergedEdit=PreferenceEdits.Merge(mergedEdit,intermediate,JsonSerializer.Serialize(reverted));
Check(mergedEdit.FeedWidth==editorBase.FeedWidth&&mergedEdit.InputScale==1.7,"rapid slider change then revert persists the final value without reverting another view");
var restoredEdit=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(mergedEdit))!;
Check(restoredEdit.Stats[0].X==50&&restoredEdit.InputScale==1.7,"saved tile positions and scale survive reopen serialization");
Console.WriteLine($"Native core smoke passed: {count} checks.");
