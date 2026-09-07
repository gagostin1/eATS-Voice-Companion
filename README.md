# eATS Voice Companion

eATS Voice Companion is an independent Windows application that adds local voice recognition, safety-checked command previews, and explicit stage-only command entry to the eATS enroute air traffic control simulator.

[Download eATS Voice Companion v0.1.0](https://github.com/gagostin1/eATS-Voice-Companion/releases/download/v0.1.0/EatsVoiceCompanion-win-x64.zip) · [Release notes](https://github.com/gagostin1/eATS-Voice-Companion/releases/tag/v0.1.0) · [Changelog](CHANGELOG.md)

[![Windows CI](https://github.com/gagostin1/eATS-Voice-Companion/actions/workflows/windows-ci.yml/badge.svg)](https://github.com/gagostin1/eATS-Voice-Companion/actions/workflows/windows-ci.yml)

The current workflow records a controller transmission, transcribes it locally with Whisper, interprets supported ATC phraseology, formats the matching eATS command, and verifies the callsign against a recent eATS snapshot. A controller may explicitly stage a freshly verified command in eATS for review; the application never presses the final Enter key or transmits the command automatically.

> [!IMPORTANT]
> This project is an early pre-release. Always verify the transcript, callsign, instruction, and value shown in the preview. A green callsign check confirms only that the exact callsign appears in a recent snapshot; it does not prove that the recognized instruction is operationally correct.

## Current features

- Discovers Windows recording devices and records hold-to-talk audio as 16 kHz, 16-bit, mono WAV files
- Downloads the Whisper `base.en` model on first use and performs speech recognition locally
- Loads airline telephony names and designators from the user's installed eATS `Airlines.txt`
- Reads active aircraft from eATS `SnapshotAuto.txt` without modifying either file
- Adds active airline callsigns to the speech-recognition prompt to improve callsign recognition
- Supports an optional controller position in transmissions, such as `American 1307, Atlanta Center, ...`
- Converts single or combined recognized instructions into editable eATS command previews
- Detects a running eATS process and displays basic process information
- Classifies previews as verified, preview-only, or blocked using exact callsign matching against a fresh snapshot
- Rejects previews that fall outside a strict allowlist of supported eATS command tokens and characters
- Stages a freshly revalidated command in the eATS radio-command field only after explicit confirmation, without pressing the final Enter key
- Provides visible cancellation while the model is downloading or a recording is being transcribed
- Persists the controller position, preferred microphone, eATS data path, snapshot threshold, and recording-retention policy
- Removes expired and excess recordings using configurable age and count limits
- Writes privacy-conscious structured diagnostics as local JSON Lines files and retains them for 14 days
- Verifies the pinned speech model by file size and SHA-256, replacing incomplete or corrupt copies before transcription
- Keeps parsing, formatting, and validation logic in a separately tested core library

Supported instructions:

| Spoken instruction | Example eATS preview |
| --- | --- |
| Fly heading | `DAL123 FH270` |
| Turn left heading | `DAL123 TLH270` |
| Turn right heading | `DAL123 TRH270` |
| Climb and maintain | `DAL123 CM230` |
| Descend and maintain | `DAL123 DM100` |
| Descend via | `DAL123 DV` |
| Descend via except maintain | `DAL123 DVXM120` |
| Maintain speed | `DAL123 S250` |
| Proceed direct | `DAL123 ..LOZIT` |
| Cross a fix at an altitude | `DAL123 XOZZZI@120` |
| Cross a fix at an altitude and speed | `DAL123 XOZZZI@120@250K` |
| Altimeter | `DAL123 A2992` |
| Roger or welcome | `DAL123 R` |

Examples of accepted phraseology include:

```text
Delta one two three, turn left heading two seven zero.
United seven fourteen, climb and maintain flight level two three zero.
American four five, descend and maintain one zero thousand five hundred.
American thirteen oh seven, cross OZZZI at and maintain one two thousand at two five zero knots, the Atlanta altimeter two niner niner two.
American thirteen oh seven, Atlanta Center, welcome.
```

See the [command catalog](docs/COMMAND_CATALOG.md) for manual value formats,
combined-command behavior, safety restrictions, and the planned command groups.

## Safety behavior

The preview displays one of three callsign states:

- **Verified (green):** eATS is running, its automatic snapshot is within the configured freshness threshold, and the exact callsign is active.
- **Preview only (amber):** a current snapshot is unavailable, so the callsign cannot be verified.
- **Blocked (red):** a current snapshot exists but does not contain the exact callsign, or the recording/transcript did not produce a valid command.

Snapshot context is loaded at startup and refreshed before transcription, after transcription, when eATS detection is requested, and before a manual preview is built. The default freshness threshold is three minutes. eATS normally refreshes `SnapshotAuto.txt` about once per minute while the simulation is active.

Every generated preview also passes through a final grammar allowlist. Only the characters and complete command tokens required by the currently supported syntax are accepted. Control characters, unknown tokens, invalid values, unsafe command ordering, and partially recognized combined instructions are rejected before staging can receive them.

The supplied eATS reference notes that eATS processes multiple tokens in order and may act on valid tokens before encountering a later operational error. The companion validates the entire generated sequence before staging, but the controller must still inspect every token because aircraft state can cause simulator-side rejection. The application never presses the final Enter key.

Only a green, freshly revalidated command can be staged. After confirmation, the application verifies that the detected window still belongs to eATS, brings it to the foreground, clears incomplete input with **Esc**, enters the radio-command field, and types the validated command. Focus is checked before every input phase. The final Enter key is never generated; the controller must inspect and transmit the command manually.

## Requirements

- Windows 11 x64
- Microsoft Visual C++ Redistributable for Visual Studio 2022 (x64)
- A processor with AVX, AVX2, FMA, and F16C support for the included Whisper CPU runtime
- A Windows-compatible microphone
- eATS installed separately
- Internet access on first use to download the Whisper model

The application uses [NAudio](https://www.nuget.org/packages/NAudio) for recording and [Whisper.net](https://www.nuget.org/packages/Whisper.net) for local speech recognition.

The self-contained release includes the .NET runtime. Building from source additionally requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Git, and Visual Studio, Visual Studio Code, or another C# editor.

## Installing the pre-release

1. Download [`EatsVoiceCompanion-win-x64.zip`](https://github.com/gagostin1/eATS-Voice-Companion/releases/download/v0.1.0/EatsVoiceCompanion-win-x64.zip) from the [v0.1.0 release](https://github.com/gagostin1/eATS-Voice-Companion/releases/tag/v0.1.0).
2. Verify the package checksum shown in the release notes if desired.
3. Extract the entire ZIP to a folder. Do not run the application from inside the ZIP archive.
4. Start `EatsVoiceCompanion.App.exe` from the extracted folder.
5. If Windows displays an unknown-publisher warning, confirm that the file came from this repository's release and that its SHA-256 matches before choosing whether to run it.
6. On the first transcription, allow the application to download and verify the local Whisper model.

The application does not need to be placed inside the eATS installation directory. It discovers the running simulator and reads the configured eATS data directory separately.

## Building from source

Clone the repository and enter its directory:

```powershell
git clone https://github.com/gagostin1/eATS-Voice-Companion.git
cd eATS-Voice-Companion
```

Restore, build, and test the solution:

```powershell
dotnet restore
dotnet build
dotnet test
```

Start the application:

```powershell
dotnet run --project .\EatsVoiceCompanion.App\EatsVoiceCompanion.App.csproj
```

On first transcription, the application downloads `ggml-base.en.bin` to:

```text
%LOCALAPPDATA%\EatsVoiceCompanion\Models
```

Settings and structured diagnostic logs are stored in:

```text
%LOCALAPPDATA%\EatsVoiceCompanion\settings.json
%LOCALAPPDATA%\EatsVoiceCompanion\Logs
```

Test recordings are stored in:

```text
%TEMP%\EatsVoiceCompanion
```

Airline and snapshot data are read from this default eATS data directory:

```text
%LOCALAPPDATA%\ATSim2020\eATS
```

The data directory can be changed in the application and is saved locally. The application does not copy eATS data files into the repository.

## Using the application

1. Start eATS, load a simulation, and allow its automatic snapshot to refresh.
2. Start eATS Voice Companion and select the intended microphone.
3. Review the settings, including the eATS data path and snapshot freshness, and choose **Save settings** after making changes.
4. Enter the exact controller position you plan to say, if any.
5. Hold **Hold to record**, speak one or more supported instructions, and release the button.
6. Choose **Cancel transcription** if recognition needs to be stopped.
7. Review the transcript and generated command.
8. Confirm that the callsign safety message matches the expected active aircraft.
9. Edit the preview fields and choose **Build preview** if a correction is needed.
10. Choose **Stage in eATS**, review the confirmation, and approve it only if the command is correct.
11. Inspect the staged text in the lower-left eATS radio-command field and press **Enter** yourself only when it is safe to transmit.

## Known limitations

- The application supports airline callsigns but does not yet parse spoken general-aviation registration callsigns such as N-numbers.
- Named STARs in descend-via phraseology are not yet accepted because the application cannot verify a spoken procedure name against the aircraft's current route.
- At-or-above and at-or-below crossing restrictions are not generated because the supplied eATS radio reference does not define equivalent command tokens.
- Recognition uses the English `base.en` Whisper model and does not expose confidence scoring.
- Tests cover core behavior and file/service integration, but actual microphone hardware and WPF interaction still require manual testing.
- Stage-only entry depends on Windows foreground input; it aborts if eATS loses focus, and both applications should run at the same Windows privilege level.
- There is no installer, signed release, or automatic command transmission.

## Project structure

```text
EatsVoiceCompanion.App/     WPF interface, audio, speech recognition, and eATS file/process integration
EatsVoiceCompanion.Core/    Parsing, command formatting, prompt construction, and safety validation
EatsVoiceCompanion.Tests/   Core and application-service integration tests
```

## Development roadmap

- Expand the [command catalog](docs/COMMAND_CATALOG.md), beginning with route-aware STAR and published-speed instructions
- Support general-aviation callsign phraseology
- Add automated WPF interaction tests and hardware-in-the-loop microphone tests
- Add an installer, code signing, and automated tagged releases

## Privacy

Recordings and transcription stay local. The application downloads the speech model on first use, but it does not upload recorded audio or transcripts. Saved WAV recordings are deleted on a best-effort basis when they exceed the configured age or count limits.

Diagnostic logs stay local and intentionally omit full transcripts, generated commands, and callsigns. They contain event names, status metadata, and exception types/messages for troubleshooting. Logs older than 14 days are removed on a best-effort basis.

## Independence and legal notice

This is an original, unofficial companion project. It is not affiliated with, endorsed by, or distributed with eATS, ATSimulations, or VICE.

The project does not include eATS executables, documentation, scenarios, data files, branding, or other proprietary material. Users must obtain and install eATS separately and comply with its applicable terms. No source code or other protected implementation from VICE is used in this project.

Product names and trademarks belong to their respective owners and are referenced only to explain compatibility and the project's intended purpose.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

This license applies only to the original source code in this repository. It does not apply to eATS or any third-party software, documentation, data, or trademarks.
