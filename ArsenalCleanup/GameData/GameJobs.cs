using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ArsenalCleanup.Triage;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace ArsenalCleanup.GameData;

/// <summary>
/// Reads the game's list of classes and jobs, in both the language the sheets are keyed by and the
/// one the player reads.
///
/// Excluded from coverage: two sheet reads. The rule with a decision in it — keeping the lookup
/// abbreviation apart from the displayed one — is <see cref="JobDirectory"/>'s, and is tested there.
/// </summary>
[ExcludeFromCodeCoverage]
public static class GameJobs
{
    /// <summary>
    /// Builds the directory.
    ///
    /// The English sheet is read explicitly for the abbreviation used to look up ClassJobCategory
    /// columns, because those column names are fixed English identifiers while the sheet's own
    /// abbreviations are translated. On a French client, 26 of the 43 classes abbreviate
    /// differently — Dancer is DNS, Sage is SAG, Monk is MOI — so using the client's abbreviation
    /// silently matched no column for over half the game, and every item those classes can equip
    /// looked equippable by nobody.
    /// </summary>
    public static JobDirectory Read(IDataManager data)
    {
        var english = data.GetExcelSheet<ClassJob>(ClientLanguage.English);
        var local = data.GetExcelSheet<ClassJob>();
        if (english is null || local is null) return JobDirectory.Empty;

        var jobs = new List<JobInfo>();
        foreach (var job in english)
        {
            // Row 0 is "Adventurer": a marker, not a class anyone has a level in.
            if (job.RowId == 0) continue;

            var lookup = job.Abbreviation.ExtractText();
            if (lookup.Length == 0) continue;

            var display = local.TryGetRow(job.RowId, out var localRow)
                ? localRow.Abbreviation.ExtractText()
                : lookup;

            jobs.Add(new JobInfo(
                job.RowId,
                lookup,
                display.Length > 0 ? display : lookup,
                job.ClassJobParent.RowId,
                job.StartingLevel));
        }

        return new JobDirectory(jobs);
    }

    /// <summary>Level per class, with base classes and their jobs reconciled — see <see cref="JobLevelTable"/>.</summary>
    public static Dictionary<uint, int> ReadLevels(IPlayerState playerState, IDataManager data, JobDirectory jobs)
    {
        var sheet = data.GetExcelSheet<ClassJob>();
        if (sheet is null) return [];

        var raw = new Dictionary<uint, int>();
        foreach (var job in jobs.Jobs)
        {
            if (sheet.TryGetRow(job.Id, out var row)) raw[job.Id] = playerState.GetClassJobLevel(row);
        }

        return JobLevelTable.WithLineageFilledIn(raw, jobs.Lineages());
    }
}
