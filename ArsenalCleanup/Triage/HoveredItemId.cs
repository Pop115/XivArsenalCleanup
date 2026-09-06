namespace ArsenalCleanup.Triage;

/// <summary>
/// Normalises the item id the game reports for whatever the player is hovering.
/// </summary>
public static class HoveredItemId
{
    /// <summary>
    /// The game marks a hovered high-quality item by adding a million to its id, rather than by a
    /// separate flag.
    /// </summary>
    public const ulong HighQualityOffset = 1_000_000;

    /// <summary>
    /// The plain catalogue id behind a hovered item, or 0 when nothing is hovered.
    ///
    /// Without stripping the high-quality marker, hovering the HQ version of a piece you own looks
    /// like an item you have never seen — the tooltip would simply say nothing, which reads as the
    /// plugin being broken rather than as the piece being unknown.
    /// </summary>
    public static uint BaseItemId(ulong hoveredItemId)
    {
        if (hoveredItemId == 0) return 0;
        var id = hoveredItemId > HighQualityOffset ? hoveredItemId - HighQualityOffset : hoveredItemId;
        return id > uint.MaxValue ? 0 : (uint)id;
    }
}
