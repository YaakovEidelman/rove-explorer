using Avalonia.Threading;
using Rove.Core.Services;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class MountsViewModelTests : HeadlessTest
{
    private static readonly MountEntry UnmountedStick = new(
        "STICK", MountKind.Removable, "/dev/sdb1", null, null, null,
        CanMount: true, CanUnmount: false, CanEject: true);

    private static readonly MountEntry MountedStick = UnmountedStick with
    {
        MountUri = "file:///run/media/me/STICK", LocalPath = "/run/media/me/STICK",
        CanMount = false, CanUnmount = true,
    };

    private static readonly MountEntry Server = new(
        "nas", MountKind.Network, null, null, "smb://nas/share/", "/run/user/1000/gvfs/smb-share:server=nas,share=share",
        CanMount: false, CanUnmount: true, CanEject: false);

    private sealed class Answers(string? reply, bool agree) : IMountPrompter
    {
        public List<string> Asked { get; } = [];

        public Task<string?> AskTextAsync(string message, string field, bool secret, string? suggested)
        {
            Asked.Add(field);
            return Task.FromResult(reply);
        }

        public Task<int?> ChooseAsync(string message, string[] choices) => Task.FromResult(agree ? 0 : (int?)null);
    }

    private sealed class Setup
    {
        public CommandRegistry Registry { get; } = new();
        public FakeMountService Service { get; } = new();
        public DrivePlaces Places { get; } = new();
        public List<string> Visited { get; } = [];
        public string Current { get; set; } = "/home/me";
        public MountsViewModel Model { get; }

        public string? Picker { get; private set; }

        public Setup(string? reply = null, bool agree = false)
        {
            Model = new(Registry, Service, new Answers(reply, agree), path =>
            {
                Visited.Add(path);
                Service.Calls.Add("go " + path);
                Current = path;
                return Task.CompletedTask;
            }, () => Current, new DriveNumbers(null), Places);
            Model.PickRequested += (prefix, _) => Picker = prefix;
        }

        public string[] Titles() =>
            [.. Registry.CommandIdsStartingWith(CommandDef.MountIdPrefix)
                .Select(id => Registry.TryGetCommand(id, out Command c) ? c.Def.Title : "")
                .Order()];

        public void Run(string titleStart, string prefix = CommandDef.MountIdPrefix)
        {
            string id = Registry.CommandIdsStartingWith(prefix)
                .Single(i => Registry.TryGetCommand(i, out Command c) && c.Def.Title.StartsWith(titleStart));
            Registry.TryExecute(id);
            Pump();
        }
    }

    private static void Pump()
    {
        for (int i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
    }

    [Fact]
    public Task EachDriveGetsTheCommandsItsStateAllows() => OnUiThread(() =>
    {
        Setup setup = new();

        setup.Model.Show([UnmountedStick, Server]);

        Assert.Equal(
            ["Disconnect Server nas", "Format USB Drive STICK…", "Go to Server nas", "Open USB Drive STICK"],
            setup.Titles());
    });

    [Fact]
    public Task AMountedUsbDriveCanBeOpenedAndEjected() => OnUiThread(() =>
    {
        Setup setup = new();

        setup.Model.Show([MountedStick]);

        Assert.Equal(
            ["Eject USB Drive STICK", "Format USB Drive STICK…", "Go to USB Drive STICK", "Unmount USB Drive STICK"],
            setup.Titles());
    });

    [Fact]
    public Task ADriveWithAVolumeIdGetsANumberThatStaysAcrossReplugs() => OnUiThread(() =>
    {
        Setup setup = new();
        MountEntry other = UnmountedStick with { Name = "OTHER", Device = "/dev/sdc1", VolumeId = "BBBB" };
        MountEntry stick = UnmountedStick with { VolumeId = "AAAA" };

        setup.Model.Show([stick]);
        setup.Model.Show([]);
        setup.Model.Show([other, stick]);

        Assert.Equal(["Open USB 1: STICK", "Open USB 2: OTHER"], setup.Titles().Where(t => t.StartsWith("Open")));
    });

    [Fact]
    public Task AMountedDriveNamesItsFolderInThePathBar() => OnUiThread(() =>
    {
        Setup setup = new();

        setup.Model.Show([MountedStick with { VolumeId = "AAAA" }, Server]);

        Assert.Equal(
            [("/run/media/me/STICK", "USB 1: STICK"), (Server.LocalPath!, "Server nas")],
            setup.Places.All);
    });

    [Fact]
    public Task CommandsForDrivesThatAreGoneAreDropped() => OnUiThread(() =>
    {
        Setup setup = new();
        setup.Model.Show([UnmountedStick, Server]);

        setup.Model.Show([Server]);

        Assert.Equal(["Disconnect Server nas", "Go to Server nas"], setup.Titles());
    });

    [Fact]
    public Task OpeningAUsbDriveMountsItAndGoesThere() => OnUiThread(() =>
    {
        Setup setup = new();
        setup.Model.Show([UnmountedStick]);

        setup.Run("Open USB Drive");

        Assert.Equal(["mount STICK", "go /run/media/me/STICK"], setup.Service.Calls);
    });

    [Fact]
    public Task EjectingTheDriveYouAreInLeavesItFirst() => OnUiThread(() =>
    {
        Setup setup = new() { Current = "/run/media/me/STICK/photos" };
        setup.Model.Show([MountedStick]);

        setup.Run("Eject");

        Assert.Equal(2, setup.Service.Calls.Count);
        Assert.StartsWith("go ", setup.Service.Calls[0]);
        Assert.Equal("eject STICK", setup.Service.Calls[1]);
    });

    [Fact]
    public Task EjectingAnotherDriveStaysPut() => OnUiThread(() =>
    {
        Setup setup = new() { Current = "/run/media/me/STICKER" };
        setup.Model.Show([MountedStick]);

        setup.Run("Eject");

        Assert.Equal(["eject STICK"], setup.Service.Calls);
    });

    [Fact]
    public Task FormattingPicksAFileSystemAsksForANameAndConfirms() => OnUiThread(() =>
    {
        Setup setup = new(reply: "BACKUP", agree: true);
        setup.Model.Show([MountedStick with { VolumeId = "OLD-ID" }]);

        setup.Run("Format");
        Assert.Equal(CommandDef.FormatAsIdPrefix, setup.Picker);
        setup.Run("exFAT", CommandDef.FormatAsIdPrefix);

        Assert.Equal(["format STICK exfat BACKUP"], setup.Service.Calls);
        setup.Model.Show([MountedStick with { VolumeId = "NEW-ID" }]);
        Assert.Contains("Go to USB 1: STICK", setup.Titles());
    });

    [Fact]
    public Task SayingNoToTheWarningFormatsNothing() => OnUiThread(() =>
    {
        Setup setup = new(reply: "BACKUP", agree: false);
        setup.Model.Show([UnmountedStick]);

        setup.Run("Format");
        setup.Run("FAT32", CommandDef.FormatAsIdPrefix);

        Assert.Empty(setup.Service.Calls);
    });

    [Fact]
    public Task AFormatNameIsCutToWhatTheFileSystemAllows() => OnUiThread(() =>
    {
        Setup setup = new(reply: "A VERY LONG NAME", agree: true);
        setup.Model.Show([UnmountedStick]);

        setup.Run("Format");
        setup.Run("FAT32", CommandDef.FormatAsIdPrefix);

        Assert.Equal(["format STICK vfat A VERY LONG"], setup.Service.Calls);
    });

    [Fact]
    public Task ConnectToServerAsksForTheAddress() => OnUiThread(() =>
    {
        Setup setup = new(reply: "sftp://me@host/srv");

        setup.Registry.TryExecute(CommandDef.ConnectToServer.Id);
        Pump();

        Assert.Equal(["connect sftp://me@host/srv", "go /run/media/me/STICK"], setup.Service.Calls);
    });

    [Fact]
    public Task SomethingThatIsNotAnAddressIsRefused() => OnUiThread(() =>
    {
        Setup setup = new(reply: "/home/me");
        string? error = null;
        setup.Model.ErrorRaised += message => error = message;

        setup.Registry.TryExecute(CommandDef.ConnectToServer.Id);
        Pump();

        Assert.Empty(setup.Service.Calls);
        Assert.NotNull(error);
    });
}
