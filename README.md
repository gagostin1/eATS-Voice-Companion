# eATS Voice Companion

<img src="assets/eats-voice-companion-logo.png" alt="eATS Voice Companion radar and microphone logo" width="160">

eATS Voice Companion is an independent Windows app that turns spoken ATC instructions into commands for the eATS enroute simulator. Recognition runs locally, commands are checked against available simulator context, and valid results are staged for review.

Created and maintained by **Gus Agostinho**.

[Download v0.2.0](https://github.com/gagostin1/eATS-Voice-Companion/releases/download/v0.2.0/EatsVoiceCompanion-win-x64.zip) · [Release notes](https://github.com/gagostin1/eATS-Voice-Companion/releases/tag/v0.2.0) · [Changelog](CHANGELOG.md)

[![Windows CI](https://github.com/gagostin1/eATS-Voice-Companion/actions/workflows/windows-ci.yml/badge.svg)](https://github.com/gagostin1/eATS-Voice-Companion/actions/workflows/windows-ci.yml)

> [!IMPORTANT]
> This project is a public beta. Review the staged command before pressing Enter in eATS. The app never presses the final Enter key for you.

## What it does

- Records with an on-screen button or configurable global push-to-talk hotkey
- Transcribes locally with Whisper and uses active eATS traffic as recognition context
- Recognizes airline and U.S. N-number callsigns
- Uses assigned STARs, flight plans, airways, and route fixes to improve command interpretation
- Ranks best-effort command hypotheses when the transcript is imperfect
- Supports multi-instruction transmissions and editable transcript retries
- Validates generated tokens and checks callsigns against the current eATS snapshot
- Automatically stages the best command in eATS while leaving transmission to the controller

Supported groups include headings, altitude and speed assignments, direct-to and route instructions, STAR/descent commands, crossing restrictions, transponder commands, reports, acknowledgements, and related combinations. See the [command catalog](docs/COMMAND_CATALOG.md) for exact phraseology and tokens.

## Install

Requires Windows 11 (x64), eATS, a microphone, the Microsoft Visual C++ 2022 Redistributable (x64), and a CPU with AVX, AVX2, FMA, and F16C support.

1. Download `EatsVoiceCompanion-win-x64.zip` from the [latest release](https://github.com/gagostin1/eATS-Voice-Companion/releases/latest).
2. Extract the entire archive to a normal folder.
3. Run `EatsVoiceCompanion.App.exe`.
4. Open **Settings**, choose your microphone and push-to-talk key, and confirm the eATS data directory.
5. Select **Detect eATS**, then **Save settings**.
6. Hold push-to-talk, speak, release, and review the staged command in eATS before pressing Enter.

The release is self-contained; no separate .NET installation is required. The Whisper model downloads on first use, so the first transcription needs an internet connection and takes longer than later ones.

## Important behavior

- Every non-empty recording with active-aircraft context attempts to produce a best-effort command and, when automatic staging is enabled, stage it.
- A verified callsign or STAR means it matched available eATS context—not that speech recognition was certainly correct.
- Best-effort results can be wrong, particularly with unclear audio, ambiguous callsigns, or unsupported phraseology.
- eATS remains the source of truth. Always inspect the radio-command field before transmitting.

Read [Safety and limitations](docs/SAFETY_AND_LIMITATIONS.md) for the detailed operating model and current constraints.

## Documentation and support

- [Supported commands and examples](docs/COMMAND_CATALOG.md)
- [Safety and limitations](docs/SAFETY_AND_LIMITATIONS.md)
- [Changelog](CHANGELOG.md)
- [Report a bug](https://github.com/gagostin1/eATS-Voice-Companion/issues/new?template=bug_report.yml)
- [Report a speech-recognition problem](https://github.com/gagostin1/eATS-Voice-Companion/issues/new?template=speech_recognition.yml)
- [Browse known issues](https://github.com/gagostin1/eATS-Voice-Companion/issues)

When reporting a recognition problem, include what you said, the transcript, the staged command, and the command you expected. Attach audio only if you are comfortable sharing it.

## Build from source

Install the .NET 8 SDK, clone the repository, then run:

```powershell
dotnet restore EatsVoiceCompanion.sln
dotnet build EatsVoiceCompanion.sln --configuration Release
dotnet test EatsVoiceCompanion.sln --configuration Release
dotnet run --project EatsVoiceCompanion.App
```

## Privacy

Speech recognition is local. Audio, transcripts, settings, and logs remain on the computer unless you choose to share them. Saved data is cleaned up according to the app's retention rules.

## Project status

This is an early public beta focused on local use with eATS. Contributions and focused bug reports are welcome.

## Independence and license

Created and maintained by Gus Agostinho. Copyright © 2026 Gus Agostinho.

This project is not affiliated with, endorsed by, or supported by eATS, FAA, or any other simulator vendor or aviation organization.

Original project code is licensed under the [MIT License](LICENSE). Third-party components remain under their own licenses.
