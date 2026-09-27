using System.Text;
namespace Reticle.Core;

// Atomic publishers must be allowed to rename a snapshot while readers hold the old file.
public static class SharedFile
{
    public static string ReadText(string path) {
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var reader=new StreamReader(file,Encoding.UTF8,true);
        return reader.ReadToEnd();
    }
    public static async Task<string> ReadTextAsync(string path,CancellationToken stop=default) {
        await using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,4096,true);
        using var reader=new StreamReader(file,Encoding.UTF8,true);
        return await reader.ReadToEndAsync(stop);
    }
    public static async Task WriteTextAsync(string path,string text,CancellationToken stop=default) {
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            await File.WriteAllTextAsync(temp,text,stop);
            for(int attempt=0;;attempt++) {
                stop.ThrowIfCancellationRequested();
                try {
                    if(File.Exists(path))File.Replace(temp,path,null);
                    else File.Move(temp,path);
                    break;
                }
                catch(Exception e) when(attempt<5 && (e is IOException or UnauthorizedAccessException)) {
                    await Task.Delay(10,stop);
                }
            }
        } finally {try{File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
