using Rove.Core.Services;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class DrivePickerTests
{
    private sealed class NoAnswers : IMountPrompter
    {
        public Task<string?> AskTextAsync(string message, string field, bool secret, string? suggested) =>
            Task.FromResult<string?>(null);

        public Task<int?> ChooseAsync(string message, string[] choices) => Task.FromResult<int?>(null);
    }

    private static (PaletteViewModel Palette, MountsViewModel Mounts, FakeMountService Service) Setup()
    {
        CommandRegistry registry = new();
        registry.Register(
            new CommandDef(CommandDef.DriveIdPrefix + "/", "Go to Drive /", CommandKind.User, CommandDef.DriveOrder,
                CommandCategory.Navigation, ShortTitle: "Drive /"),
            () => { });
        FakeMountService service = new();
        MountsViewModel mounts = new(registry, service, new NoAnswers(), _ => Task.CompletedTask, () => "/home/me");
        PaletteViewModel palette = new(registry);
        mounts.PickRequested += palette.OpenScoped;
        return (palette, mounts, service);
    }

    private static string[] Rows(PaletteViewModel palette) =>
        [.. palette.Items.Select(row => string.Concat(row.Entry!.TitleSegments.Select(segment => segment.Text)))];

    private static void Pick(PaletteViewModel palette, string row)
    {
        palette.SelectedIndex = Array.IndexOf(Rows(palette), row);
        palette.ExecuteOption();
    }

    private static readonly MountEntry Stick = new("STICK", MountKind.Removable, "/dev/sdb1", null,
        "file:///run/media/me/STICK", "/run/media/me/STICK", CanMount: false, CanUnmount: true, CanEject: true);

    [Fact]
    public void TheDrivePickerListsOnlyTheDrives()
    {
        (PaletteViewModel palette, MountsViewModel mounts, FakeMountService service) = Setup();
        service.Missing.Add(MountTool.Gio);
        mounts.Show([
            new("nas", MountKind.Network, null, null, "smb://nas/", "/run/user/1000/gvfs/nas",
                CanMount: false, CanUnmount: true, CanEject: false),
            Stick,
            new("BACKUP", MountKind.Removable, "/dev/sdc1", null, null, null,
                CanMount: true, CanUnmount: false, CanEject: true),
        ]);

        palette.OpenScoped(MountsViewModel.DrivePicker);

        Assert.Equal(
            ["Drive /", "USB Drive BACKUP", "USB Drive STICK", "Phones and servers: install gio"],
            Rows(palette));
    }

    [Fact]
    public void PickingADriveShowsWhatYouCanDoWithIt()
    {
        (PaletteViewModel palette, MountsViewModel mounts, _) = Setup();
        mounts.Show([Stick]);
        palette.OpenScoped(MountsViewModel.DrivePicker);

        Pick(palette, "USB Drive STICK");

        Assert.True(palette.IsPaletteOpen);
        Assert.Equal(["Go to", "Unmount", "Eject", "Format…"], Rows(palette));
    }

    [Fact]
    public void EscapeFromADrivesActionsGoesBackToTheDrives()
    {
        (PaletteViewModel palette, MountsViewModel mounts, _) = Setup();
        mounts.Show([Stick]);
        palette.OpenScoped(MountsViewModel.DrivePicker);
        Pick(palette, "USB Drive STICK");

        palette.Back();

        Assert.Equal(["Drive /", "USB Drive STICK"], Rows(palette));

        palette.Back();

        Assert.False(palette.IsPaletteOpen);
    }

    [Fact]
    public void TypingFiltersTheDrivesByName()
    {
        (PaletteViewModel palette, MountsViewModel mounts, _) = Setup();
        mounts.Show([Stick]);
        palette.OpenScoped(MountsViewModel.DrivePicker);

        palette.PaletteSearchText = "stick";

        Assert.Equal(["USB Drive STICK"], Rows(palette));
    }
}
