using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Reticle.Core;

if(args.Contains("--open-config")) {
    // Resolve package identity ourselves, before receiver lock/port/token initialization.
    string ownFolder=PackageState();Directory.CreateDirectory(ownFolder);
    try {
        await ConfigFolderLaunch.ExecuteAsync(ownFolder, folder => {
            var start=new System.Diagnostics.ProcessStartInfo {
                FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),
                UseShellExecute=true
            };
            start.ArgumentList.Add(folder);
            using var process=System.Diagnostics.Process.Start(start);
            return true; // Shell acceptance, not a guarantee the window is foreground.
        },DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    } catch(Exception e) {await File.WriteAllTextAsync(Path.Combine(ownFolder,"folder-open-error.txt"),e.ToString());Environment.ExitCode=1;}
    return;
}
if(args.Contains("--mouse-smoke")) {
    using var mouse=new RawMouse();
    var state=mouse.Snapshot();
    if(!state.Enabled)throw new InvalidOperationException("Raw Input registration failed: "+state.Error);
    Console.WriteLine("PASS: message-only window, mouse RIDEV_INPUTSINK registration and snapshot; no synthetic input.");
    return;
}
var stateArg = Array.IndexOf(args, "--state-dir");
var portArg = Array.IndexOf(args, "--port");
int port = portArg >= 0 && portArg + 1 < args.Length ? int.Parse(args[portArg + 1]) : 29841;
if (port < 1024 || port > 65535) throw new ArgumentException("Invalid port");
string stateDir = stateArg >= 0 && stateArg + 1 < args.Length ? Path.GetFullPath(args[stateArg + 1]) : PackageState();
Directory.CreateDirectory(stateDir);
// Prevent duplicate packaged helpers; released by the OS on exit/crash.
FileStream processLock;
try { processLock = new FileStream(Path.Combine(stateDir, "bridge.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
catch (IOException e) when ((e.HResult & 0xffff) is 32 or 33) { return; } // Existing receiver owns this state directory.
using var ownedLock = processLock;
AppDomain.CurrentDomain.UnhandledException += (_, e) => {
    try { File.WriteAllText(Path.Combine(stateDir, "bridge-error.txt"), DateTimeOffset.Now + " " + e.ExceptionObject); } catch { }
};
string tokenPath = Path.Combine(stateDir, "gsi-token.txt");
string token = File.Exists(tokenPath) ? File.ReadAllText(tokenPath).Trim() : Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
if (token.Length != 64 || token.Any(c => !Uri.IsHexDigit(c))) token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
await File.WriteAllTextAsync(tokenPath, token);
await File.WriteAllTextAsync(Path.Combine(stateDir, "gamestate_integration_reticlelab.cfg"), $$"""
"Reticle Lab"
{
  "uri" "http://127.0.0.1:{{port}}/gsi"
  "timeout" "2.0"
  "buffer" "0.0"
  "throttle" "0.03"
  "heartbeat" "1.0"
  "auth" { "token" "{{token}}" }
  "data" {
    "provider" "1"
    "map" "1"
    "round" "1"
    "player_id" "1"
    "player_state" "1"
    "player_match_stats" "1"
    "player_weapons" "1"
  }
}
""");
var builder = WebApplication.CreateSlimBuilder();
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(o => {
    o.Listen(System.Net.IPAddress.Loopback, port);
    o.Limits.MaxRequestBodySize = 128 * 1024;
    o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(3);
    o.Limits.MaxConcurrentConnections = 8;
});
var app = builder.Build();
var reducer = new GsiReducer();
var gate = new SemaphoreSlim(1, 1);
var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
await Save(reducer.Current);
long rateWindow = 0; int requestCount = 0;
app.MapPost("/gsi", async context => {
    if (!await gate.WaitAsync(0)) { context.Response.StatusCode = 429; return; }
    try {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - rateWindow >= 1000) { rateWindow = now; requestCount = 0; }
        if (++requestCount > 40) { context.Response.StatusCode = 429; return; }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, new JsonDocumentOptions { MaxDepth = 32 }, timeout.Token);
        string? incoming = GsiReducer.Str(GsiReducer.Obj(doc.RootElement, "auth"), "token");
        if (incoming == null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(incoming), Encoding.UTF8.GetBytes(token))) {
            context.Response.StatusCode = 403; return;
        }
        if (!reducer.Accept(doc.RootElement, now)) { context.Response.StatusCode = 422; return; }
        await Save(reducer.Current);
        context.Response.StatusCode = 204;
    } catch (JsonException) { context.Response.StatusCode = 400; }
      catch (OperationCanceledException) { context.Response.StatusCode = 408; }
      catch (BadHttpRequestException e) { context.Response.StatusCode = e.StatusCode; }
      catch (Exception e) when(e is IOException or UnauthorizedAccessException) { context.Response.StatusCode = 503; }
    finally { gate.Release(); }
});
app.MapGet("/health", () => Results.Text("Reticle GSI bridge"));
app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () => {
    var stop = app.Lifetime.ApplicationStopping;
    while (!stop.IsCancellationRequested) {
        try {
            await SharedFile.WriteTextAsync(Path.Combine(stateDir,"bridge-status.json"),JsonSerializer.Serialize(new { Status = "running", Port = port, At = DateTimeOffset.UtcNow }),stop);
            await Task.Delay(1000, stop);
        } catch (OperationCanceledException) { break; }
          catch (Exception e) when(e is IOException or UnauthorizedAccessException) { try { await Task.Delay(1000, stop); } catch (OperationCanceledException) { break; } }
    }
}));
Console.WriteLine($"Reticle Bridge listening on 127.0.0.1:{port}. Config and snapshots: {stateDir}");
using var mouseStop=new CancellationTokenSource();
var mouseWork=RunMouse(mouseStop.Token);
try { await app.RunAsync(); }
catch (Exception e) {
    await File.WriteAllTextAsync(Path.Combine(stateDir, "bridge-error.txt"), DateTimeOffset.Now + " " + e);
    Environment.ExitCode = 1;
}
finally { mouseStop.Cancel(); await mouseWork; }
async Task RunMouse(CancellationToken stop) {
    RawMouse? mouse=null;
    long checkedAt=0; bool enabled=false;
    try {
        while(!stop.IsCancellationRequested) {
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if(now-checkedAt>=250) {
                checkedAt=now;enabled=false;
                try {
                    string lease=Path.Combine(stateDir,"mouse-request.txt");
                    bool visible=File.Exists(lease) && DateTime.UtcNow-File.GetLastWriteTimeUtc(lease)<TimeSpan.FromSeconds(3);
                    if(visible) {
                        var pref=JsonSerializer.Deserialize<Preferences>(await SharedFile.ReadTextAsync(Path.Combine(stateDir,"preferences.json"),stop));
                        enabled=pref?.BackgroundMouse==true;
                    }
                } catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) { }
            }
            if(enabled && mouse==null)mouse=new RawMouse();
            if(!enabled && mouse!=null){mouse.Dispose();mouse=null;}
            var state=mouse?.Snapshot() ?? new MouseState{At=now};
            try {
                await SharedFile.WriteTextAsync(Path.Combine(stateDir,"mouse-state.json"),JsonSerializer.Serialize(state),stop);
            } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {
                try {await File.WriteAllTextAsync(Path.Combine(stateDir,"mouse-error.txt"),DateTimeOffset.Now+" Mouse state publication will retry: "+e.Message,stop);}
                catch(Exception logError) when(logError is IOException or UnauthorizedAccessException) { }
            }
            await Task.Delay(enabled?33:250,stop);
        }
    } catch(OperationCanceledException) { }
    finally {mouse?.Dispose();}
}
async Task Save(Snapshot value) {
    await SharedFile.WriteTextAsync(Path.Combine(stateDir,"snapshot.json"),JsonSerializer.Serialize(value,jsonOptions));
}
static string PackageState() {
    uint size = 0;
    int code = GetCurrentPackageFamilyName(ref size, null);
    if (code != 122) throw new InvalidOperationException("Unpackaged run requires --state-dir <directory>.");
    var family = new StringBuilder((int)size);
    if (GetCurrentPackageFamilyName(ref size, family) != 0) throw new InvalidOperationException("Package identity unavailable.");
    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", family.ToString(), "LocalState");
}
[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
static extern int GetCurrentPackageFamilyName(ref uint length, StringBuilder? name);
