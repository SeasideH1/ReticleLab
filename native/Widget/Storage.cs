using System.Text.Json;
using Reticle.Core;
using Windows.Storage;

namespace Reticle.Widget;
static class Store
{
    public static string Root => ApplicationData.Current.LocalFolder.Path;
    static readonly SemaphoreSlim Gate = new(1, 1);
    static readonly SemaphoreSlim SnapshotGate = new(1, 1);
    static DateTime snapshotStamp;
    static Snapshot? cachedSnapshot;
    public static Preferences Load()
    {
        try { var p = JsonSerializer.Deserialize<Preferences>(SharedFile.ReadText(Path.Combine(Root, "preferences.json"))); if (p != null) { p.Normalize(); return p; } }
        catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException) { }
        return Preferences.Default();
    }
    public static async Task Save(Preferences p)
    {
        p.Normalize(); await Gate.WaitAsync();
        try {
            await SharedFile.WriteTextAsync(Path.Combine(Root,"preferences.json"),JsonSerializer.Serialize(p,new JsonSerializerOptions{WriteIndented=true}));
        } finally { Gate.Release(); }
    }
    public static async Task Update(Action<Preferences> edit) {
        await Gate.WaitAsync();
        try {
            var latest=Load();edit(latest);latest.Normalize();
            await SharedFile.WriteTextAsync(Path.Combine(Root,"preferences.json"),JsonSerializer.Serialize(latest));
        } finally {Gate.Release();}
    }
    public static async Task SaveEdits(string baseline,string edited) {
        await Gate.WaitAsync();
        try {
            var merged=PreferenceEdits.Merge(Load(),baseline,edited);
            await SharedFile.WriteTextAsync(Path.Combine(Root,"preferences.json"),JsonSerializer.Serialize(merged));
        } finally {Gate.Release();}
    }
    public static Windows.Foundation.Size? LoadWindowSize(string id) {
        if(ApplicationData.Current.LocalSettings.Values.TryGetValue("window-"+id,out var stored) && stored is ApplicationDataCompositeValue value &&
            value.TryGetValue("width",out var w) && value.TryGetValue("height",out var h) && w is double width && h is double height &&
            double.IsFinite(width) && double.IsFinite(height) && width>=160 && height>=120)
            return new Windows.Foundation.Size(Math.Min(width,760),Math.Min(height,520));
        return null;
    }
    public static void SaveWindowSize(string id,Windows.Foundation.Rect bounds) {
        if(!double.IsFinite(bounds.Width)||!double.IsFinite(bounds.Height)||bounds.Width<160||bounds.Height<120)return;
        ApplicationData.Current.LocalSettings.Values["window-"+id]=new ApplicationDataCompositeValue {{"width",bounds.Width},{"height",bounds.Height}};
    }
    public static async Task<Snapshot?> ReadSnapshot()
    {
        await SnapshotGate.WaitAsync();
        try {
            string path=Path.Combine(Root,"snapshot.json");
            var stamp=File.GetLastWriteTimeUtc(path);
            if(cachedSnapshot!=null && stamp==snapshotStamp)return cachedSnapshot;
            var next=JsonSerializer.Deserialize<Snapshot>(await SharedFile.ReadTextAsync(path));
            snapshotStamp=stamp;cachedSnapshot=next;return next;
        }
        catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException) { return null; }
        finally {SnapshotGate.Release();}
    }
    public static async Task<MouseState?> ReadMouse() {
        try {return JsonSerializer.Deserialize<MouseState>(await SharedFile.ReadTextAsync(Path.Combine(Root,"mouse-state.json")));}
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) {return null;}
    }
    public static void Log(Exception e) {
        try { File.WriteAllText(Path.Combine(Root, "last-error.txt"), Stamp() + " " + e); } catch { }
    }
    public static void Feedback(string widget,long sourceAt,long deliveredAt) {
        string path=Path.Combine(Root,"feedback-"+widget+".txt");
        string line=Stamp()+$" source_received_at={sourceAt} ui_consumed_at={deliveredAt} receive_to_ui_ms={deliveredAt-sourceAt}";
        // Diagnostic is off the UI path; it measures delivery, not game action-to-photon latency.
        _=Task.Run(async()=>{try{await File.WriteAllTextAsync(path,line);}catch(IOException){}catch(UnauthorizedAccessException){}});
    }
    static string Stamp() {
        var v = Windows.ApplicationModel.Package.Current.Id.Version;
        return $"{DateTimeOffset.Now:O} version={v.Major}.{v.Minor}.{v.Build}.{v.Revision} pid={Environment.ProcessId} thread={Environment.CurrentManagedThreadId}";
    }
    public static void Startup(string stage) {
        try {
            string line = Stamp() + " " + stage;
            File.WriteAllText(Path.Combine(Root, "last-startup.txt"), line);
            string history = Path.Combine(Root, "startup-history.txt");
            // Keep recent startup phases without mixing them with stale last-error entries.
            if (File.Exists(history) && new FileInfo(history).Length > 32 * 1024) File.WriteAllText(history, "");
            File.AppendAllText(history, line + Environment.NewLine);
        } catch { }
    }
}
