using System;
using System.IO;
using System.Text.Json;
using ArsenalCleanup;
using Xunit;

namespace ArsenalCleanup.Tests;

public sealed class ExportWriterTests
{
    private static readonly DateTimeOffset ExportedAt = new(2026, 9, 6, 18, 30, 0, TimeSpan.FromHours(2));

    private static ArmoryExportFile Export(string character = "Hero Example") =>
        ArmoryExportBuilder.Build(character, ExportedAt, [
            new SlottedArmoryPiece("main", IsEquipped: true, new RawArmoryPiece(1688, 1, false, false, 0)),
        ]);

    [Fact]
    public void BuildFileName_UsesTheExportDate_AndAHyphenatedCharacterName()
    {
        Assert.Equal("2026-09-06-Hero-Example-arsenal.json", ExportWriter.BuildFileName(ExportedAt, "Hero Example"));
    }

    [Fact]
    public void BuildFileName_IsStableForTheSameCharacterAndDay_SoARe_ExportReplacesIt()
    {
        var morning = new DateTimeOffset(2026, 9, 6, 8, 0, 0, TimeSpan.Zero);
        var evening = new DateTimeOffset(2026, 9, 6, 23, 0, 0, TimeSpan.Zero);

        Assert.Equal(ExportWriter.BuildFileName(morning, "Alpha"), ExportWriter.BuildFileName(evening, "Alpha"));
    }

    [Theory]
    [InlineData("A/B", "A_B")]
    [InlineData("A:B", "A_B")]
    [InlineData("../evil", ".._evil")]
    [InlineData("Y'shtola Rhul", "Y'shtola-Rhul")]
    public void SanitizeForFileName_ReplacesAnythingAPathCannotHold(string raw, string expected)
    {
        Assert.Equal(expected, ExportWriter.SanitizeForFileName(raw));
    }

    [Fact]
    public void SanitizeForFileName_LeavesAnOrdinaryNameAlone()
    {
        Assert.Equal("Hero", ExportWriter.SanitizeForFileName("Hero"));
    }

    [Fact]
    public void Write_CreatesTheDirectory_AndReturnsThePathItWrote()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"arsenal-export-test-{Guid.NewGuid():N}", "nested");
        try
        {
            var path = ExportWriter.Write(directory, Export());

            Assert.True(File.Exists(path));
            Assert.Equal(Path.Combine(directory, "2026-09-06-Hero-Example-arsenal.json"), path);
        }
        finally
        {
            Directory.Delete(Directory.GetParent(directory)!.FullName, recursive: true);
        }
    }

    [Fact]
    public void Write_ProducesTheDocumentedJsonShape()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"arsenal-export-test-{Guid.NewGuid():N}");
        try
        {
            var json = JsonDocument.Parse(File.ReadAllText(ExportWriter.Write(directory, Export()))).RootElement;

            Assert.Equal(ArmoryExportBuilder.CurrentFormatVersion, json.GetProperty("format_version").GetInt32());
            Assert.Equal("Hero Example", json.GetProperty("character").GetString());
            Assert.NotEqual(default, json.GetProperty("exported_at").GetDateTimeOffset());

            var piece = Assert.Single(json.GetProperty("pieces").EnumerateArray());
            Assert.Equal("main", piece.GetProperty("slot").GetString());
            Assert.Equal(1688u, piece.GetProperty("item_id").GetUInt32());
            Assert.Equal(1, piece.GetProperty("quantity").GetInt32());
            Assert.False(piece.GetProperty("is_hq").GetBoolean());
            Assert.False(piece.GetProperty("is_collectable").GetBoolean());
            Assert.Equal(0, piece.GetProperty("position").GetInt32());
            Assert.True(piece.GetProperty("is_equipped").GetBoolean());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_ReExportingTheSameDay_ReplacesThatDaysFileRatherThanAddingOne()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"arsenal-export-test-{Guid.NewGuid():N}");
        try
        {
            ExportWriter.Write(directory, Export());
            ExportWriter.Write(directory, Export());

            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_KeepsOneFilePerCharacter()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"arsenal-export-test-{Guid.NewGuid():N}");
        try
        {
            ExportWriter.Write(directory, Export("Hero Example"));
            ExportWriter.Write(directory, Export("Other Character"));

            Assert.Equal(2, Directory.GetFiles(directory).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
