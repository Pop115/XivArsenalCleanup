using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ArsenalCleanup.Triage;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace ArsenalCleanup.GameData;

/// <summary>
/// Which classes and jobs each ClassJobCategory row lets equip a piece.
///
/// The sheet carries one boolean column per class, and the obvious way to read one is by the name
/// the game-data library gives that column — <c>category.PCT</c>. That worked until Beastmaster
/// shipped: the game gained the column, the library did not gain the name, and a reflection lookup
/// for "BST" matched nothing and answered "no" for every row. Sixty Beastmaster-only pieces
/// resolved to no class at all and came out as "check yourself", while every piece shared with
/// another job was judged for that job and never for Beastmaster.
///
/// So the name is no longer the only way in. The columns are laid out in ClassJob row order after a
/// single leading Adventurer column — column index is the class's row id plus one — and that
/// position is read directly for any class the library has no name for.
///
/// That layout is an assumption, so it is checked rather than trusted: every class the library
/// *does* name is read both ways and the two answers compared, across every row of the sheet. Forty
/// three classes agreeing on two hundred rows is what earns the right to read the forty fourth by
/// position. If they ever disagree, the positional reading is dropped and the plugin goes back to
/// names alone — which loses the new job again, but never invents equip rights for a class from a
/// column that turned out to be something else.
///
/// Excluded from coverage: every line reads a Lumina sheet, which cannot be constructed outside a
/// loaded game.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class JobEquipRights
{
    /// <summary>The sheet's first column is Adventurer, which is ClassJob row 0.</summary>
    private const int FirstJobColumn = 1;

    private readonly Dictionary<uint, IReadOnlyList<uint>> byCategory;

    private JobEquipRights(Dictionary<uint, IReadOnlyList<uint>> byCategory)
    {
        this.byCategory = byCategory;
    }

    /// <summary>Nothing known — every category empty. The state before the sheets have been read.</summary>
    public static readonly JobEquipRights Empty = new([]);

    /// <summary>
    /// Every class allowed by one ClassJobCategory row, or nothing at all for a row the sheet does
    /// not have.
    /// </summary>
    public IReadOnlyList<uint> For(uint categoryRowId) =>
        byCategory.TryGetValue(categoryRowId, out var jobs) ? jobs : [];

    public static JobEquipRights Read(IDataManager data, JobDirectory jobs, IPluginLog log)
    {
        var named = data.GetExcelSheet<ClassJobCategory>();
        var positional = data.GetExcelSheet<RawRow>(null, "ClassJobCategory");
        if (named is null || positional is null)
        {
            log.Error("The ClassJobCategory sheet could not be read; no gear can be matched to a class.");
            return Empty;
        }

        var unnamed = UnnamedJobs(jobs);
        var trustPositions = unnamed.Count == 0 || PositionsAgreeWithNames(named, positional, jobs, log);

        if (unnamed.Count > 0)
        {
            var names = string.Join(", ", unnamed.ConvertAll(j => j.LookupAbbreviation));
            if (trustPositions)
            {
                log.Information(
                    "The game data library has no column named for {Jobs}; reading it by position instead.", names);
            }
            else
            {
                log.Warning(
                    "The ClassJobCategory columns are not laid out as expected, so {Jobs} cannot be read at all. "
                    + "Gear only those classes can wear will be reported as needing a look.", names);
            }
        }

        var map = new Dictionary<uint, IReadOnlyList<uint>>();
        foreach (var row in named)
        {
            var allowed = new List<uint>();
            foreach (var job in jobs.Jobs)
            {
                if (Allows(row, positional, job, trustPositions)) allowed.Add(job.Id);
            }

            map[row.RowId] = allowed;
        }

        return new JobEquipRights(map);
    }

    /// <summary>The classes this build of the game data library has no column name for.</summary>
    private static List<JobInfo> UnnamedJobs(JobDirectory jobs)
    {
        var unnamed = new List<JobInfo>();
        foreach (var job in jobs.Jobs)
        {
            if (!ClassJobCategoryFlags.HasColumn(job.LookupAbbreviation)) unnamed.Add(job);
        }

        return unnamed;
    }

    private static bool Allows(ClassJobCategory row, ExcelSheet<RawRow> positional, JobInfo job, bool trustPositions)
    {
        if (ClassJobCategoryFlags.HasColumn(job.LookupAbbreviation))
        {
            return ClassJobCategoryFlags.IsSet(row, job.LookupAbbreviation);
        }

        return trustPositions && ReadByPosition(positional, row.RowId, job.Id) is true;
    }

    /// <summary>
    /// One column read by where it sits rather than what it is called, or null when the sheet is too
    /// narrow for it or the value is not a flag after all.
    /// </summary>
    private static bool? ReadByPosition(ExcelSheet<RawRow> sheet, uint rowId, uint jobId)
    {
        if (!sheet.TryGetRow(rowId, out var row)) return null;

        var column = (int)jobId + FirstJobColumn;
        if (column >= row.Columns.Count) return null;

        return row.ReadColumn(column) as bool?;
    }

    /// <summary>
    /// Reads every named class both ways, on every row, and reports whether the two ever disagree.
    ///
    /// This is the whole safety net for reading an unnamed column by position, so it is deliberately
    /// exhaustive rather than a spot check: a mismatch on a single obscure category is exactly the
    /// shape the assumption would break in.
    /// </summary>
    private static bool PositionsAgreeWithNames(
        ExcelSheet<ClassJobCategory> named, ExcelSheet<RawRow> positional, JobDirectory jobs, IPluginLog log)
    {
        foreach (var row in named)
        {
            foreach (var job in jobs.Jobs)
            {
                if (!ClassJobCategoryFlags.HasColumn(job.LookupAbbreviation)) continue;

                var byName = ClassJobCategoryFlags.IsSet(row, job.LookupAbbreviation);
                var byPosition = ReadByPosition(positional, row.RowId, job.Id);
                if (byPosition == byName) continue;

                log.Warning(
                    "ClassJobCategory row {Row} reads {ByName} for {Job} by name but {ByPosition} at column {Column}.",
                    row.RowId,
                    byName,
                    job.LookupAbbreviation,
                    byPosition?.ToString() ?? "no flag at all",
                    (int)job.Id + FirstJobColumn);
                return false;
            }
        }

        return true;
    }
}
