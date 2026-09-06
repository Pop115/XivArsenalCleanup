using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ArsenalCleanup;

/// <summary>
/// A single inventory item, already stripped down to plain data — deliberately NOT Dalamud's
/// <c>GameInventoryItem</c>, which is a struct with an internal constructor filled from the game's
/// memory and therefore impossible to build in a unit test. <see cref="Plugin"/> is the only place
/// that touches real Dalamud types; everything past this point is pure and tested.
/// </summary>
/// <param name="BaseItemId">
/// The item's catalogue id with the HQ/collectable offset already removed by Dalamud
/// (<c>GameInventoryItem.BaseItemId</c>) — so consumers join on an exact numeric id rather than
/// guessing at a localised display name.
/// </param>
/// <param name="InventorySlot">Raw slot index of the item inside its own container.</param>
public readonly record struct RawArmoryPiece(uint BaseItemId, int Quantity, bool IsHq, bool IsCollectable, int InventorySlot);

/// <summary>
/// One piece plus where it was read from. The slot name and the equipped flag are both *location*
/// facts rather than item facts, which is why they live here and not on <see cref="RawArmoryPiece"/>.
///
/// A flat list replaced an earlier <c>Dictionary&lt;slot, pieces&gt;</c>: because the export also
/// covers the gear currently worn, one canonical slot name now has two possible sources — the
/// "main" tab of the Armoury Chest *and* the equipped main hand — so the slot is no longer a
/// unique key.
/// </summary>
public readonly record struct SlottedArmoryPiece(string Slot, bool IsEquipped, RawArmoryPiece Piece);

/// <param name="Position">
/// Raw slot index within this piece's container (<c>GameInventoryItem.InventorySlot</c>). The game
/// draws each Armoury Chest tab in ascending index order, so a consumer can reproduce the layout
/// the player actually sees instead of inventing its own. For an equipped piece this is its index
/// in the equipped container (see <see cref="ArmorySlot.ResolveEquipped"/>).
/// </param>
/// <param name="IsEquipped">
/// True when the piece was worn by the character at export time rather than sitting in the Armoury
/// Chest. Gear on the *active* job never appears in any Armoury tab, so an export without this pass
/// is missing the one piece per slot that is most likely to be the best one owned.
/// </param>
public sealed record ArmoryPieceExport(
    [property: JsonPropertyName("slot")] string Slot,
    [property: JsonPropertyName("item_id")] uint ItemId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("is_hq")] bool IsHq,
    [property: JsonPropertyName("is_collectable")] bool IsCollectable,
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("is_equipped")] bool IsEquipped);

/// <param name="FormatVersion">
/// Schema version of this document, so a consumer can reject a file it does not understand instead
/// of silently reading missing fields as defaults. Bump it whenever a field changes meaning or
/// disappears; adding an optional field does not require a bump.
/// </param>
public sealed record ArmoryExportFile(
    [property: JsonPropertyName("format_version")] int FormatVersion,
    [property: JsonPropertyName("character")] string Character,
    [property: JsonPropertyName("exported_at")] DateTimeOffset ExportedAt,
    [property: JsonPropertyName("pieces")] ArmoryPieceExport[] Pieces);

/// <summary>
/// Pure assembly of the export document from already-read inventory data — no Dalamud or game I/O
/// here. See <see cref="ArmorySlot"/> for the slot-name vocabulary this builds on.
/// </summary>
public static class ArmoryExportBuilder
{
    /// <summary>Current <see cref="ArmoryExportFile.FormatVersion"/>. Version 1 is the first
    /// release that carries equipped gear and English field names.</summary>
    public const int CurrentFormatVersion = 1;

    public static ArmoryExportFile Build(
        string characterName,
        DateTimeOffset exportedAt,
        IReadOnlyList<SlottedArmoryPiece> slottedPieces)
    {
        var pieces = slottedPieces
            .Select(sp => new ArmoryPieceExport(
                Slot: sp.Slot,
                ItemId: sp.Piece.BaseItemId,
                Quantity: sp.Piece.Quantity,
                IsHq: sp.Piece.IsHq,
                IsCollectable: sp.Piece.IsCollectable,
                Position: sp.Piece.InventorySlot,
                IsEquipped: sp.IsEquipped))
            .ToArray();

        return new ArmoryExportFile(CurrentFormatVersion, characterName, exportedAt, pieces);
    }
}
