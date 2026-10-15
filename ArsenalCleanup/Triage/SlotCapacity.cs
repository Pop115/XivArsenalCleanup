namespace ArsenalCleanup.Triage;

/// <summary>
/// How many pieces of one slot a character wears at once.
///
/// One, everywhere but the fingers: a character wears two rings. That single exception matters
/// because the triage's whole rule is "something better is already in use". With one ring better
/// than another, the second ring is still on a hand — so the piece it beat is still the one filling
/// the other slot, and calling it replaced would have the player throw away a ring they are wearing.
///
/// Belts are absent for the same reason they are absent everywhere else in this plugin: the slot
/// was removed from the game and nothing occupies it.
/// </summary>
public static class SlotCapacity
{
    /// <summary>
    /// How many of <paramref name="slot"/> can be worn at once. Unknown slots count as one, which is
    /// the cautious answer: it can only make the triage keep more than it strictly must.
    /// </summary>
    public static int For(string slot) => slot switch
    {
        "ring" => 2,
        _ => 1,
    };
}
