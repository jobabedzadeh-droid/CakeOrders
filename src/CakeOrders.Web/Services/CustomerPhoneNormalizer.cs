namespace CakeOrders.Web.Services;

public static class CustomerPhoneNormalizer
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var digits = new string(value
            .Trim()
            .Select(ToWesternDigit)
            .Where(char.IsDigit)
            .ToArray());

        if (digits.StartsWith("0098", StringComparison.Ordinal) && digits.Length == 14)
            digits = "0" + digits[4..];
        else if (digits.StartsWith("98", StringComparison.Ordinal) && digits.Length == 12)
            digits = "0" + digits[2..];
        else if (digits.Length == 10 && digits.StartsWith('9'))
            digits = "0" + digits;

        if (digits.Length != 11 || !digits.StartsWith("09", StringComparison.Ordinal))
            return false;

        normalized = digits;
        return true;
    }

    private static char ToWesternDigit(char value)
    {
        if (value is >= '۰' and <= '۹')
            return (char)('0' + value - '۰');

        if (value is >= '٠' and <= '٩')
            return (char)('0' + value - '٠');

        return value;
    }
}
