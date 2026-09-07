using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public static partial class StarNameNormalizer
{
    public static string Normalize(string spokenName)
    {
        if (string.IsNullOrWhiteSpace(spokenName))
        {
            throw new ArgumentException(
                "A STAR name is required.",
                nameof(spokenName));
        }

        string normalized = Regex.Replace(
            spokenName.Trim().ToLowerInvariant(),
            @"[^\p{L}\p{N}]+",
            " ");
        string[] words = Regex.Replace(normalized, @"\s+", " ")
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 1)
        {
            string compact = words[0].ToUpperInvariant();

            if (ProcedurePattern().IsMatch(compact))
            {
                return compact;
            }
        }

        if (words.Length != 2)
        {
            throw new ArgumentException(
                "A STAR must be spoken as its name followed by its number.",
                nameof(spokenName));
        }

        int number = AviationNumberParser.Parse(words[1]);
        string result = words[0].ToUpperInvariant() + number;

        if (!ProcedurePattern().IsMatch(result))
        {
            throw new ArgumentException(
                "The spoken STAR name is not in a supported format.",
                nameof(spokenName));
        }

        return result;
    }

    public static string ToPromptPhrase(string procedureName)
    {
        string normalized = Normalize(procedureName);
        char number = normalized[^1];

        string numberWord = number switch
        {
            '1' => "one",
            '2' => "two",
            '3' => "three",
            '4' => "four",
            '5' => "five",
            '6' => "six",
            '7' => "seven",
            '8' => "eight",
            '9' => "niner",
            _ => throw new ArgumentException(
                "The STAR number must be one through nine.",
                nameof(procedureName))
        };

        return $"{normalized[..^1]} {numberWord}";
    }

    [GeneratedRegex(
        "^[A-Z][A-Z0-9]{1,7}[1-9]$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ProcedurePattern();
}
