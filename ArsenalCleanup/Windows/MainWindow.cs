using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using ArsenalCleanup.GameData;
using ArsenalCleanup.Triage;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace ArsenalCleanup.Windows;

/// <summary>
/// The plugin's own window: what the character owns, sorted into what is worth keeping and what is
/// not, without any external tool.
///
/// Laid out to answer one question first — "what can I clear out?" — so pieces worth keeping are
/// hidden until asked for, and the summary leads with how to part with things rather than with how
/// many there are.
///
/// Excluded from coverage: it is ImGui draw calls needing a live rendering context. Everything with
/// a decision in it lives in <see cref="Presentation"/> and <see cref="ArsenalTriage"/>, both tested.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MainWindow : Window, IDisposable
{
    /// <summary>Icons make a gear list scannable — a player recognises a piece by its icon long
    /// before they finish reading its name.</summary>
    private const float IconSize = 32f;

    private readonly ArsenalService arsenal;
    private readonly ITextureProvider textures;
    private readonly Configuration configuration;
    private readonly string defaultExportDirectory;
    private readonly Action openSettings;

    private bool showKeepers;
    private string? lastExportPath;
    private string? lastExportError;

    public MainWindow(
        ArsenalService arsenal,
        ITextureProvider textures,
        Configuration configuration,
        string defaultExportDirectory,
        Action openSettings)
        : base("Arsenal Cleanup###ArsenalCleanupMain")
    {
        this.arsenal = arsenal;
        this.textures = textures;
        this.configuration = configuration;
        this.defaultExportDirectory = defaultExportDirectory;
        this.openSettings = openSettings;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(720, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(960, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    /// <summary>Nothing to release — the window holds no unmanaged handle and no subscription.</summary>
    public void Dispose() => GC.SuppressFinalize(this);

    public override void Draw()
    {
        DrawToolbar();
        ImGui.Separator();

        if (arsenal.LastError is { } error)
        {
            ImGui.TextColored(Presentation.VerdictColour(Verdict.NeedsReview), error);
            ImGui.Separator();
        }

        var result = arsenal.Current;
        if (result.Pieces.Count == 0)
        {
            ImGui.TextWrapped(
                "Nothing read yet. Press Refresh while logged in — everything in your Armoury Chest " +
                "and everything you are wearing gets sorted into what is worth keeping and what is not.");
            return;
        }

        DrawSummary(result);
        ImGui.Separator();
        DrawSlotTabs(result);
    }

    private void DrawToolbar()
    {
        if (ImGui.Button("Refresh")) arsenal.Refresh();

        ImGui.SameLine();
        if (ImGui.Button("Export JSON")) Export();

        ImGui.SameLine();
        if (ImGui.Button("Settings")) openSettings();

        ImGui.SameLine();
        ImGui.Checkbox("Show pieces worth keeping", ref showKeepers);

        if (arsenal.LastRefreshed is { } refreshed)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"read at {refreshed:HH:mm}");
        }

        if (lastExportPath is not null) ImGui.TextDisabled($"Written to {lastExportPath}");
        if (lastExportError is not null)
        {
            ImGui.TextColored(Presentation.VerdictColour(Verdict.NeedsReview), lastExportError);
        }
    }

    private void Export()
    {
        lastExportPath = null;
        lastExportError = null;
        try
        {
            lastExportPath = arsenal.Export(
                Configuration.ResolveExportDirectory(configuration.ExportDirectory, defaultExportDirectory));
        }
        catch (Exception ex)
        {
            lastExportError = $"Could not write the export: {ex.Message}";
        }
    }

    /// <summary>
    /// Leads with how to part with things rather than with totals: "18 can go in the Armoire" is
    /// something to act on, where "396 pieces" is not.
    /// </summary>
    private static void DrawSummary(TriageResult result)
    {
        Stat("Armoire", result.Count(DisposalMethod.Armoire), "Stored forever at no cost — nothing is lost.");
        ImGui.SameLine();
        Stat("Desynthesise", result.Count(DisposalMethod.Desynthesis), "Destroyed, but returns materials.");
        ImGui.SameLine();
        Stat("Discard", result.Count(DisposalMethod.Discard), "Nothing comes back — the last resort.");
        ImGui.SameLine();
        Stat("Check yourself", result.Count(Verdict.NeedsReview), "Relics, and anything judged on incomplete information.");
        ImGui.SameLine();
        Stat("Keep", result.Pieces.Count - result.Actionable.Count(), "Still the best you own, or still needed later.");
        ImGui.SameLine();
        Stat("Soul crystals", result.SoulCrystals.Count, "Never triaged — one per job, and never worth parting with.");
    }

    private static void Stat(string label, int value, string tooltip)
    {
        ImGui.BeginGroup();
        ImGui.Text($"{value}");
        ImGui.TextDisabled(label);
        ImGui.EndGroup();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
    }

    private void DrawSlotTabs(TriageResult result)
    {
        var bySlot = result.Pieces
            .GroupBy(p => p.Facts.Slot)
            .ToDictionary(g => g.Key, g => g.ToList());

        using var tabs = ImRaii.TabBar("##slots");
        if (!tabs) return;

        foreach (var slot in Presentation.SlotOrder)
        {
            if (!bySlot.TryGetValue(slot, out var pieces)) continue;

            var visible = Visible(pieces).ToList();
            using var tab = ImRaii.TabItem($"{Presentation.SlotLabel(slot)} ({visible.Count})###tab-{slot}");
            if (!tab) continue;

            if (visible.Count == 0)
            {
                ImGui.TextDisabled("Nothing to deal with here. Tick \"Show pieces worth keeping\" to see the rest.");
                continue;
            }

            DrawPieceTable(slot, visible);
        }
    }

    private IEnumerable<TriagedPiece> Visible(IEnumerable<TriagedPiece> pieces) =>
        showKeepers
            ? pieces
            : pieces.Where(p => p.Verdict is Verdict.Obsolete or Verdict.NeedsReview);

    private void DrawPieceTable(string slot, List<TriagedPiece> pieces)
    {
        // Worn gear first, then the chest in the order the game itself draws it: the two blocks
        // index their positions in different containers, so interleaving them would be meaningless.
        var ordered = pieces
            .OrderByDescending(p => p.Piece.IsEquipped)
            .ThenBy(p => p.Piece.Position)
            .ToList();

        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;

        using var table = ImRaii.Table($"##pieces-{slot}", 4, flags);
        if (!table) return;

        ImGui.TableSetupColumn("Piece", ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn("Verdict", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("What to do", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("Why", ImGuiTableColumnFlags.WidthStretch, 5f);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        foreach (var piece in ordered)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            DrawIcon(piece.Facts.IconId);
            ImGui.SameLine();
            ImGui.Text(piece.Facts.Name);
            if (piece.Piece.IsEquipped)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(worn)");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("You are wearing this. Take it off before storing, desynthesising or discarding it.");
                }
            }
            ImGui.TextDisabled($"item level {piece.Facts.ItemLevel}, wearable from level {piece.Facts.EquipLevel}");

            ImGui.TableNextColumn();
            ImGui.TextColored(Presentation.VerdictColour(piece.Verdict), Presentation.VerdictLabel(piece.Verdict));

            ImGui.TableNextColumn();
            ImGui.Text(Presentation.DisposalLabel(piece.Disposal));

            ImGui.TableNextColumn();
            ImGui.TextWrapped(piece.Reason);
            if (piece.DecidingJobLevel is { } level && piece.DecidingJob is not null)
            {
                ImGui.TextDisabled($"judged on a job at level {level}");
            }
        }
    }

    /// <summary>
    /// Draws an item's icon, falling back to blank space of the same size when the texture is not
    /// ready yet — the game loads them lazily, and a row that jumps sideways on the second frame is
    /// worse than one that starts with a gap.
    /// </summary>
    private void DrawIcon(ushort iconId)
    {
        var size = new Vector2(IconSize, IconSize);
        if (iconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var shared)
            && shared.TryGetWrap(out var wrap, out _))
        {
            ImGui.Image(wrap.Handle, size);
        }
        else
        {
            ImGui.Dummy(size);
        }
    }
}
