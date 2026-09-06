# eATS Voice Companion

eATS Voice Companion is an independent Windows application intended to add voice-command support to the eATS enroute air traffic control simulator.

The long-term goal is a push-to-talk workflow that records a controller instruction, converts it to text, validates and formats it as an eATS command, presents it for confirmation, and then safely enters it into eATS.

> [!IMPORTANT]
> This project is currently an early prototype. It can detect a running eATS instance, discover microphones, record WAV audio, and build command previews. It does **not** currently perform speech recognition or transmit commands to eATS.

## Current features

- Detects a running eATS process without modifying it
- Lists available Windows recording devices
- Records push-to-talk audio as 16 kHz, 16-bit, mono WAV files
- Builds and validates previews for several common eATS instructions
- Keeps command generation separate from the Windows interface
- Includes automated tests for command formatting and validation

Currently supported command previews include:

- Fly heading
- Turn left heading
- Turn right heading
- Climb and maintain
- Descend and maintain
- Maintain speed
- Proceed direct to a fix

For example, the spoken intent “Delta one twenty-three, turn left heading two seven zero” is ultimately expected to produce:

```text
DAL123 TLH270
```

## Requirements

- Windows 10 or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A microphone recognized by Windows
- eATS installed separately for process-detection testing
- Git and Visual Studio Code, Visual Studio, or another C# editor for development

The project currently uses [NAudio 2.3.0](https://www.nuget.org/packages/NAudio/2.3.0) for Windows audio-device discovery and recording.

## Getting started

Clone the repository and enter its directory:

```powershell
git clone [<repository-url>](https://github.com/gagostin1/eATS-Voice-Companion.git)
cd eATS-Voice-Companion
```

Restore dependencies and build the solution:

```powershell
dotnet restore
dotnet build
```

Run the automated tests:

```powershell
dotnet test
```

Start the application:

```powershell
dotnet run --project .\EatsVoiceCompanion.App\EatsVoiceCompanion.App.csproj
```

## Testing the current prototype

1. Start eATS and wait for its main window to appear.
2. Start eATS Voice Companion.
3. Confirm that the intended microphone is selected.
4. Hold **Hold to record**, speak a short instruction, and release the button.
5. Open the saved WAV file and confirm that the recording is clear.
6. Select an instruction, enter a callsign and value, and choose **Build preview**.
7. Select **Detect eATS** and confirm that the running process is found.

Test recordings are stored outside the repository in:

```text
%TEMP%\EatsVoiceCompanion
```

The recordings remain local and are not automatically uploaded or transmitted.

## Project structure

```text
EatsVoiceCompanion.App/     WPF user interface and Windows integration
EatsVoiceCompanion.Core/    Command formatting and validation logic
EatsVoiceCompanion.Tests/   Automated tests for the core logic
```

Keeping the core command logic independent of the interface makes it easier to test speech interpretation before allowing any interaction with eATS.

## Planned development

- Convert recorded audio to text
- Normalize aviation phraseology, callsigns, and spoken numbers
- Parse transcripts into structured controller instructions
- Display recognition confidence and require confirmation when uncertain
- Add a dry-run mode for end-to-end testing
- Enter confirmed commands through a narrowly scoped Windows integration layer
- Add logging, configuration, packaging, and release documentation

Command transmission will remain disabled until recognition, parsing, validation, focus handling, and failure behavior have been tested independently.

## Independence and legal notice

This is an original, unofficial companion project. It is not affiliated with, endorsed by, or distributed with eATS, ATSimulations, or VICE.

The project does not include eATS executables, documentation, scenarios, data files, branding, or other proprietary material. Users must obtain and install eATS separately and comply with its applicable terms. No source code or other protected implementation from VICE is used in this project.

Product names and trademarks belong to their respective owners and are referenced only to explain compatibility and the project’s intended purpose.

## License

This project is licensed under the MIT License. See `LICENSE` for details.

This license applies only to the original source code in this repository. It does not apply to eATS or any third-party software, documentation, data, or trademarks.
