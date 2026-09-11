using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public static class NNumberCallsignParser
{
    private static readonly Regex RegistrationPattern =
        new(
            "^N(?:[1-9][0-9]{0,4}|" +
            "[1-9][0-9]{0,3}[A-HJ-NP-Z]|" +
            "[1-9][0-9]{0,2}[A-HJ-NP-Z]{2})$",
            RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, char> Characters =
        new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase)
        {
            ["zero"] = '0',
            ["oh"] = '0',
            ["one"] = '1',
            ["two"] = '2',
            ["to"] = '2',
            ["three"] = '3',
            ["tree"] = '3',
            ["four"] = '4',
            ["fower"] = '4',
            ["five"] = '5',
            ["fife"] = '5',
            ["six"] = '6',
            ["seven"] = '7',
            ["eight"] = '8',
            ["nine"] = '9',
            ["niner"] = '9',
            ["alpha"] = 'A',
            ["alfa"] = 'A',
            ["bravo"] = 'B',
            ["charlie"] = 'C',
            ["delta"] = 'D',
            ["echo"] = 'E',
            ["foxtrot"] = 'F',
            ["golf"] = 'G',
            ["hotel"] = 'H',
            ["juliett"] = 'J',
            ["juliet"] = 'J',
            ["kilo"] = 'K',
            ["lima"] = 'L',
            ["mike"] = 'M',
            ["november"] = 'N',
            ["papa"] = 'P',
            ["quebec"] = 'Q',
            ["romeo"] = 'R',
            ["sierra"] = 'S',
            ["tango"] = 'T',
            ["uniform"] = 'U',
            ["victor"] = 'V',
            ["whiskey"] = 'W',
            ["xray"] = 'X',
            ["yankee"] = 'Y',
            ["zulu"] = 'Z'
        };

    public static string Parse(string spokenCallsign)
    {
        if (!TryParse(spokenCallsign, out string? callsign))
        {
            throw new ArgumentException(
                "The N-number callsign was not recognized.",
                nameof(spokenCallsign));
        }

        return callsign;
    }

    public static bool TryParse(
        string? spokenCallsign,
        out string callsign)
    {
        callsign = string.Empty;

        if (string.IsNullOrWhiteSpace(spokenCallsign))
        {
            return false;
        }

        string normalized = Regex.Replace(
            spokenCallsign.Trim().ToLowerInvariant(),
            @"[^\p{L}\p{N}]+",
            " ");
        normalized = Regex.Replace(normalized, @"\bx\s+ray\b", "xray");
        string[] tokens = Regex.Replace(normalized, @"\s+", " ")
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 1)
        {
            string compact = tokens[0].ToUpperInvariant();

            if (IsValid(compact))
            {
                callsign = compact;
                return true;
            }
        }

        if (tokens.Length < 2 ||
            tokens[0] is not ("november" or "n"))
        {
            return false;
        }

        List<char> suffix = new(capacity: 5);

        foreach (string token in tokens.Skip(1))
        {
            if (token.All(char.IsDigit))
            {
                suffix.AddRange(token);
            }
            else if (token.Length == 1 && char.IsLetter(token[0]))
            {
                suffix.Add(char.ToUpperInvariant(token[0]));
            }
            else if (Characters.TryGetValue(token, out char character))
            {
                suffix.Add(character);
            }
            else
            {
                return false;
            }

            if (suffix.Count > 5)
            {
                return false;
            }
        }

        string candidate = "N" + new string(suffix.ToArray());

        if (!IsValid(candidate))
        {
            return false;
        }

        callsign = candidate;
        return true;
    }

    public static bool IsValid(string? callsign)
    {
        return !string.IsNullOrWhiteSpace(callsign) &&
               RegistrationPattern.IsMatch(
                   callsign.Trim().ToUpperInvariant());
    }

    public static string ToPromptPhrase(string callsign)
    {
        string normalized = callsign.Trim().ToUpperInvariant();

        if (!IsValid(normalized))
        {
            throw new ArgumentException(
                "A valid N-number is required.",
                nameof(callsign));
        }

        return "November " + string.Join(
            ' ',
            normalized[1..].Select(ToPromptWord));
    }

    private static string ToPromptWord(char character)
    {
        return character switch
        {
            '0' => "Zero",
            '1' => "One",
            '2' => "Two",
            '3' => "Three",
            '4' => "Four",
            '5' => "Five",
            '6' => "Six",
            '7' => "Seven",
            '8' => "Eight",
            '9' => "Niner",
            'A' => "Alpha",
            'B' => "Bravo",
            'C' => "Charlie",
            'D' => "Delta",
            'E' => "Echo",
            'F' => "Foxtrot",
            'G' => "Golf",
            'H' => "Hotel",
            'J' => "Juliett",
            'K' => "Kilo",
            'L' => "Lima",
            'M' => "Mike",
            'N' => "November",
            'P' => "Papa",
            'Q' => "Quebec",
            'R' => "Romeo",
            'S' => "Sierra",
            'T' => "Tango",
            'U' => "Uniform",
            'V' => "Victor",
            'W' => "Whiskey",
            'X' => "X-ray",
            'Y' => "Yankee",
            'Z' => "Zulu",
            _ => throw new ArgumentOutOfRangeException(nameof(character))
        };
    }
}
