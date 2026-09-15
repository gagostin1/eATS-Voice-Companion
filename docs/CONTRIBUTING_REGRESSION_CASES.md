# Contributing a speech regression case

Regression cases preserve real recognition failures as automated tests. They contain text and the minimum relevant simulator context; they do not contain audio or local file paths.

## Export a case

1. Open **History** in eATS Voice Companion.
2. Select the failed attempt.
3. Enter the corrected transcript and expected eATS command.
4. Select **Save correction**.
5. Select **Export regression case** and review the JSON before sharing it.

You can attach the JSON to the [speech-recognition issue form](https://github.com/gagostin1/eATS-Voice-Companion/issues/new?template=speech_recognition.yml). Audio is never part of the regression-case export.

## Send queued corrections

When recognition-improvement participation is enabled, saving a correction
also places a sanitized schema-v2 case in **Settings > Development feedback**.
The app sends these cases automatically when participation is enabled.
Confirmed submissions move to a local sent archive; if the service cannot
confirm receipt, the case stays pending and retries at startup, after later
corrections, and periodically while the app is open. You can inspect or delete
pending JSON and choose **Retry pending now** in Settings.

## Add a case in a pull request

Place the exported JSON in [`regression-cases/speech`](../regression-cases/speech). Use a short descriptive filename, keep one case per file, and do not manually add audio, local paths, or unrelated traffic.

Cases follow [`schema-v2.json`](../regression-cases/schema-v2.json). Run:

```powershell
dotnet test EatsVoiceCompanion.sln
```

Every JSON file is validated and executed automatically. A new failure case should be submitted with the interpreter fix that makes its expected command pass.
