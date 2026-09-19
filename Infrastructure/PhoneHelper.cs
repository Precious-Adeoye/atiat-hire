namespace AtiatHire.Infrastructure;

public static class PhoneHelper
{
    /// <summary>
    /// Reduces a phone number to digits in international form for Nigerian numbers,
    /// e.g. "0803 123 4567" and "+234 803 123 4567" both become "2348031234567".
    /// </summary>
    public static string Normalize(string? raw)
    {
        var digits = new string((raw ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digits.StartsWith("00")) digits = digits[2..];
        if (digits.StartsWith("2340")) digits = "234" + digits[4..];   // "+234 0803..." typed by mistake
        if (digits.Length == 11 && digits[0] == '0') digits = "234" + digits[1..];

        return digits;
    }

    public static bool Matches(string? a, string? b)
    {
        var na = Normalize(a);
        return na.Length > 0 && na == Normalize(b);
    }
}
