namespace ArsenalCleanup.Triage;

/// <summary>
/// Tells a relic apart from ordinary gear, so the triage never suggests parting with one.
/// </summary>
public static class RelicDetection
{
    /// <summary>
    /// True when an item's description opens with a bracketed tag — "[Manderville Weapon]",
    /// "[Antique Weapon]", and the equivalent on every relic line checked, in every language.
    ///
    /// Ordinary raid and tomestone gear never uses that format: it carries flavour text, or the
    /// generic sentence about item level scaling with the wearer. The obvious alternatives are
    /// worse: "unique" and "untradable" are true of nearly all endgame gear and would protect
    /// everything, drowning the result rather than guarding anything.
    /// </summary>
    public static bool LooksLikeRelic(string description) =>
        description.Length > 0 && description[0] == '[';
}
