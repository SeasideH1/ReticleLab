using System.Text.Json;

namespace Reticle.Core;

public sealed record FolderLaunchRequest(string Id,long At);
public sealed record FolderLaunchResult(string Id,bool Succeeded,string Error);

// Fixed local command: the request has no executable, URI or destination path.
public static class ConfigFolderLaunch
{
    public const string RequestFile="folder-open-request.json", ResultFile="folder-open-result.json";
    public static async Task ExecuteAsync(string ownFolder,Func<string,bool> open,long now)
    {
        // Independent of bridge.lock: this command must work with GSI already running.
        using var gate=new FileStream(Path.Combine(ownFolder,"folder-open.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var request=JsonSerializer.Deserialize<FolderLaunchRequest>(await SharedFile.ReadTextAsync(Path.Combine(ownFolder,RequestFile)));
        if(request==null || !Guid.TryParseExact(request.Id,"N",out _) || now<request.At || now-request.At>10000)
            throw new InvalidOperationException("Folder request is invalid or expired.");
        var responsePath=Path.Combine(ownFolder,ResultFile);
        if(File.Exists(responsePath)) {
            try { if(JsonSerializer.Deserialize<FolderLaunchResult>(await SharedFile.ReadTextAsync(responsePath))?.Id==request.Id)return; }
            catch(JsonException) { }
        }
        bool success=false;string error="";
        try {success=open(Path.GetFullPath(ownFolder));if(!success)error="Windows did not accept the folder launch.";}
        catch(Exception e) {error=e.Message;}
        await SharedFile.WriteTextAsync(responsePath,JsonSerializer.Serialize(new FolderLaunchResult(request.Id,success,error)));
    }
}
