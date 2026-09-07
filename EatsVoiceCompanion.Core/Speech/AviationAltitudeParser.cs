using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public static class AviationAltitudeParser
{
    public static int Parse(string spokenAltitude)
    {
        if (string.IsNullOrWhiteSpace(spokenAltitude))
        {
            throw new ArgumentException(
                "A spoken altitude is required.",
                nameof(spokenAltitude));
        }

        string normalized = NormalizeWords(spokenAltitude);

        if (int.TryParse(normalized, out int numericAltitude))
        {
            return numericAltitude;
        }

        string compactDigits = normalized.Replace(" ", string.Empty);

        if (compactDigits.All(char.IsDigit) &&
            int.TryParse(compactDigits, out numericAltitude))
        {
            return numericAltitude;
        }

        const string thousandWord = " thousand";

        int thousandIndex = normalized.IndexOf(
            thousandWord,
            StringComparison.OrdinalIgnoreCase);

        if (thousandIndex < 0)
        {
            return ParseHundreds(normalized);
        }

        string thousandsText =
            normalized[..thousandIndex];

        int thousands =
            AviationNumberParser.Parse(thousandsText);

        int altitude = checked(thousands * 1000);

        string remainder =
            normalized[
                (thousandIndex + thousandWord.Length)..]
            .Trim();

        if (remainder.Length == 0)
        {
            return altitude;
        }

        int hundreds = ParseHundreds(remainder);

        return checked(altitude + hundreds);
    }

    private static int ParseHundreds(string value)
    {
        const string hundredWord = " hundred";

        if (!value.EndsWith(
                hundredWord,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Altitude must use digit-by-digit aviation " +
                "phraseology followed by 'thousand' or 'hundred'.",
                nameof(value));
        }

        string hundredsText =
            value[..^hundredWord.Length].Trim();

        int hundreds =
            AviationNumberParser.Parse(hundredsText);

        if (hundreds is < 1 or > 9)
        {
            throw new ArgumentException(
                "The hundreds portion must be one through nine.",
                nameof(value));
        }

        return hundreds * 100;
    }

    private static string NormalizeWords(string value)
    {
        string normalized = Regex.Replace(
            value.Trim().ToLowerInvariant(),
            @"[^\p{L}\p{N}]+",
            " ");

        return Regex.Replace(
            normalized,
            @"\s+",
            " ").Trim();
    }
}
