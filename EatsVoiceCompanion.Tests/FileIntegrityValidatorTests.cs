using System.Security.Cryptography;
using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class FileIntegrityValidatorTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"EatsVoiceCompanionIntegrity-{Guid.NewGuid():N}.bin");

    [Fact]
    public async Task MatchesSha256Async_ReturnsTrueForMatchingFile()
    {
        byte[] content = "model data"u8.ToArray();
        await File.WriteAllBytesAsync(_path, content);
        string expectedHash = Convert.ToHexString(
            SHA256.HashData(content));

        bool result = await FileIntegrityValidator.MatchesSha256Async(
            _path,
            content.Length,
            expectedHash);

        Assert.True(result);
    }

    [Fact]
    public async Task MatchesSha256Async_ReturnsFalseForCorruptFile()
    {
        byte[] content = "corrupt"u8.ToArray();
        await File.WriteAllBytesAsync(_path, content);

        bool result = await FileIntegrityValidator.MatchesSha256Async(
            _path,
            content.Length,
            new string('0', 64));

        Assert.False(result);
    }

    [Fact]
    public async Task MatchesSha256Async_ReturnsFalseForWrongLength()
    {
        await File.WriteAllTextAsync(_path, "content");

        bool result = await FileIntegrityValidator.MatchesSha256Async(
            _path,
            expectedLength: 1,
            new string('0', 64));

        Assert.False(result);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
