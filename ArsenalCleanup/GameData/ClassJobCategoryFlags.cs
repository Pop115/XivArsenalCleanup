using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Lumina.Excel.Sheets;

namespace ArsenalCleanup.GameData;

/// <summary>
/// Reads one job's flag off a ClassJobCategory row.
///
/// That sheet carries a separate boolean column per job abbreviation (GLA, PGL, … PCT), so asking
/// "may this job equip this item?" means reading the column named after the job. The lookup goes
/// through reflection, cached per abbreviation, rather than a hand-written switch, so a job Lumina
/// already names needs no code here at all.
///
/// What reflection cannot do is read a column Lumina has not named yet, and a patch ships the
/// column before the library does: Beastmaster arrived as <c>Unknown0</c> and answered "no" for
/// every row. <see cref="HasColumn"/> is how a caller tells the two apart — a genuine "no" from a
/// column that is not there — and <see cref="JobEquipRights"/> is what reads the missing ones by
/// position instead.
///
/// The reflection cost is paid once per abbreviation for the lifetime of the plugin.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ClassJobCategoryFlags
{
    private static readonly ConcurrentDictionary<string, PropertyInfo?> Columns = new(StringComparer.Ordinal);

    /// <summary>
    /// True when <paramref name="abbreviation"/> (a ClassJob's own abbreviation, e.g. "PGL") is
    /// flagged on this category. An abbreviation with no column of its own reads as false; ask
    /// <see cref="HasColumn"/> first when the difference matters.
    /// </summary>
    public static bool IsSet(ClassJobCategory category, string abbreviation) =>
        Column(abbreviation)?.GetValue(category) is true;

    /// <summary>
    /// True when this build of Lumina has a column named after <paramref name="abbreviation"/>.
    ///
    /// Without this, a class whose column is missing is indistinguishable from one the category
    /// genuinely excludes — which is how a whole job can go quietly missing from the triage for a
    /// patch cycle.
    /// </summary>
    public static bool HasColumn(string abbreviation) => Column(abbreviation) is not null;

    private static PropertyInfo? Column(string abbreviation)
    {
        var column = Columns.GetOrAdd(abbreviation, static name =>
            typeof(ClassJobCategory).GetProperty(name, BindingFlags.Public | BindingFlags.Instance));

        return column?.PropertyType == typeof(bool) ? column : null;
    }
}
