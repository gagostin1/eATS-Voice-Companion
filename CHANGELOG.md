# Changelog

All notable changes to eATS Voice Companion are documented in this file.

The project uses semantic versioning. Pre-release builds may change as simulator compatibility and controller workflows are tested.

## [Unreleased]

### Added

- Cobertura code-coverage collection, validation, summaries, and downloadable CI artifacts
- Explicit stage-only entry of freshly verified commands into the eATS radio-command field, with foreground/process checks and no final Enter key
- Ordered combined-instruction parsing with whole-preview allowlist validation
- Crossing-altitude, combined crossing-altitude/speed, altimeter, descend-via, and descend-via-except-maintain commands
- Route-aware named STAR recognition and fail-closed descend-via staging using fresh eATS generated-route and procedure data
- Clearly labeled, constrained best-effort recovery for common callsign, controller-position, command-phrase, and assigned-STAR transcription errors
- Beam-search speech decoding for improved short ATC transmission recognition
- The higher-accuracy Whisper `small.en` model replaces `base.en` for callsign and ATC phrase recognition
- A command catalog documenting supported mappings, safety constraints, and planned command groups
- Editable transcripts with an **Interpret again** workflow that does not require rerecording
- Ambiguous active-callsign suggestions that remain fail-closed until the controller corrects the transcript
- A generated, scenario-independent recognition matrix covering varied airlines, flight numbers, controller positions, STARs, command families, and recovery cases through the production interpreter
- Published-speed compliance at a named fix using canonical `CWS@FIX` output, descend-via context enforcement, safe ordering, speech parsing, and manual preview support
- Pilot's-discretion descent, expedite-current, expedite-through/to-altitude, report-leaving/reaching-altitude, and say-altitude commands with speech, editor, allowlist, ordering, and scenario-matrix coverage
- Assigned speed and Mach exact/greater/less commands, resume-normal-speed, and indicated-speed/Mach report commands with speech, editor, allowlist, and scenario-matrix coverage
- Fly-present-heading, expect-approach, intercept-final, cleared-approach, final-approach-speed, and say-approach-request commands with approach-ID normalization, conservative ordering rules, editor support, and scenario-matrix coverage
- Civil VHF contact-frequency, remain-this-frequency, say-again, and stand-by commands with FAA digit parsing, canonical eATS frequency abbreviation, conservative ordering, editor support, and scenario-matrix coverage
- Full U.S. N-number callsigns with aviation digit/phonetic parsing, active-aircraft Whisper prompting, mixed-traffic scenario coverage, and snapshot-unique final-three abbreviation recovery
- Transponder code, IDENT, altitude-reporting, normal, standby, and VFR commands with octal validation, conflict checks, speech/editor support, and scenario-matrix coverage
- Cross-distance restrictions with eight-point direction normalization, conservative route-change ordering, speech/editor support, and scenario-matrix coverage

### Fixed

- Airline entries with trailing flight-number ranges, including Blue Streak, Endeavor, Piedmont, Brickyard, and SkyWest, are now loaded from `Airlines.txt`
- Stale snapshots retain non-authoritative callsign context for best-effort preview recovery while staging remains disabled

### Planned

- Broader real-world microphone, mixed-traffic, and eATS scenario testing

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
