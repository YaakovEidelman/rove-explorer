using Rove.UI.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Rove.UI.Services;

public static class ColumnDefaults
{
    public static ObservableCollection<FolderViewColumn> Create()
    {
        return
        [
            new() { Name = "Name", IsVisible = true, Width = 340, DefaultWidth = 340 },
            new() { Name = "Type", IsVisible = true, Width = 100, DefaultWidth = 100 },
            new() { Name = "Size", IsVisible = true, Width = 90, DefaultWidth = 90 },
            new() { Name = "Modified", IsVisible = true, Width = 160, DefaultWidth = 160 },
        ];
    }

    private static readonly string[] _sizeSuffixes = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

    public static string FormatSize(long? sizeBytes)
    {
        if (sizeBytes is null) return "";
        if (sizeBytes == 0) return "0 B";

        int place = Convert.ToInt32(Math.Floor(Math.Log(Math.Abs(sizeBytes.Value), 1024)));
        place = Math.Min(place, _sizeSuffixes.Length - 1);
        double num = Math.Round(sizeBytes.Value / Math.Pow(1024, place), 1);
        return $"{num.ToString("0.#", CultureInfo.InvariantCulture)} {_sizeSuffixes[place]}";
    }
}
