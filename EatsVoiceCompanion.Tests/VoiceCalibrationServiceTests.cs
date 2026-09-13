using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class VoiceCalibrationServiceTests
{
    [Fact]
    public void Evaluate_AllDisplayedPhrasesProduceExpectedCommands()
    {
        foreach (VoiceCalibrationPhrase phrase in
                 VoiceCalibrationService.Phrases)
        {
            VoiceCalibrationResult result =
                VoiceCalibrationService.Evaluate(
                    phrase,
                    phrase.Instruction);

            Assert.True(result.IsMatch, result.Error);
            Assert.Equal(phrase.ExpectedCommand, result.GeneratedCommand);
        }
    }

    [Fact]
    public void CreateHistoryEntry_MatchIsMarkedCorrect()
    {
        VoiceCalibrationPhrase phrase =
            VoiceCalibrationService.Phrases[0];
        VoiceCalibrationResult result = new(
            phrase.Instruction,
            phrase.ExpectedCommand,
            true,
            false);

        CorrectionHistoryEntry entry =
            VoiceCalibrationService.CreateHistoryEntry(
                phrase,
                result,
                "calibration.wav");

        Assert.Equal(CorrectionReviewStatus.Correct, entry.ReviewStatus);
        Assert.Equal(result.Transcript, entry.CorrectedTranscript);
        Assert.True(entry.UseForLocalLearning);
    }

    [Fact]
    public void CreateHistoryEntry_MismatchTeachesDisplayedPhraseAndCommand()
    {
        VoiceCalibrationPhrase phrase =
            VoiceCalibrationService.Phrases[2];
        VoiceCalibrationResult result = new(
            "Delta eight twenty-five, cross Aussie.",
            "DAL825 R",
            false,
            true);

        CorrectionHistoryEntry entry =
            VoiceCalibrationService.CreateHistoryEntry(
                phrase,
                result,
                "calibration.wav");

        Assert.Equal(CorrectionReviewStatus.Corrected, entry.ReviewStatus);
        Assert.Equal(phrase.Instruction, entry.CorrectedTranscript);
        Assert.Equal(phrase.ExpectedCommand, entry.ExpectedCommand);
        Assert.Equal("Voice setup calibration", entry.ControllerPosition);
    }
}
