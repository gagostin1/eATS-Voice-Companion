# eATS Voice Companion

eATS Voice Companion is an independent Windows application that adds local voice recognition, safety-checked command previews, and explicit stage-only command entry to the eATS enroute air traffic control simulator.

[Download eATS Voice Companion v0.1.0](https://github.com/gagostin1/eATS-Voice-Companion/releases/download/v0.1.0/EatsVoiceCompanion-win-x64.zip) · [Release notes](https://github.com/gagostin1/eATS-Voice-Companion/releases/tag/v0.1.0) · [Changelog](CHANGELOG.md)

[![Windows CI](https://github.com/gagostin1/eATS-Voice-Companion/actions/workflows/windows-ci.yml/badge.svg)](https://github.com/gagostin1/eATS-Voice-Companion/actions/workflows/windows-ci.yml)

The current workflow records a controller transmission, transcribes it locally with Whisper, interprets supported ATC phraseology, formats the matching eATS command, and verifies the callsign against a recent eATS snapshot. A controller may explicitly stage a freshly verified command in eATS for review; the application never presses the final Enter key or transmits the command automatically.

> [!IMPORTANT]
> This project is an early pre-release. Always verify the transcript, callsign, instruction, and value shown in the preview. A green check confirms the available eATS context required for that command; it does not prove that the recognized instruction is operationally correct.

## Current features

- Discovers Windows recording devices and records hold-to-talk audio as 16 kHz, 16-bit, mono WAV files
- Downloads the higher-accuracy Whisper `small.en` model on first use and performs local speech recognition with beam-search decoding
- Loads airline telephony names and designators from the user's installed eATS `Airlines.txt`
- Recognizes full U.S. registration callsigns such as `N253PZ` from compact text or `November two five three papa zulu`
- Reads active aircraft from eATS `SnapshotAuto.txt` without modifying it
- Resolves descend-via-capable assigned STARs from eATS `LogDetail.txt` and `Airways.txt`
- Supports published-speed compliance at a named fix while descending via, with simulator-safe command ordering
- Supports pilot's-discretion descent, expedite, report-leaving/reaching, and say-altitude instructions
- Supports transponder code, IDENT, altitude-reporting, normal, standby, and VFR instructions
- Adds active airline and N-number callsigns to the speech-recognition prompt to improve callsign recognition
- Attempts a clearly labeled, constrained best-effort interpretation when raw transcription cannot be parsed, using only active eATS callsigns, supported instruction phrases, and assigned STAR context
- Lets the controller correct an imperfect transcript and interpret it again without making another recording
- Reports likely active callsigns when a fuzzy callsign remains ambiguous instead of choosing one silently
- Supports an optional controller position in transmissions, such as `American 1307, Atlanta Center, ...`
- Converts single or combined recognized instructions into editable eATS command previews
- Detects a running eATS process and displays basic process information
- Classifies previews as verified, preview-only, or blocked using fresh callsign and, for descend-via commands, assigned-STAR context
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
| Fly present heading | `DAL123 PH` |
| Climb and maintain | `DAL123 CM230` |
| Descend and maintain | `DAL123 DM100` |
| Descend via, optionally naming the assigned STAR | `DAL123 DV` |
| Descend via the assigned STAR except maintain | `DAL123 DVXM120` |
| Descend at pilot's discretion | `DAL123 PD120` |
| Expedite the current altitude clearance | `DAL123 EXP` |
| Expedite through/to an altitude | `DAL123 EXP280` |
| Report leaving/reaching an altitude | `DAL123 RL240` / `DAL123 RR120` |
| Say altitude | `DAL123 SA` |
| Maintain speed | `DAL123 S250` |
| Maintain speed or greater/less | `DAL123 S250+` / `DAL123 S250-` |
| Maintain Mach, optionally or greater/less | `DAL123 MM76` / `DAL123 MM76+` / `DAL123 MM76-` |
| Resume normal speed | `DAL123 RNS` |
| Say indicated speed, Mach, or normal speed/Mach | `DAL123 SI` / `DAL123 SM` / `DAL123 SNS` |
| Expect an approach | `DAL123 EILS25L` |
| Fly present heading and intercept final | `DAL123 PH INTC` |
| Clear the approach currently in the pilot route | `DAL123 CA` |
| Say approach request | `DAL123 SAR` |
| Clear the approach and reduce to final approach speed | `DAL123 CA S-` |
| Contact a frequency | `DAL123 *3237` |
| Remain this frequency | `DAL123 *0` |
| Say again | `DAL123 ?` |
| Stand by | `DAL123 SBY` |
| Squawk a four-digit octal code | `DAL123 SQ4321` |
| Squawk ident | `DAL123 ID` |
| Squawk altitude/normal/standby/VFR | `DAL123 SQALT` / `DAL123 SQNORM` / `DAL123 SQSBY` / `DAL123 SQVFR` |
| Stop altitude squawk | `DAL123 STOPALTSQ` |
| Descend via and comply with published speeds at a fix | `DAL123 DV CWS@HOMER` |
| Proceed direct | `DAL123 ..LOZIT` |
| Cross a fix at an altitude | `DAL123 XOZZZI@120` |
| Cross a fix at an altitude and speed | `DAL123 XOZZZI@120@250K` |
| Cross a distance and direction from a fix at an altitude | `DAL123 X10NW.BURGL@330` |
| Altimeter | `DAL123 A2992` |
| Roger or welcome | `DAL123 R` |

Examples of accepted phraseology include:

```text
Delta one two three, turn left heading two seven zero.
United seven fourteen, climb and maintain flight level two three zero.
American four five, descend and maintain one zero thousand five hundred.
Brickyard fifty-five eighty-eight, descend via the BANKR Five arrival.
Delta one two three, descend at pilot's discretion, maintain one two thousand.
United seven fourteen, descend and maintain one two thousand, then expedite.
American thirteen oh seven, report leaving flight level two four zero.
American thirteen oh seven, descend via, maintain speed two five zero, then comply with speed restrictions at HOMER.
Delta one two three, maintain Mach point seven six or greater.
United seven fourteen, resume normal speed, then say indicated speed.
Delta one two three, expect ILS runway two five left approach.
Delta one two three, fly heading two two zero, then intercept the final approach course.
Delta one two three, cleared for the approach.
Delta one two three, cleared for the approach, then reduce to final approach speed.
Delta one two three, contact Jacksonville Center one three two point three seven.
Delta one two three, remain this frequency.
Delta one two three, say again.
Delta one two three, squawk four three two one and ident.
November two five three papa zulu, squawk VFR.
American thirteen oh seven, cross OZZZI at and maintain one two thousand at two five zero knots, the Atlanta altimeter two niner niner two.
Delta one two three, cross ten miles northwest of BURGL at flight level three three zero.
American thirteen oh seven, Atlanta Center, welcome.
November two five three papa zulu, Atlanta Center, fly heading two seven zero.
November three papa zulu, Atlanta Center, say altitude.
```

See the [command catalog](docs/COMMAND_CATALOG.md) for manual value formats,
combined-command behavior, safety restrictions, and the planned command groups.

## Safety behavior

The preview displays one of three callsign states:

- **Verified (green):** eATS is running, its automatic snapshot is fresh, and the exact callsign is active. A descend-via command additionally requires a fresh route whose assigned STAR supports descend via; a spoken STAR name must match it.
- **Preview only (amber):** required current snapshot or route context is unavailable, so the command cannot be fully verified.
- **Blocked (red):** current context disproves the callsign or spoken STAR, or the recording/transcript did not produce a valid command.

Snapshot and route context are loaded at startup and refreshed before transcription, after transcription, when eATS detection is requested, and before a manual preview is built. The default freshness threshold is three minutes. eATS normally refreshes `SnapshotAuto.txt` and `LogDetail.txt` while the simulation is active. `Airways.txt` is treated as the installed static procedure database.

Every generated preview also passes through a final grammar allowlist. Only the characters and complete command tokens required by the currently supported syntax are accepted. Control characters, unknown tokens, invalid values, unsafe command ordering, and partially recognized combined instructions are rejected before staging can receive them. Assigned speed and Mach commands use the documented ranges and exact/greater/less suffixes. Published-speed compliance uses the canonical `CWS@FIX` token, must follow `DV` or `DVXM` in the same preview, and must be reissued after a later direct or descend-via command. Expedite is rejected alongside descend via or pilot's-discretion descent, and it must follow the final altitude command because a later altitude assignment cancels it in eATS.

Transponder assignments require exactly four separately spoken octal digits (`0` through `7`). Conflicting code, operating-mode, or altitude-reporting instructions are rejected in one preview. Code-plus-IDENT and code-plus-altitude-reporting combinations remain supported.

Cross-distance restrictions require 1-999 miles, an eight-point compass direction, a full 2-8 character fix identifier, and an altitude. Only one can appear in a preview. A later direct command is rejected because eATS would remove the cross-distance restriction. Fix existence and route geometry remain controller-verified.

If strict parsing fails, the companion may display a **Best-effort interpretation generated** warning. Recovery is limited to a uniquely matched active callsign, a sufficiently similar supported instruction phrase, and—when applicable—a sufficiently similar assigned STAR. Ambiguous callsigns and unsupported instructions remain rejected; when useful, the error lists likely active callsigns for the controller to compare. The recovered wording is displayed beneath the original transcript and must be reviewed before staging. The transcript itself can also be corrected and submitted with **Interpret again** without rerecording. Callsigns from a stale snapshot may assist recovery, but the resulting preview remains amber and cannot be staged until fresh context is available.

Full N-numbers follow FAA registration structure and are spoken as `November` followed by individual digits and phonetic letters. The companion also accepts `November` plus the final three registration characters only when exactly one active N-number in the current snapshot has that suffix. A missing or ambiguous match remains fail-closed and is never silently selected.

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

The test suite includes a generated voice-interpretation matrix that exercises
the same interpreter used by the application across multiple airline
telephony names, N-number shapes, flight-number shapes, controller positions,
assigned STARs, strict command families, and constrained-recovery cases. This keeps recognition
coverage independent of any one eATS scenario. Microphone transcription and
foreground staging still require the manual checks described below.

Start the application:

```powershell
dotnet run --project .\EatsVoiceCompanion.App\EatsVoiceCompanion.App.csproj
```

On first transcription, the application downloads the approximately 488 MB `ggml-small.en.bin` model to:

```text
%LOCALAPPDATA%\EatsVoiceCompanion\Models
```

Existing development installations may retain the older `ggml-base.en.bin` in
that directory. After confirming `small.en` works, it may be deleted while the
application is closed to reclaim space.

Settings and structured diagnostic logs are stored in:

```text
%LOCALAPPDATA%\EatsVoiceCompanion\settings.json
%LOCALAPPDATA%\EatsVoiceCompanion\Logs
```

Test recordings are stored in:

```text
%TEMP%\EatsVoiceCompanion
```

Airline, snapshot, generated-route, and procedure data are read from this default eATS data directory:

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
7. Review the transcript and generated command. If transcription wording is incorrect, edit the transcript and choose **Interpret again**; a new recording is not required.
8. If an ambiguous-callsign error lists possible active callsigns, correct the transcript rather than selecting a guess blindly.
9. Confirm that the safety message matches the expected active aircraft and, for descend via, its assigned STAR.
10. Edit the preview fields and choose **Build preview** if a correction is needed.
11. Choose **Stage in eATS**, review the confirmation, and approve it only if the command is correct.
12. Inspect the staged text in the lower-left eATS radio-command field and press **Enter** yourself only when it is safe to transmit.

## Known limitations

- Aircraft-type registration callsigns such as `Cessna Three Papa Zulu` are not yet resolved because the active snapshot context currently contains callsigns but not a trusted aircraft-type-to-callsign mapping. Use `November Three Papa Zulu` for a uniquely active abbreviated N-number.
- Named STAR runway transitions are not yet parsed; say only the base procedure name and number, such as `BANKR Five arrival`.
- Descend-via staging requires an unambiguous assigned STAR found in fresh eATS generated-route data and marked with descend-via support in the installed procedure database.
- Published-speed compliance is limited to a full 2-8 character fix identifier and is staged only with descend via in the same preview; the application does not determine whether that fix has a usable charted speed.
- The companion cannot determine whether eATS currently has an approach clearance or another simulator state that independently prevents `EXP`; controllers must verify the simulator response.
- Reduce-to-final-approach-speed (`S-`) is generated only with `CA` earlier in the same preview, so eATS receives the required approach clearance first.
- `CA` clears the approach already present in the eATS pilot route. Because the token contains no approach identifier, named spoken approach clearances are not inferred and the controller must confirm the expected/current approach in eATS before staging `CA`.
- `INTC` is accepted only after an explicit heading in the same preview. This intentionally rejects reliance on an earlier heading that the companion cannot verify from snapshot data.
- Contact-frequency speech must contain three separately spoken digits before “point” and one or two after it. The companion accepts civil VHF frequencies from 118.000 through 136.975 MHz on documented 25 kHz channel endings and emits canonical abbreviated eATS tokens.
- A frequency transfer must be the final token in a preview. Say-again and stand-by responses must be staged by themselves.
- Transponder codes are syntax-validated but are not compared with the aircraft's assigned beacon code in its flight plan; verify every code before staging.
- Contact-tower, advisory-frequency, and oceanic-communications shortcuts are deferred because they depend on facility or approach state the companion cannot yet verify. The eATS `??` communications-buffer reset is also deferred because it has a broader side effect than a normal radio response.
- At-or-above and at-or-below crossing restrictions are not generated because the supplied eATS radio reference does not define equivalent command tokens.
- Cross-distance restrictions are syntax- and order-validated, but the companion does not verify that the named fix is in the aircraft route or that the direction matches the inbound route segment.
- Recognition uses the English `small.en` Whisper model and does not expose confidence scoring. It requires more download space and processing time than the earlier `base.en` model.
- Best-effort recovery improves common transcription errors but cannot guarantee that the intended instruction was understood; the original transcript, recovered wording, preview fields, and staged eATS text must all be reviewed.
- Generated tests cover core interpretation across varied airlines, flight numbers, positions, STARs, and command families, plus file/service integration; actual microphone hardware and WPF interaction still require manual testing.
- Stage-only entry depends on Windows foreground input; it aborts if eATS loses focus, and both applications should run at the same Windows privilege level.
- There is no installer, signed release, or automatic command transmission.

## Project structure

```text
EatsVoiceCompanion.App/     WPF interface, audio, speech recognition, and eATS file/process integration
EatsVoiceCompanion.Core/    Parsing, command formatting, prompt construction, and safety validation
EatsVoiceCompanion.Tests/   Core and application-service integration tests
```

## Development roadmap

- Keep the supported command set stable while expanding real-world microphone and eATS scenario testing
- Support aircraft-type-based general-aviation callsign phraseology
- Add automated WPF interaction tests and hardware-in-the-loop microphone tests
- Add an installer, code signing, and automated tagged releases

## Privacy

Recordings and transcription stay local. The application downloads the speech model on first use, but it does not upload recorded audio or transcripts. It reads eATS airline, snapshot, route-log, and procedure files in place and does not copy or modify them. Saved WAV recordings are deleted on a best-effort basis when they exceed the configured age or count limits.

Diagnostic logs stay local and intentionally omit full transcripts, generated commands, and callsigns. They contain event names, status metadata, and exception types/messages for troubleshooting. Logs older than 14 days are removed on a best-effort basis.

## Independence and legal notice

This is an original, unofficial companion project. It is not affiliated with, endorsed by, or distributed with eATS, ATSimulations, or VICE.

The project does not include eATS executables, documentation, scenarios, data files, branding, or other proprietary material. Users must obtain and install eATS separately and comply with its applicable terms. No source code or other protected implementation from VICE is used in this project.

Product names and trademarks belong to their respective owners and are referenced only to explain compatibility and the project's intended purpose.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

This license applies only to the original source code in this repository. It does not apply to eATS or any third-party software, documentation, data, or trademarks.
