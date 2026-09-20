using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class FileOpenerTests
{
    /// <summary>A PATH, written the way this machine writes one.</summary>
    private static string Path_(params string[] directories) =>
        string.Join(System.IO.Path.PathSeparator, directories);

    private static string In(string directory, string program) =>
        System.IO.Path.Combine(directory, program);

    private static Func<string, bool> Present(params string[] programs) =>
        candidate => programs.Contains(candidate, StringComparer.Ordinal);

    private const string UsrBin = "/usr/bin";
    private const string LocalBin = "/usr/local/bin";
    private const string HomeBin = "/home/me/bin";

    [Fact]
    public void AProgramIsFoundInTheFirstFolderThatHasIt()
    {
        string? found = FileOpener.FindProgram(
            Path_(LocalBin, UsrBin),
            "gio",
            Present(In(UsrBin, "gio"), In(LocalBin, "gio")));

        Assert.Equal(In(LocalBin, "gio"), found);
    }

    [Fact]
    public void AProgramThatIsNowhereOnThePathIsNotFound()
    {
        Assert.Null(FileOpener.FindProgram(Path_(UsrBin), "gio", Present(In(UsrBin, "xdg-open"))));
    }

    [Fact]
    public void TheTerminalAwareOpenerIsPreferred()
    {
        (string Path, string? First)? found = FileOpener.FindOpener(
            Path_(LocalBin, UsrBin),
            Present(In(UsrBin, "gio"), In(UsrBin, "xdg-open")));

        Assert.NotNull(found);
        Assert.Equal(In(UsrBin, "gio"), found.Value.Path);
        Assert.Equal("open", found.Value.First);
    }

    [Fact]
    public void TheCrossDesktopOpenerIsUsedWhenGioIsMissing()
    {
        (string Path, string? First)? found = FileOpener.FindOpener(
            Path_(UsrBin),
            Present(In(UsrBin, "xdg-open")));

        Assert.NotNull(found);
        Assert.Equal(In(UsrBin, "xdg-open"), found.Value.Path);
        Assert.Null(found.Value.First);
    }

    [Fact]
    public void EarlierDirectoriesOnThePathWin()
    {
        (string Path, string? First)? found = FileOpener.FindOpener(
            Path_(HomeBin, UsrBin),
            Present(In(HomeBin, "xdg-open"), In(UsrBin, "xdg-open")));

        Assert.Equal(In(HomeBin, "xdg-open"), found!.Value.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoPathMeansNoOpener(string? pathVariable)
    {
        Assert.Null(FileOpener.FindOpener(pathVariable, Present(In(UsrBin, "xdg-open"))));
    }

    [Fact]
    public void AnEmptyPathEntryIsSkippedRatherThanSearched()
    {
        (string Path, string? First)? found = FileOpener.FindOpener(
            Path_("", "", UsrBin, ""),
            Present(In(UsrBin, "xdg-open")));

        Assert.Equal(In(UsrBin, "xdg-open"), found!.Value.Path);
    }

    [Fact]
    public void NothingInstalledMeansNoOpener()
    {
        Assert.Null(FileOpener.FindOpener(Path_(UsrBin, "/bin"), Present(In(UsrBin, "vim"))));
    }

    /// <summary>
    /// Only Unix has an execute bit, so only Unix can have a file that is
    /// there and still cannot be run. Windows has no such state, and asking
    /// it about one throws rather than answering.
    /// </summary>
    [Fact]
    public void APresentButUnrunnableNameIsNotAnOpener()
    {
        if (OperatingSystem.IsWindows())
            return;

        string directory = System.IO.Directory.CreateTempSubdirectory("rove-openers-").FullName;
        try
        {
            string unrunnable = In(directory, "gio");
            System.IO.File.WriteAllText(unrunnable, string.Empty);
            System.IO.File.SetUnixFileMode(unrunnable, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            Assert.False(FileOpener.IsRunnable(unrunnable));

            System.IO.File.SetUnixFileMode(
                unrunnable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            Assert.True(FileOpener.IsRunnable(unrunnable));
        }
        finally
        {
            System.IO.Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The other half of the same rule: on Windows, being there is the whole test.</summary>
    [Fact]
    public void OnWindowsAnOpenerThatIsThereCanBeRun()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string directory = System.IO.Directory.CreateTempSubdirectory("rove-openers-").FullName;
        try
        {
            string present = In(directory, "opener.exe");
            System.IO.File.WriteAllText(present, string.Empty);

            Assert.True(FileOpener.IsRunnable(present));
            Assert.False(FileOpener.IsRunnable(In(directory, "missing.exe")));
        }
        finally
        {
            System.IO.Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AnOpenerWithNothingToSayGetsTheCodeItExitedWith()
    {
        Assert.Equal(
            "Nothing on this system is set up to open notes.txt.",
            FileOpener.Refusal(3, "", "notes.txt"));
        Assert.Equal(
            "The program that opens notes.txt would not start.",
            FileOpener.Refusal(4, "", "notes.txt"));
        Assert.Equal("Could not open notes.txt.", FileOpener.Refusal(1, "", "notes.txt"));
    }

    [Fact]
    public void WhatTheOpenerSaidIsPreferredToAGuessAtWhatItMeant()
    {
        Assert.Equal(
            "Could not open notes.txt: no method available for opening",
            FileOpener.Refusal(3, "no method available for opening\nsecond line", "notes.txt"));
    }

    [Fact]
    public void TheOpenerNamingItselfAndTheFileIsNotRepeatedBackToTheUser()
    {
        Assert.Equal(
            "Could not open notes.txt: Failed to find default application for content type "
                + "\u2018application/octet-stream\u2019",
            FileOpener.Refusal(
                2,
                "gio: file:///home/me/notes.txt: Failed to find default application for content type "
                    + "\u2018application/octet-stream\u2019",
                "notes.txt"));
    }

    [Fact]
    public void AComplaintThatNamesNoFileIsLeftAsItWasSaid()
    {
        Assert.Equal("Permission denied", FileOpener.Spoken("Permission denied"));
    }

    [Fact]
    public void AnOpenerWithNothingToSayFallsBackToTheExitCode()
    {
        Assert.Equal(
            "Nothing on this system is set up to open notes.txt.",
            FileOpener.Refusal(3, "   \n  ", "notes.txt"));
    }
}
