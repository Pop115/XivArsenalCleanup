using System.Collections.Generic;
using System.Linq;

namespace ArsenalCleanup.Triage;

/// <summary>
/// One class or job, in the two forms this plugin needs it.
/// </summary>
/// <param name="LookupAbbreviation">
/// The <em>English</em> abbreviation, which is what the game's ClassJobCategory sheet names its
/// columns after. These identifiers never change with the client's language.
/// </param>
/// <param name="DisplayAbbreviation">
/// The abbreviation in the player's own language — "DNS" for Dancer on a French client, "SGE"
/// against "SAG" for Sage. This is what goes on screen, because it is what the player sees
/// everywhere else in the game.
/// </param>
/// <param name="ParentId">The base class a job grows out of; a class with no antecedent points at itself.</param>
public sealed record JobInfo(uint Id, string LookupAbbreviation, string DisplayAbbreviation, uint ParentId);

/// <summary>
/// Every class and job the game knows, and the bridge between the two abbreviations.
///
/// Keeping those apart is not pedantry. Reading a job's abbreviation in the client's language and
/// then using it to look up a ClassJobCategory column works perfectly in English and silently fails
/// for 26 of the 43 classes in French — those jobs match no column, so every item they can equip
/// looks equippable by nobody, and the triage quietly judges half the wardrobe wrong.
/// </summary>
public sealed class JobDirectory
{
    private readonly Dictionary<uint, JobInfo> byId;

    public JobDirectory(IReadOnlyList<JobInfo> jobs)
    {
        Jobs = jobs;
        byId = jobs.ToDictionary(j => j.Id);
    }

    /// <summary>An empty directory, for the state before the game has been read.</summary>
    public static readonly JobDirectory Empty = new([]);

    public IReadOnlyList<JobInfo> Jobs { get; }

    /// <summary>How many classes and jobs exist, so "every one of them" can be recognised and said as such.</summary>
    public int Count => Jobs.Count;

    /// <summary>
    /// The abbreviation to show for a job, or a legible placeholder when the id is unknown — a job
    /// added by a patch this build predates should read as odd, never crash a tooltip.
    /// </summary>
    public string Display(uint jobId)
    {
        if (!byId.TryGetValue(jobId, out var job)) return $"#{jobId}";
        if (job.DisplayAbbreviation.Length > 0) return job.DisplayAbbreviation;
        return job.LookupAbbreviation.Length > 0 ? job.LookupAbbreviation : $"#{jobId}";
    }

    public IReadOnlyList<JobLineage> Lineages() => [.. Jobs.Select(j => new JobLineage(j.Id, j.ParentId))];
}
