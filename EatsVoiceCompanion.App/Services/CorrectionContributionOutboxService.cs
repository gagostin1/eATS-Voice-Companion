using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EatsVoiceCompanion.App.Services;

public sealed record CorrectionContributionOutboxItem(
    string FilePath,
    CorrectionRegressionCase RegressionCase,
    string Json)
{
    public string DisplayTitle =>
        $"{RegressionCase.RecordedAtUtc.ToLocalTime():g} · " +
        $"{RegressionCase.Callsign} · {RegressionCase.ExpectedCommand}";
}

public sealed record CorrectionContributionReceipt(
    string ReceiptId,
    string Status,
    DateTimeOffset SentAtUtc,
    CorrectionRegressionCase Contribution)
{
    public string DisplayTitle =>
        $"{SentAtUtc.ToLocalTime():g} · {Contribution.Callsign} · " +
        Contribution.ExpectedCommand;
}

public sealed class CorrectionContributionOutboxService
{
    private const int MaximumItems = 500;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly CorrectionHistoryService _historyService;

    public CorrectionContributionOutboxService(
        CorrectionHistoryService historyService,
        string? outboxDirectory = null,
        string? sentDirectory = null)
    {
        _historyService = historyService;
        OutboxDirectory = outboxDirectory ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "EatsVoiceCompanion",
            "ContributionOutbox");
        SentDirectory = sentDirectory ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "EatsVoiceCompanion",
            "SentContributions");
    }

    public string OutboxDirectory { get; }

    public string SentDirectory { get; }

    public bool Enqueue(
        CorrectionHistoryEntry entry,
        string appVersion,
        IReadOnlyDictionary<string, string> airlineAliases)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.ReviewStatus != CorrectionReviewStatus.Corrected)
        {
            return false;
        }

        CorrectionRegressionCase regressionCase =
            _historyService.CreateRegressionCase(
                entry,
                appVersion,
                airlineAliases);
        string fingerprint = CreateFingerprint(regressionCase);
        string path = Path.Combine(OutboxDirectory, $"{fingerprint}.json");

        if (File.Exists(path))
        {
            return false;
        }

        Directory.CreateDirectory(OutboxDirectory);
        string temporaryPath = path + ".tmp";

        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(regressionCase, SerializerOptions));
            File.Move(temporaryPath, path, overwrite: false);
        }
        finally
        {
            TryDelete(temporaryPath);
        }

        TrimExcessItems();
        return true;
    }

    public IReadOnlyList<CorrectionContributionOutboxItem> Load()
    {
        if (!Directory.Exists(OutboxDirectory))
        {
            return [];
        }

        List<CorrectionContributionOutboxItem> items = [];

        foreach (string path in Directory.EnumerateFiles(
                     OutboxDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                string json = File.ReadAllText(path);
                CorrectionRegressionCase regressionCase =
                    JsonSerializer.Deserialize<CorrectionRegressionCase>(
                        json,
                        SerializerOptions) ??
                    throw new InvalidDataException(
                        "The outbox file did not contain a regression case.");
                CorrectionRegressionCaseValidator.Validate(regressionCase);
                items.Add(new CorrectionContributionOutboxItem(
                    path,
                    regressionCase,
                    json));
            }
            catch (JsonException)
            {
                // A damaged contribution must not hide valid pending items.
            }
            catch (InvalidDataException)
            {
                // Invalid files remain available for manual inspection.
            }
            catch (IOException)
            {
                // A temporarily unavailable file is skipped until refresh.
            }
            catch (UnauthorizedAccessException)
            {
                // A temporarily unavailable file is skipped until refresh.
            }
        }

        return items
            .OrderByDescending(item => item.RegressionCase.RecordedAtUtc)
            .ToArray();
    }

    public void Delete(CorrectionContributionOutboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        string fullPath = Path.GetFullPath(item.FilePath);
        string fullDirectory = Path.GetFullPath(OutboxDirectory);

        if (!string.Equals(
                Path.GetDirectoryName(fullPath),
                fullDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected file is outside the contribution outbox.");
        }

        File.Delete(fullPath);
    }

    public void MarkSent(
        CorrectionContributionOutboxItem item,
        string receiptId,
        string status,
        DateTimeOffset sentAtUtc)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptId);

        if (receiptId.Length != 64 || !receiptId.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "The server receipt identifier is invalid.",
                nameof(receiptId));
        }

        if (status is not ("accepted" or "duplicate"))
        {
            throw new ArgumentException(
                "The server receipt status is invalid.",
                nameof(status));
        }

        ValidateOutboxItemPath(item);
        Directory.CreateDirectory(SentDirectory);
        string destinationPath = Path.Combine(
            SentDirectory,
            $"{receiptId.ToLowerInvariant()}.json");
        string temporaryPath = destinationPath + ".tmp";
        CorrectionContributionReceipt receipt = new(
            receiptId.ToLowerInvariant(),
            status,
            sentAtUtc.ToUniversalTime(),
            item.RegressionCase);

        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(receipt, SerializerOptions));
            File.Move(temporaryPath, destinationPath, overwrite: true);
            File.Delete(item.FilePath);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    public IReadOnlyList<CorrectionContributionReceipt> LoadSent()
    {
        if (!Directory.Exists(SentDirectory))
        {
            return [];
        }

        List<CorrectionContributionReceipt> receipts = [];

        foreach (string path in Directory.EnumerateFiles(
                     SentDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                CorrectionContributionReceipt? receipt =
                    JsonSerializer.Deserialize<CorrectionContributionReceipt>(
                        File.ReadAllText(path),
                        SerializerOptions);
                if (receipt is not null)
                {
                    receipts.Add(receipt);
                }
            }
            catch (JsonException)
            {
                // A damaged receipt must not hide the remaining sent history.
            }
            catch (IOException)
            {
                // A temporarily unavailable receipt is skipped until refresh.
            }
            catch (UnauthorizedAccessException)
            {
                // A temporarily unavailable receipt is skipped until refresh.
            }
        }

        return receipts
            .OrderByDescending(receipt => receipt.SentAtUtc)
            .ToArray();
    }

    public int ExportAll(string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        string destination = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destination);
        int exported = 0;

        foreach (CorrectionContributionOutboxItem item in Load())
        {
            string destinationPath = Path.Combine(
                destination,
                Path.GetFileName(item.FilePath));
            File.Copy(item.FilePath, destinationPath, overwrite: true);
            exported++;
        }

        return exported;
    }

    private static string CreateFingerprint(
        CorrectionRegressionCase regressionCase)
    {
        var deduplicationContent = new
        {
            regressionCase.Callsign,
            AirlineAliases = regressionCase.AirlineAliases
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase),
            regressionCase.OriginalTranscript,
            regressionCase.GeneratedCommand,
            regressionCase.CorrectedTranscript,
            regressionCase.ExpectedCommand,
            regressionCase.ControllerPosition,
            regressionCase.ActiveStar,
            RouteFixes = regressionCase.RouteFixes
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        };
        string json = JsonSerializer.Serialize(deduplicationContent);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void ValidateOutboxItemPath(
        CorrectionContributionOutboxItem item)
    {
        string fullPath = Path.GetFullPath(item.FilePath);
        string fullDirectory = Path.GetFullPath(OutboxDirectory);

        if (!string.Equals(
                Path.GetDirectoryName(fullPath),
                fullDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected file is outside the contribution outbox.");
        }
    }

    private void TrimExcessItems()
    {
        FileInfo[] files = new DirectoryInfo(OutboxDirectory)
            .GetFiles("*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();

        foreach (FileInfo file in files.Skip(MaximumItems))
        {
            TryDelete(file.FullName);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Cleanup is best effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup is best effort.
        }
    }
}
