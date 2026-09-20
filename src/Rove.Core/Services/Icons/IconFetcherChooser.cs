namespace Rove.Core.Services;

public static class IconFetcherChooser
{
    public static IIconFetcher CreateForHost()
    {
        IIconFetcher fetcher;
        if (OperatingSystem.IsWindows())
            fetcher = new WindowsIconFetcher();
        else if (OperatingSystem.IsLinux())
            fetcher = new LinuxIconFetcher();
        else
            fetcher = new NoIconFetcher();
        return new CacheIconFetcher(fetcher);
    }
}
