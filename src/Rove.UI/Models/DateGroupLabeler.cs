namespace Rove.UI.Models;

public static class DateGroupLabeler
{
    public static string LabelFor(DateTime modified, DateTime now)
    {
        DateTime today = now.Date;
        DateTime date = modified.Date;
        int daysAgo = (today - date).Days;

        if (daysAgo <= 0)
            return "Today";
        if (daysAgo == 1)
            return "Yesterday";

        DateTime startOfWeek = StartOfWeek(today);
        if (date >= startOfWeek)
            return "Earlier This Week";
        if (date >= startOfWeek.AddDays(-7))
            return "Last Week";

        DateTime startOfMonth = new(today.Year, today.Month, 1);
        if (date >= startOfMonth)
            return "Earlier This Month";
        if (date >= startOfMonth.AddMonths(-1))
            return "Last Month";

        if (date.Year == today.Year)
            return "Earlier This Year";
        if (date.Year == today.Year - 1)
            return "Last Year";
        return date.Year.ToString();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }
}
