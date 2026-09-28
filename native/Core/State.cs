using System.Text.Json;

namespace Reticle.Core;

public sealed class KillNotice
{
    public long Id { get; set; }
    public long At { get; set; }
    public int Count { get; set; }
    public int Headshots { get; set; }
    public string Weapon { get; set; } = "—";
    public string WeaponSource { get; set; } = "当前手持";
    // Label describes the scope; an update delta is never target/weapon damage.
    public int? Damage { get; set; }
    public string DamageLabel { get; set; } = "未知伤害";
    // Final ordinal for an aggregate; FeedbackCursor assigns each kill its own ordinal.
    public int? Streak { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string CounterText => Damage.HasValue ? Damage.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : Streak > 0 ? Ordinal(Streak.Value) : "—";
    [System.Text.Json.Serialization.JsonIgnore]
    public string CounterLabel => Damage.HasValue ? DamageLabel : "连杀";
    public static string Ordinal(int number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture) +
        (number % 100 is 11 or 12 or 13 ? "TH" : (number % 10) switch { 1=>"ST",2=>"ND",3=>"RD",_=>"TH" });
}

public sealed class RoundSummary
{
    public int? Round { get; set; }
    public int? Kills { get; set; }
    public int? Headshots { get; set; }
    public int? Damage { get; set; }
    public int? NetMoney { get; set; }
}

public sealed class Snapshot
{
    public int Schema { get; set; } = 1;
    public string Session { get; set; } = Guid.NewGuid().ToString("N");
    public long ReceivedAt { get; set; }
    public bool IsSelf { get; set; }
    public string Status { get; set; } = "等待 GSI";
    public string Map { get; set; } = "";
    public string Mode { get; set; } = "";
    public int? Round { get; set; }
    public string Phase { get; set; } = "";
    public string Weapon { get; set; } = "—";
    public int? Kills { get; set; }
    public int? Deaths { get; set; }
    public int? Assists { get; set; }
    public int? Money { get; set; }
    public int? RoundKills { get; set; }
    public int? RoundHeadshots { get; set; }
    public int? RoundDamage { get; set; }
    public string DamageStatus { get; set; } = "unknown";
    public int? RecordedDamage { get; set; }
    public int? NetMoney { get; set; }
    public RoundSummary? LastRound { get; set; }
    public RoundSummary DisplayRound() => Phase == "freezetime" ? LastRound ?? new RoundSummary() :
        new RoundSummary { Round=Round, Kills=RoundKills, Headshots=RoundHeadshots, Damage=RoundDamage, NetMoney=NetMoney };
    public List<KillNotice> Events { get; set; } = new();
    public bool Fresh(long now) => IsSelf && now >= ReceivedAt && now - ReceivedAt <= 5000;
}

// Only consumes whitelisted local-player snapshot fields. No allplayers, positions or process access.
public sealed class GsiReducer
{
    public Snapshot Current { get; private set; } = new();
    private string? identity, roundKey;
    private int? priorKills, priorHeadshots, baselineMoney, lastDamage;
    private int? priorMatchKills;
    private int? priorDeaths, priorHealth;
    private int lifeKills;
    private int recordedDamage;
    private long lastTimestamp, sequence;
    private bool canCompare;
    private RoundSummary? roundCandidate, completedRound;
    public bool Accept(JsonElement root, long now)
    {
        var provider = Obj(root, "provider");
        if (Num(provider, "appid") != 730) return false;
        long timestamp = Long(provider, "timestamp") ?? 0;
        // A new baseline after a long gap must not replay old kills.
        bool gap = Current.ReceivedAt == 0 || now - Current.ReceivedAt > 5000;
        if (timestamp <= 0 || timestamp < now / 1000 - 30 || timestamp > now / 1000 + 30) return false;
        if (!gap && timestamp < lastTimestamp) return false;
        var player = Obj(root, "player");
        var owner = Str(provider, "steamid");
        var playerId = Str(player, "steamid");
        if (string.IsNullOrEmpty(owner) || owner != playerId || Str(player, "activity") != "playing")
        {
            Current = new Snapshot { Session = Current.Session, ReceivedAt = now, Status = "非本人参战数据", IsSelf = false };
            canCompare = false;
            baselineMoney = null;
            lastDamage = null;
            roundCandidate = completedRound = null;
            lifeKills=0;priorDeaths=priorHealth=null;
            return true;
        }
        var map = Obj(root, "map");
        var state = Obj(player, "state");
        var stats = Obj(player, "match_stats");
        var round = Obj(root, "round");
        string mapName = Str(map, "name") ?? "";
        int? roundNumber = Num(map, "round");
        string phase = Str(round, "phase") ?? "";
        string nextIdentity = owner + ":" + mapName;
        string key = nextIdentity + ":" + roundNumber;
        int? matchKills = Num(stats, "kills");
        bool changedMatch = identity != nextIdentity || (roundNumber < Current.Round) == true ||
            (matchKills < priorMatchKills) == true;
        bool changedRound = roundKey != key;
        bool beganFreeze = Current.Phase != "freezetime" && phase == "freezetime";
        int? kills = Num(state, "round_kills"), hs = Num(state, "round_killhs");
        bool reset = gap || changedMatch || changedRound || beganFreeze || !canCompare || (kills < priorKills) == true;
        int? money = Num(state, "money"), damage = DamageCounter(state);
        string damageStatus=state.ValueKind!=JsonValueKind.Object ? "missing_state" :
            Obj(state,"round_totaldmg").ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? "not_sent" :
            damage.HasValue ? "available" : "invalid_value";
        bool continuous = !gap && !changedMatch && canCompare;
        // GSI's previously block can supply the adjacent acknowledged value when our
        // preceding snapshot lacked it. Never use it across an identity/round reset.
        var previous = Obj(root, "previously");
        var previousPlayer = Obj(previous, "player");
        string? previousOwner = Str(previousPlayer, "steamid");
        int? reportedPreviousDamage = DamageCounter(Obj(previousPlayer, "state"));
        bool samePrevious = (previousOwner == null || previousOwner == owner) &&
            (Num(Obj(previous, "map"), "round") is not int previousRound || previousRound == roundNumber);
        int? damageBaseline = lastDamage ?? (samePrevious ? reportedPreviousDamage : null);
        bool finalCombatUpdate = continuous && phase == "over" && Current.Phase is "live" or "over" &&
            kills.HasValue && priorKills.HasValue && kills >= priorKills;
        int? updateDamage=(!reset || finalCombatUpdate)&&damage.HasValue&&damageBaseline.HasValue&&damage>=damageBaseline
            ? damage.Value-damageBaseline.Value:null;
        if (!continuous) roundCandidate = completedRound = null;
        // map.round can advance while phase is still over. Seal on freeze, before
        // clearing the old money baseline, rather than archiving the reset counters.
        if (continuous && (beganFreeze || (changedRound && phase == "live" && Current.Phase != "freezetime"))) {
            completedRound = roundCandidate;
            if (completedRound != null && beganFreeze && baselineMoney.HasValue && money.HasValue)
                completedRound.NetMoney = money.Value - baselineMoney.Value;
            roundCandidate = null;
        }
        if (changedMatch || gap) recordedDamage = 0;
        if (reset)
        {
            // A first packet halfway through freeze time is not the start-of-round balance.
            if (!(continuous && (phase == "over" || (phase == "freezetime" && Current.Phase == "freezetime"))))
                baselineMoney = !gap && canCompare && (changedRound || beganFreeze) && phase == "freezetime" ? money : null;
            if (updateDamage.HasValue) recordedDamage += updateDamage.Value;
            else if(continuous && changedRound && phase=="live" && roundNumber==Current.Round+1 && damage.HasValue)
                recordedDamage+=damage.Value; // First new-round packet can already contain damage.
            lastDamage = damage;
        }
        else if (updateDamage.HasValue)
        {
            recordedDamage += updateDamage.Value;
            lastDamage = damage;
        }
        else lastDamage = damage;
        var weapon = ActiveWeapon(Obj(player, "weapons"));
        // Match totals survive respawns and round boundaries. Round counters may reset on community maps.
        int delta = continuous && matchKills.HasValue && priorMatchKills.HasValue
            ? Math.Max(0, matchKills.Value - priorMatchKills.Value)
            : !reset && kills.HasValue && priorKills.HasValue ? Math.Max(0, kills.Value - priorKills.Value) : 0;
        int? deaths=Num(stats,"deaths"), health=Num(state,"health");
        bool deathAdvanced=(deaths>priorDeaths)==true;
        bool died=deathAdvanced || health==0 && priorHealth!=0;
        // The final match-kill delta may arrive together with, or after, the
        // round-number advance and cleared round counters. Preserve its ordinal
        // throughout over/freeze; the next live phase starts the new streak.
        bool closingRound = Current.Phase is "live" or "over" or "freezetime" && phase is "over" or "freezetime";
        bool beganLive = phase == "live" && Current.Phase is "over" or "freezetime";
        // A death-count increase while alive means a death/respawn was missed.
        if(!continuous || (!closingRound && (changedRound || beganLive || (kills<priorKills)==true)) ||
            (deathAdvanced && health!=0))lifeKills=0;
        // Retain the final kill across a round boundary until each widget has polled it.
        var events = continuous ? Current.Events.Where(e => now - e.At < 10000).ToList() : new List<KillNotice>();
        if (delta > 0)
        {
            int count = delta;
            int headCount = !reset && hs.HasValue && priorHeadshots.HasValue && kills - priorKills == count
                ? Math.Clamp(hs.Value - priorHeadshots.Value, 0, count) : 0;
            if (count <= 10) {
                lifeKills=(int)Math.Min(int.MaxValue,(long)lifeKills+count);
                events.Add(new KillNotice { Id = ++sequence, At = now, Count = count, Headshots = headCount, Weapon = weapon, Streak=lifeKills,
                Damage=updateDamage,DamageLabel=updateDamage.HasValue ? "更新增量" :
                    !damage.HasValue ? damageStatus=="invalid_value"?"伤害字段无效":"GSI 未提供伤害" : reset ? "跨重置未知" : "缺少相邻基线" });
            }
        }
        // A final trade kill can be displayed with its old-life ordinal; future
        // kills must not inherit that streak after death.
        if(died || health==0)lifeKills=0;
        if (events.Count > 64) events.RemoveRange(0, events.Count - 64);
        Current = new Snapshot {
            Session = changedMatch ? Guid.NewGuid().ToString("N") : Current.Session, ReceivedAt = now, IsSelf = true, Status = "本人 GSI · 已连接",
            Map = mapName, Mode = Str(map, "mode") ?? "", Round = roundNumber, Phase = phase, Weapon = weapon,
            Kills = Num(stats, "kills"), Deaths = Num(stats, "deaths"), Assists = Num(stats, "assists"),
            Money = money, RoundKills = kills, RoundHeadshots = hs, RoundDamage = damage, DamageStatus = damageStatus,
            RecordedDamage = damage.HasValue ? recordedDamage : null,
            NetMoney = baselineMoney.HasValue && money.HasValue ? money.Value - baselineMoney.Value : null,
            Events = events, LastRound = completedRound
        };
        if (phase == "live" || phase == "over") {
            // During over, a later packet may carry cleared counters for the next
            // round. Keep the final combat values; never replace them with zeros.
            if (phase == "live" || roundCandidate == null)
                roundCandidate = new RoundSummary { Round=roundNumber, Kills=kills, Headshots=hs, Damage=damage, NetMoney=Current.NetMoney };
            else {
                if (kills.HasValue && (!roundCandidate.Kills.HasValue || kills >= roundCandidate.Kills)) roundCandidate.Kills=kills;
                if (hs.HasValue && (!roundCandidate.Headshots.HasValue || hs >= roundCandidate.Headshots)) roundCandidate.Headshots=hs;
                if (damage.HasValue && (!roundCandidate.Damage.HasValue || damage >= roundCandidate.Damage)) roundCandidate.Damage=damage;
                if (Current.NetMoney.HasValue) roundCandidate.NetMoney=Current.NetMoney;
            }
        }
        identity = nextIdentity; roundKey = key; priorKills = kills; priorHeadshots = hs; priorMatchKills = matchKills;
        priorDeaths=deaths;priorHealth=health;
        lastTimestamp = timestamp; canCompare = true;
        return true;
    }
    private static string ActiveWeapon(JsonElement weapons)
    {
        if (weapons.ValueKind != JsonValueKind.Object) return "—";
        foreach (var item in weapons.EnumerateObject())
            if (Str(item.Value, "state") == "active")
            {
                string name = Str(item.Value, "name") ?? "";
                return name.StartsWith("weapon_", StringComparison.Ordinal) ? name.Substring(7).ToUpperInvariant() : "—";
            }
        return "—";
    }
    public static JsonElement Obj(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var value) ? value : default;
    private static int? DamageCounter(JsonElement state) {
        var value=Obj(state,"round_totaldmg");
        // Accept an exact whole-valued JSON number such as 100.0; never round,
        // infer damage from score/health, or substitute another player's fields.
        return value.ValueKind==JsonValueKind.Number && value.TryGetDecimal(out decimal n) &&
            n>=0 && n<=int.MaxValue && n==decimal.Truncate(n) ? (int)n : null;
    }
    public static string? Str(JsonElement e, string key) { var v = Obj(e, key); return v.ValueKind == JsonValueKind.String ? v.GetString() : null; }
    public static int? Num(JsonElement e, string key) { var v = Obj(e, key); return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) && n >= 0 ? n : null; }
    public static long? Long(JsonElement e, string key) { var v = Obj(e, key); return v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n) ? n : null; }
}
