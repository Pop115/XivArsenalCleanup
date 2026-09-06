using System.Collections.Generic;

namespace ArsenalCleanup.Triage;

/// <summary>One class or job, as far as the triage is concerned.</summary>
/// <param name="ParentId">
/// The base class a job grows out of — Archer for Bard, Gladiator for Paladin. A class with no
/// antecedent points at itself.
/// </param>
public sealed record JobLineage(uint Id, uint ParentId);

/// <summary>
/// Builds the "level per job" map the triage compares against.
/// </summary>
public static class JobLevelTable
{
    /// <summary>
    /// Fills in a job whose own level reads as zero from its base class, since the two share a
    /// single level in game.
    ///
    /// Why this is needed: a level-41 Bard is a level-41 Archer — one experience bar, two rows. Any
    /// source that reports only the unlocked job leaves the base class at zero, and every item both
    /// can equip (most of an Archer's gear) would then be judged against a level of zero and come
    /// out unjudgeable. A job genuinely never started stays at zero, because its base class is zero
    /// too: propagation can only ever raise a level from a shared bar, never invent one.
    /// </summary>
    public static Dictionary<uint, int> WithLineageFilledIn(
        IReadOnlyDictionary<uint, int> rawLevels,
        IReadOnlyList<JobLineage> jobs)
    {
        var levels = new Dictionary<uint, int>(rawLevels.Count);
        foreach (var (jobId, level) in rawLevels) levels[jobId] = level;

        foreach (var job in jobs)
        {
            var known = levels.TryGetValue(job.Id, out var own) ? own : 0;
            if (known > 0) continue;

            if (levels.TryGetValue(job.ParentId, out var parentLevel) && parentLevel > 0)
            {
                levels[job.Id] = parentLevel;
            }
        }

        // The reverse direction matters just as much: a source reporting only "Bard 41" leaves
        // Archer absent entirely, and Archer is the row most of that gear is filed under.
        foreach (var job in jobs)
        {
            if (job.ParentId == job.Id) continue;
            var parentKnown = levels.TryGetValue(job.ParentId, out var parent) ? parent : 0;
            if (parentKnown > 0) continue;

            if (levels.TryGetValue(job.Id, out var childLevel) && childLevel > 0)
            {
                levels[job.ParentId] = childLevel;
            }
        }

        return levels;
    }
}
