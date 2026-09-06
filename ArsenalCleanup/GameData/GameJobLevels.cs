using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ArsenalCleanup.Triage;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace ArsenalCleanup.GameData;

/// <summary>
/// Asks the game what level the character is on every class and job.
///
/// Excluded from coverage: it is two calls into services that only exist inside the game process.
/// The rule with a decision in it — what to do when a job and its base class disagree — lives in
/// <see cref="JobLevelTable"/> and is tested there.
/// </summary>
[ExcludeFromCodeCoverage]
public static class GameJobLevels
{
    /// <summary>Level per ClassJob row id, with base classes and their jobs reconciled.</summary>
    public static Dictionary<uint, int> Read(IDataManager data, IPlayerState playerState)
    {
        var sheet = data.GetExcelSheet<ClassJob>();
        if (sheet is null) return [];

        var raw = new Dictionary<uint, int>();
        var lineages = new List<JobLineage>();

        foreach (var job in sheet)
        {
            if (job.RowId == 0) continue; // row 0 is "Adventurer", not a class anyone levels

            lineages.Add(new JobLineage(job.RowId, job.ClassJobParent.RowId));
            raw[job.RowId] = playerState.GetClassJobLevel(job);
        }

        return JobLevelTable.WithLineageFilledIn(raw, lineages);
    }
}
