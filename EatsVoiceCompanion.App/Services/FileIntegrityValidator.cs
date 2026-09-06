using System.IO;
using System.Security.Cryptography;

namespace EatsVoiceCompanion.App.Services;

public static class FileIntegrityValidator
{
    public static async Task<bool> MatchesSha256Async(
        string filePath,
        long expectedLength,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "A file path is required.",
                nameof(filePath));
        }

        if (expectedLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedLength));
        }

        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            throw new ArgumentException(
                "An expected SHA-256 hash is required.",
                nameof(expectedSha256));
        }

        try
        {
            if (!File.Exists(filePath) ||
                new FileInfo(filePath).Length != expectedLength)
            {
                return false;
            }

            await using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            byte[] hash = await SHA256.HashDataAsync(
                stream,
                cancellationToken);

            return string.Equals(
                Convert.ToHexString(hash),
                expectedSha256.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
