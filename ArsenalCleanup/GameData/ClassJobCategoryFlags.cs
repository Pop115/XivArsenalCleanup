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
/// through reflection, cached per abbreviation, rather than a hand-written switch: when a new job
/// ships with an expansion, Lumina gains the column and this keeps working untouched, where a
/// switch would silently answer "no" for the new job until someone noticed.
///
/// The reflection cost is paid once per abbreviation for the lifetime of the plugin.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ClassJobCategoryFlags
{
    private static readonly ConcurrentDictionary<string, PropertyInfo?> Columns = new(StringComparer.Ordinal);

    /// <summary>
    /// True when <paramref name="abbreviation"/> (a ClassJob's own abbreviation, e.g. "PGL") is
    /// flagged on this category. Unknown abbreviations read as false — a job the sheet has no
    /// column for cannot be granted equip rights by it.
    /// </summary>
    public static bool IsSet(ClassJobCategory category, string abbreviation)
    {
        var column = Columns.GetOrAdd(abbreviation, static name =>
            typeof(ClassJobCategory).GetProperty(name, BindingFlags.Public | BindingFlags.Instance));

        return column is not null
            && column.PropertyType == typeof(bool)
            && column.GetValue(category) is true;
    }
}
