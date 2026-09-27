using System.Text.Json;
using System.Text.Json.Nodes;

namespace Reticle.Core;

// Merge only controls edited by this view. Other views may have saved positions
// or scale since this settings page was opened.
public static class PreferenceEdits
{
    public static Preferences Merge(Preferences latest,string baseline,string edited) {
        var result=JsonSerializer.SerializeToNode(latest)!.AsObject();
        var before=JsonNode.Parse(baseline)!.AsObject();var after=JsonNode.Parse(edited)!.AsObject();
        foreach(var pair in after) {
            if(JsonNode.DeepEquals(before[pair.Key],pair.Value))continue;
            if(pair.Key is "Stats" or "Inputs" && before[pair.Key] is JsonArray oldTiles && pair.Value is JsonArray newTiles && result[pair.Key] is JsonArray current) {
                string Id(JsonNode? node)=>node?["Id"]?.GetValue<string>()??"";
                var oldIds=oldTiles.Select(Id).ToHashSet();var newIds=newTiles.Select(Id).ToHashSet();
                for(int i=current.Count-1;i>=0;i--)if(oldIds.Contains(Id(current[i]))&&!newIds.Contains(Id(current[i])))current.RemoveAt(i);
                foreach(var tile in newTiles) {
                    string id=Id(tile);var original=oldTiles.FirstOrDefault(t=>Id(t)==id);
                    if(JsonNode.DeepEquals(original,tile))continue;
                    var existing=current.FirstOrDefault(t=>Id(t)==id);
                    if(original==null){if(existing!=null)current.Remove(existing);current.Add(tile!.DeepClone());continue;}
                    if(existing==null)continue; // Respect a concurrent deletion.
                    foreach(var property in tile!.AsObject())
                        if(!JsonNode.DeepEquals(original[property.Key],property.Value))existing[property.Key]=property.Value?.DeepClone();
                }
            } else result[pair.Key]=pair.Value?.DeepClone();
        }
        var merged=result.Deserialize<Preferences>()!;merged.Normalize();return merged;
    }
}
