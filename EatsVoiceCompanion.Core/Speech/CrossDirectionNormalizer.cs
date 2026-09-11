namespace EatsVoiceCompanion.Core.Speech;

public static class CrossDirectionNormalizer
{
    public static string Normalize(string direction)
    {
        if (string.IsNullOrWhiteSpace(direction))
        {
            throw new ArgumentException(
                "A cross-distance direction is required.",
                nameof(direction));
        }

        string normalized = string.Concat(
            direction
                .Where(char.IsLetter)
                .Select(char.ToUpperInvariant));

        return normalized switch
        {
            "N" or "NORTH" => "N",
            "NE" or "NORTHEAST" => "NE",
            "E" or "EAST" => "E",
            "SE" or "SOUTHEAST" => "SE",
            "S" or "SOUTH" => "S",
            "SW" or "SOUTHWEST" => "SW",
            "W" or "WEST" => "W",
            "NW" or "NORTHWEST" => "NW",
            _ => throw new ArgumentException(
                "Direction must be north, northeast, east, southeast, " +
                "south, southwest, west, or northwest.",
                nameof(direction))
        };
    }
}
