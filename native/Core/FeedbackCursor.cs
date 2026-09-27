namespace Reticle.Core;

// Per-widget delivery cursor; source time filters stale events, presentation time starts animations.
public sealed class FeedbackCursor
{
    string? session;
    long sequence;
    public void Reset() { session = null; sequence = 0; }
    public IReadOnlyList<KillNotice> Take(Snapshot next, long now)
    {
        long newest = next.Events.Count == 0 ? 0 : next.Events.Max(e => e.Id);
        if (session != next.Session || !next.Fresh(now)) {
            session = next.Session; sequence = newest;
            return Array.Empty<KillNotice>();
        }
        var result = next.Events.Where(e => e.Id > sequence && now >= e.At && now - e.At <= 3000)
            .OrderBy(e => e.Id).SelectMany(e => Enumerable.Range(0,Math.Clamp(e.Count,1,64)).Select(_ => new KillNotice {
                Id=e.Id, At=now, Count=1,
                // A mixed aggregate cannot attribute a headshot to a particular kill.
                Headshots=e.Headshots==e.Count?1:0,
                Weapon=e.Weapon, WeaponSource=e.WeaponSource,
                // Each split notice carries the same update-level total, not a share per target.
                Damage=e.Count==1||e.DamageLabel=="更新增量"?e.Damage:null,DamageLabel=e.DamageLabel
            })).ToArray();
        sequence = Math.Max(sequence, newest);
        return result;
    }
}
