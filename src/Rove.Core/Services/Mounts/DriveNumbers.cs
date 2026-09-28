using System.Globalization;

namespace Rove.Core.Services;

public sealed class DriveNumbers(string? path)
{
    private const int PreferredLimit = 9;

    private Dictionary<string, int>? _known;

    public MountEntry[] Assign(MountEntry[] entries)
    {
        Dictionary<string, int> known = _known ??= Read(path);
        MountEntry[] numbered = [.. entries];
        HashSet<int> taken = [];
        HashSet<string> placed = [];
        List<int> waiting = [];
        for (int i = 0; i < numbered.Length; i++)
        {
            if (numbered[i].VolumeId is not { } id)
                continue;
            if (!placed.Contains(id) && known.TryGetValue(id, out int number) && taken.Add(number))
            {
                placed.Add(id);
                numbered[i] = numbered[i] with { Number = number };
            }
            else
            {
                waiting.Add(i);
            }
        }

        bool changed = false;
        foreach (int i in waiting)
        {
            int number = Free(taken, known);
            taken.Add(number);
            if (placed.Add(numbered[i].VolumeId!))
            {
                known[numbered[i].VolumeId!] = number;
                changed = true;
            }
            numbered[i] = numbered[i] with { Number = number };
        }
        if (changed)
            Save(known);
        return numbered;
    }

    public void Move(string from, string to)
    {
        Dictionary<string, int> known = _known ??= Read(path);
        if (from == to || !known.Remove(from, out int number))
            return;
        known[to] = number;
        Save(known);
    }

    private static int Free(HashSet<int> taken, Dictionary<string, int> known)
    {
        HashSet<int> remembered = [.. known.Values];
        for (int number = 1; number <= PreferredLimit; number++)
        {
            if (!taken.Contains(number) && !remembered.Contains(number))
                return number;
        }
        int free = 1;
        while (taken.Contains(free))
            free++;
        return free;
    }

    private static Dictionary<string, int> Read(string? path)
    {
        Dictionary<string, int> known = [];
        if (path is null)
            return known;
        try
        {
            foreach (string line in File.ReadLines(path))
            {
                int space = line.IndexOf(' ');
                if (space > 0
                    && int.TryParse(line.AsSpan(0, space), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                    && number > 0)
                    known[line[(space + 1)..].Trim()] = number;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return known;
    }

    private void Save(Dictionary<string, int> known)
    {
        if (path is null)
            return;
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllLines(path, known
                .OrderBy(pair => pair.Value)
                .Select(pair => $"{pair.Value.ToString(CultureInfo.InvariantCulture)} {pair.Key}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
