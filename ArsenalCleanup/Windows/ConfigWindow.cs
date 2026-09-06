using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ArsenalCleanup.Windows;

/// <summary>
/// Settings window. Excluded from coverage: it is ImGui draw calls, which need a live rendering
/// context; the one rule with a decision in it (what an empty folder setting means) lives in
/// <see cref="Configuration.ResolveExportDirectory"/> and is tested there.
/// </summary>
[ExcludeFromCodeCoverage]
public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly string defaultExportDirectory;
    private string exportDirectoryBuffer;

    public ConfigWindow(Plugin plugin, string defaultExportDirectory)
        : base("Arsenal Cleanup — Settings###ArsenalCleanupConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
        Size = new Vector2(560, 0);
        SizeCondition = ImGuiCond.Appearing;

        configuration = plugin.Configuration;
        this.defaultExportDirectory = defaultExportDirectory;
        exportDirectoryBuffer = configuration.ExportDirectory;
    }

    /// <summary>Nothing to release — the window holds no unmanaged handle and no subscription.</summary>
    public void Dispose() => GC.SuppressFinalize(this);

    public override void Draw()
    {
        ImGui.TextWrapped("Folder where /arsenalcleanup writes one JSON file per character, named " +
                          "after the export date. Re-exporting the same character on the same day " +
                          "replaces that day's file.");
        ImGui.Spacing();

        ImGui.InputText("Export folder", ref exportDirectoryBuffer, 260);

        // Showing where an empty setting resolves to beats making the player guess: without it,
        // "leave it blank" reads as "exports go nowhere".
        ImGui.TextDisabled($"Leave empty to use: {defaultExportDirectory}");
        ImGui.Spacing();

        if (ImGui.Button("Save"))
        {
            configuration.ExportDirectory = exportDirectoryBuffer;
            configuration.Save();
        }

        ImGui.Separator();

        var hover = configuration.ShowHoverPanel;
        if (ImGui.Checkbox("Show the verdict when hovering a piece", ref hover))
        {
            configuration.ShowHoverPanel = hover;
            configuration.Save();
        }
        ImGui.TextDisabled("Hover anything in your Armoury Chest, bags or gear to see what it is worth.");
    }
}
