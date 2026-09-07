# Changelog

All notable changes to eATS Voice Companion are documented in this file.

The project uses semantic versioning. Pre-release builds may change as simulator compatibility and controller workflows are tested.

## [Unreleased]

### Added

- Cobertura code-coverage collection, validation, summaries, and downloadable CI artifacts

### Planned

- Combined spoken instructions
- General-aviation callsign phraseology
- Additional eATS commands
- Explicit stage-only eATS entry with no automatic transmission

## [0.1.0] - 2026-09-06

### Added

- Local hold-to-talk recording and Whisper speech recognition
- Dynamic airline telephony names loaded from the installed eATS `Airlines.txt`
- Active-aircraft speech context and exact callsign verification from `SnapshotAuto.txt`
- Controller-position phrase support
- Heading, altitude, speed, proceed-direct, and acknowledgment previews
- Verified, preview-only, and blocked safety states
- Strict allowlist validation for generated eATS command text
- Persistent controller, microphone, data-path, snapshot, and recording-retention settings
- User-visible model-download and transcription cancellation
- Configurable recording cleanup and privacy-conscious JSON Lines diagnostics
- Whisper model size and SHA-256 integrity validation
- Self-contained Windows x64 packaging and Windows CI

### Fixed

- Aviation digit and altitude wording edge cases
- Recognition of dynamically loaded airline callsigns such as Asiana
- Controller self-identification before supported instructions and acknowledgments
- Snapshot startup loading, refresh, stale-state reporting, and preview revalidation
- A native Whisper crash when transcription was canceled

### Safety

- Commands remain preview-only and are never typed into or transmitted to eATS
- Active callsigns require an exact match against a fresh snapshot for verified status
- Unknown commands, invalid values, control characters, and unsupported output tokens are rejected

[Unreleased]: https://github.com/gagostin1/eATS-Voice-Companion/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/gagostin1/eATS-Voice-Companion/releases/tag/v0.1.0
