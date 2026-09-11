using System.Text;
using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public static class TransponderCodeParser
{
    private static readonly IReadOnlyDictionary<string, char> Digits =
        new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase)
        {
            ["zero"] = '0',
            ["oh"] = '0',
            ["one"] = '1',
            ["two"] = '2',
            ["three"] = '3',
            ["tree"] = '3',
            ["four"] = '4',
            ["fower"] = '4',
            ["five"] = '5',
            ["fife"] = '5',
            ["six"] = '6',
            ["seven"] = '7'
        };

    public static string Parse(string spokenCode)
    {
        if (string.IsNullOrWhiteSpace(spokenCode))
        {
            throw new ArgumentException(
                "A four-digit transponder code is required.",
                nameof(spokenCode));
        }

        string normalized = Regex.Replace(
            spokenCode.Trim().ToLowerInvariant(),
            @"[^\p{L}\p{N}]+",
            " ");
        string[] tokens = Regex.Replace(normalized, @"\s+", " ")
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        StringBuilder code = new(capacity: 4);

        foreach (string token in tokens)
        {
            if (token.All(character => character is >= '0' and <= '7'))
            {
                code.Append(token);
            }
            else if (Digits.TryGetValue(token, out char digit))
            {
                code.Append(digit);
            }
            else
            {
                throw new ArgumentException(
                    $"'{token}' is not a valid transponder-code digit.",
                    nameof(spokenCode));
            }
        }

        if (code.Length != 4)
        {
            throw new ArgumentException(
                "A transponder code must contain exactly four digits.",
                nameof(spokenCode));
        }

        return code.ToString();
    }
}
