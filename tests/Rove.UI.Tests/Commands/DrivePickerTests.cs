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
                CommandCategory.Navigation, Group: "Drives", ShortTitle: "/"),
            () => { });
        FakeMountService service = new();
        MountsViewModel mounts = new(registry, service, new NoAnswers(), _ => Task.CompletedTask, () => "/home/me");
        return (new PaletteViewModel(registry), mounts, service);
    }

    private static string[] Rows(PaletteViewModel palette) =>
        [.. palette.Items.Select(row => row.IsHeader
            ? $"# {row.Header}"
            : string.Concat(row.Entry!.TitleSegments.Select(segment => segment.Text)))];

    [Fact]
    public void EachDriveGetsAHeadingWithItsActionsUnderIt()
    {
        (PaletteViewModel palette, MountsViewModel mounts, FakeMountService service) = Setup();
        service.Missing.Add(MountTool.Gio);
        mounts.Show([
            new("nas", MountKind.Network, null, null, "smb://nas/", "/run/user/1000/gvfs/nas",
                CanMount: false, CanUnmount: true, CanEject: false),
            new("STICK", MountKind.Removable, "/dev/sdb1", null, "file:///run/media/me/STICK", "/run/media/me/STICK",
                CanMount: false, CanUnmount: true, CanEject: true),
            new("BACKUP", MountKind.Removable, "/dev/sdc1", null, null, null,
                CanMount: true, CanUnmount: false, CanEject: true),
        ]);

        palette.OpenScoped(CommandDef.DriveIdPrefix, "pick a drive…");

        Assert.Equal(
        [
            "# Drives", "/",
            "# USB Drive BACKUP", "Open", "Format…",
            "# USB Drive STICK", "Go to", "Unmount", "Eject", "Format…",
            "# Server nas", "Go to", "Disconnect",
            "# Set up", "Phones and servers: install gio",
        ], Rows(palette));
    }

    [Fact]
    public void TypingSwitchesToAFlatListWithFullNames()
    {
        (PaletteViewModel palette, MountsViewModel mounts, _) = Setup();
        mounts.Show([
            new("STICK", MountKind.Removable, "/dev/sdb1", null, "file:///run/media/me/STICK", "/run/media/me/STICK",
                CanMount: false, CanUnmount: true, CanEject: true),
        ]);
        palette.OpenScoped(CommandDef.DriveIdPrefix, "pick a drive…");

        palette.PaletteSearchText = "unmount";

        Assert.Equal(["Unmount USB Drive STICK"], Rows(palette));
    }
}
