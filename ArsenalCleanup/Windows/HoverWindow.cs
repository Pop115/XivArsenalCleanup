using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using ArsenalCleanup.GameData;
using ArsenalCleanup.Triage;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace ArsenalCleanup.Windows;

/// <summary>
/// A verdict that follows the cursor: hover a piece anywhere the game shows one — the Armoury
/// Chest, your bags, the gear you are wearing — and this says whether it is still worth keeping.
///
/// This is the answer to "how do I know what to recycle?" without reading a list. The player is
/// already looking at the piece; the plugin should meet them there rather than make them find the
/// same name in a window somewhere else.
///
/// Excluded from coverage: ImGui draw calls needing a live rendering context. What to say is
/// <see cref="Presentation"/>'s job, and which piece is under the cursor is
/// <see cref="HoveredItemId"/>'s — both pure and tested.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class HoverWindow : Window, IDisposable
{
    /// <summary>Enough of a gap that the panel never sits under the cursor itself.</summary>
    private static readonly Vector2 CursorOffset = new(24f, 24f);

    private readonly ArsenalService arsenal;
    private readonly Configuration configuration;
    private readonly IGameGui gameGui;

    public HoverWindow(ArsenalService arsenal, Configuration configuration, IGameGui gameGui)
        : base("##ArsenalCleanupHover",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoSavedSettings)
    {
        this.arsenal = arsenal;
        this.configuration = configuration;
        this.gameGui = gameGui;

        // Nothing about this window belongs in the plugin window list: it is a hint, not a tool.
        RespectCloseHotkey = false;
        ShowCloseButton = false;
        IsOpen = true;
    }

    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>
    /// Only draw when the player is hovering something the triage has an opinion about, and when
    /// the game's own interface is visible — a panel floating over a hidden UI, during a cutscene
    /// or a screenshot, would be the plugin talking over the game.
    /// </summary>
    public override bool DrawConditions()
    {
        if (!configuration.ShowHoverPanel || gameGui.GameUiHidden) return false;
        return arsenal.Current.PiecesOf(HoveredItemId.BaseItemId(gameGui.HoveredItem)).Count > 0;
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowPos(ImGui.GetIO().MousePos + CursorOffset, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.92f);
    }

    public override void Draw()
    {
        var pieces = arsenal.Current.PiecesOf(HoveredItemId.BaseItemId(gameGui.HoveredItem));
        if (pieces.Count == 0) return;

        ImGui.TextDisabled("Arsenal Cleanup");
        ImGui.Separator();

        // Ordered so the copy the player can act on without unequipping comes first.
        foreach (var piece in pieces.OrderBy(p => p.Piece.IsEquipped))
        {
            if (pieces.Count > 1)
            {
                ImGui.TextDisabled(piece.Piece.IsEquipped ? "the one you are wearing" : "the one in your chest");
            }

            ImGui.TextColored(Presentation.VerdictColour(piece.Verdict), Presentation.VerdictLabel(piece.Verdict));

            var disposal = Presentation.DisposalLabel(piece.Disposal);
            if (disposal.Length > 0)
            {
                ImGui.SameLine();
                ImGui.Text($"— {disposal}");
            }

            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);
            ImGui.TextDisabled(piece.Reason);
            ImGui.PopTextWrapPos();

            if (piece.Piece.IsEquipped && piece.Disposal != DisposalMethod.None)
            {
                ImGui.TextDisabled("Take it off first.");
            }
        }
    }
}
