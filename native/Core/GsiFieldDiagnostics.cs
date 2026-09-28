using System.Text.Json;

namespace Reticle.Core;

// Opt-in field inventory. Never persists complete requests or identity/auth values.
public sealed class GsiFieldDiagnostics
{
    public long Packets { get; private set; }
    public long SelfPackets { get; private set; }
    public long StatePackets { get; private set; }
    public long DamagePackets { get; private set; }
    public long LastReceivedAt { get; private set; }
    public SortedDictionary<string, Field> Fields { get; } = new(StringComparer.Ordinal);
    public sealed class Field
    {
        public long Seen { get; set; }
        public SortedSet<string> Types { get; } = new();
        public object? LastValue { get; set; }
    }
    public void Observe(JsonElement root, long now)
    {
        if (GsiReducer.Num(GsiReducer.Obj(root,"provider"),"appid") != 730) return;
        Packets++; LastReceivedAt=now;
        var provider=GsiReducer.Obj(root,"provider");
        var player=GsiReducer.Obj(root,"player");
        string? owner=GsiReducer.Str(provider,"steamid");
        bool self=!string.IsNullOrEmpty(owner) && owner==GsiReducer.Str(player,"steamid");
        if(self) {
            SelfPackets++;
            var state=GsiReducer.Obj(player,"state");
            if(state.ValueKind==JsonValueKind.Object)StatePackets++;
            if(GsiReducer.Obj(state,"round_totaldmg").ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))DamagePackets++;
        }
        Walk(root,"",0,self);
    }
    void Walk(JsonElement value,string path,int depth,bool self)
    {
        if(path.Length>0) {
            if(!Fields.TryGetValue(path,out var field)) {
                if(Fields.Count>=512)return;
                Fields[path]=field=new Field();
            }
            field.Seen++;field.Types.Add(value.ValueKind.ToString());field.LastValue=null;
            bool numeric=path.StartsWith("player.state.") || path.StartsWith("player.match_stats.") ||
                path.StartsWith("player.weapons.") || path.StartsWith("map.") || path is "provider.appid" or "provider.timestamp";
            if(numeric && value.ValueKind==JsonValueKind.Number && value.TryGetDecimal(out var n))field.LastValue=n;
            else if(numeric && value.ValueKind is JsonValueKind.True or JsonValueKind.False)field.LastValue=value.GetBoolean();
            else if(path is "player.activity" or "player.team" or "map.mode" or "map.phase" or "round.phase" ||
                path.StartsWith("player.weapons.") && (path.EndsWith(".name") || path.EndsWith(".type") || path.EndsWith(".state")))
                field.LastValue=value.ValueKind==JsonValueKind.String ? value.GetString() : null;
        }
        // Other-player payloads and delta blocks are inventoried only at their root.
        if(depth>=5 || value.ValueKind!=JsonValueKind.Object ||
            path.Length>0 && !(path is "provider" or "map" or "round" || path.StartsWith("map.") || path.StartsWith("round.") ||
                self && (path=="player" || path.StartsWith("player."))))return;
        foreach(var property in value.EnumerateObject()) {
            if(property.Name.Length>64 || property.Name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='_'))continue;
            Walk(property.Value,path.Length==0?property.Name:path+"."+property.Name,depth+1,self);
        }
    }
}
