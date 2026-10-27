using System.Globalization;
using System.Text;

namespace CakeOrders.Web.Helpers;

public static class PersianDateTime
{
    private static readonly PersianCalendar Calendar = new();

    public static string Format(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        var text = $"{Calendar.GetYear(local):0000}/{Calendar.GetMonth(local):00}/{Calendar.GetDayOfMonth(local):00} {local.Hour:00}:{local.Minute:00}";
        return ToPersianDigits(text);
    }

    private static string ToPersianDigits(string value)
    {
        var digits = new[] { '۰','۱','۲','۳','۴','۵','۶','۷','۸','۹' };
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
            builder.Append(c is >= '0' and <= '9' ? digits[c - '0'] : c);
        return builder.ToString();
    }
}