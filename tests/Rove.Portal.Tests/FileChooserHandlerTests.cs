using System.Text;
using Tmds.DBus.Protocol;
using Xunit;

namespace Rove.Portal.Tests;

public class DecodePathTests
{
    [Fact]
    public void ANullTerminatedByteArrayDecodesToItsPath()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("/home/user/Documents\0");

        Assert.Equal("/home/user/Documents", FileChooserHandler.DecodePath(bytes));
    }

    [Fact]
    public void ABareByteArrayWithNoTrailingNullStillDecodes()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("/home/user/Documents");

        Assert.Equal("/home/user/Documents", FileChooserHandler.DecodePath(bytes));
    }
}

public class ToFileUriTests
{
    [Fact]
    public void APathBecomesAFileUri()
    {
        Assert.Equal("file:///home/user/report.pdf", FileChooserHandler.ToFileUri("/home/user/report.pdf"));
    }

    [Fact]
    public void SpacesAreEscaped()
    {
        Assert.Equal("file:///home/user/My%20Report.pdf", FileChooserHandler.ToFileUri("/home/user/My Report.pdf"));
    }
}

public class ReadFolderOptionTests
{
    [Fact]
    public void WithNoCurrentFolderThereIsNothingToRead()
    {
        Assert.Null(FileChooserHandler.ReadFolderOption([]));
    }

    [Fact]
    public void ACurrentFolderOptionDecodesToItsPath()
    {
        Dictionary<string, VariantValue> options = new()
        {
            ["current_folder"] = VariantValue.Array(Encoding.UTF8.GetBytes("/home/user/Pictures\0")),
        };

        Assert.Equal("/home/user/Pictures", FileChooserHandler.ReadFolderOption(options));
    }
}

public class ResolveFiltersTests
{
    private static VariantValue Glob(string pattern) =>
        VariantValue.Struct(VariantValue.UInt32(0), VariantValue.String(pattern));

    private static VariantValue Group(string name, params VariantValue[] patterns) =>
        VariantValue.Struct(VariantValue.String(name), VariantValue.ArrayOfVariant(patterns));

    private static Dictionary<string, VariantValue> Options(
        VariantValue[] groups, string? currentFilterName = null)
    {
        Dictionary<string, VariantValue> options = new()
        {
            ["filters"] = VariantValue.ArrayOfVariant(groups),
        };
        if (currentFilterName is not null)
            options["current_filter"] = VariantValue.Struct(VariantValue.String(currentFilterName));
        return options;
    }

    [Fact]
    public void NoFiltersOptionMeansNoFilters()
    {
        var (filters, selected) = FileChooserHandler.ResolveFilters([]);

        Assert.Empty(filters);
        Assert.Equal(0, selected);
    }

    [Fact]
    public void GlobPatternsPassThroughAsIs()
    {
        VariantValue[] groups = [Group("Text files", Glob("*.txt"), Glob("*.md"))];

        var (filters, selected) = FileChooserHandler.ResolveFilters(Options(groups));

        Assert.Equal("Text files", filters[0].Name);
        Assert.Equal(["*.txt", "*.md"], filters[0].Patterns);
        Assert.Equal(0, selected);
    }

    [Fact]
    public void WithNoCurrentFilterTheFirstGroupIsSelected()
    {
        VariantValue[] groups = [Group("All files", Glob("*")), Group("Text files", Glob("*.txt"))];

        var (_, selected) = FileChooserHandler.ResolveFilters(Options(groups));

        Assert.Equal(0, selected);
    }

    [Fact]
    public void TheCurrentFilterOptionPicksItsGroupByName()
    {
        VariantValue[] groups = [Group("All files", Glob("*")), Group("Text files", Glob("*.txt"))];

        var (_, selected) = FileChooserHandler.ResolveFilters(Options(groups, currentFilterName: "Text files"));

        Assert.Equal(1, selected);
    }

    [Fact]
    public void ACurrentFilterNameThatMatchesNothingFallsBackToTheFirst()
    {
        VariantValue[] groups = [Group("All files", Glob("*")), Group("Text files", Glob("*.txt"))];

        var (_, selected) = FileChooserHandler.ResolveFilters(Options(groups, currentFilterName: "Nothing like this"));

        Assert.Equal(0, selected);
    }
}
