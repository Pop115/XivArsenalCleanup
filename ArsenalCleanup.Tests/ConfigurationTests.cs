using System.IO;
using ArsenalCleanup;
using Xunit;

namespace ArsenalCleanup.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void ResolveExportDirectory_UsesTheConfiguredFolder_WhenThePlayerSetOne()
    {
        Assert.Equal(@"D:\exports", Configuration.ResolveExportDirectory(@"D:\exports", @"C:\default"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ResolveExportDirectory_FallsBackToTheDefault_WhenNothingUsableIsConfigured(string? configured)
    {
        // Whitespace is a slip rather than a choice, and Directory.CreateDirectory would throw on
        // it at the least helpful moment — mid-export, after the inventory was already read.
        Assert.Equal(@"C:\default", Configuration.ResolveExportDirectory(configured, @"C:\default"));
    }

    [Fact]
    public void DefaultExportDirectory_SitsUnderThePluginsOwnConfigFolder()
    {
        Assert.Equal(Path.Combine(@"C:\config", "exports"), Configuration.DefaultExportDirectory(@"C:\config"));
    }

    [Fact]
    public void ExportDirectory_DefaultsToEmpty_SoNoMachineSpecificPathIsBakedIn()
    {
        // A hardcoded absolute path is meaningless on someone else's machine; empty means "use the
        // default", which ResolveExportDirectory turns into a real folder at export time.
        Assert.Equal(string.Empty, new Configuration().ExportDirectory);
    }
}
