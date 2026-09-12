# Changelog

All notable changes to eATS Voice Companion are documented in this file.

The project uses semantic versioning. Pre-release builds may change as simulator compatibility and controller workflows are tested.

## [Unreleased]

## [0.2.0] - 2026-09-12

### Added

- Ranked, scenario-constrained command hypotheses that automatically stage the highest-ranked valid command; non-empty speech with active-aircraft context always falls back to a valid command for the best-matched aircraft
- Best-hypothesis recovery for close active flight numbers, dropped flight-level wording, and individually spoken waypoint characters
- A configurable non-exclusive system-wide hold-to-record hotkey, including modifier-only assignments, Settings capture, persistence, key-repeat protection, TeamSpeak passthrough, and an on-screen recording fallback
- A modern three-tab interface that prioritizes voice operation while separating manual command generation and connection/audio settings
- Optional automatic staging of freshly verified voice and manual commands, while retaining the controller's final Enter action
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
- Deterministic best-match selection for ambiguous active callsigns, instructions, and incomplete commands
- A generated, scenario-independent recognition matrix covering varied airlines, flight numbers, controller positions, STARs, command families, and recovery cases through the production interpreter
- Published-speed compliance at a named fix using canonical `CWS@FIX` output, descend-via context enforcement, safe ordering, speech parsing, and manual preview support
- Pilot's-discretion descent, expedite-current, expedite-through/to-altitude, report-leaving/reaching-altitude, and say-altitude commands with speech, editor, allowlist, ordering, and scenario-matrix coverage
- Assigned speed and Mach exact/greater/less commands, resume-normal-speed, and indicated-speed/Mach report commands with speech, editor, allowlist, and scenario-matrix coverage
- Fly-present-heading, expect-approach, intercept-final, cleared-approach, final-approach-speed, and say-approach-request commands with approach-ID normalization, conservative ordering rules, editor support, and scenario-matrix coverage
- Civil VHF contact-frequency, remain-this-frequency, say-again, and stand-by commands with FAA digit parsing, canonical eATS frequency abbreviation, conservative ordering, editor support, and scenario-matrix coverage
- Full U.S. N-number callsigns with aviation digit/phonetic parsing, active-aircraft Whisper prompting, mixed-traffic scenario coverage, and snapshot-unique final-three abbreviation recovery
- Transponder code, IDENT, altitude-reporting, normal, standby, and VFR commands with octal validation, conflict checks, speech/editor support, and scenario-matrix coverage
- Cross-distance restrictions with eight-point direction normalization, conservative route-change ordering, speech/editor support, and scenario-matrix coverage
- Aircraft-specific flight-plan and STAR fix context for Whisper prompting and unique correction of misheard direct/crossing fixes

### Fixed

- Bare altimeter readouts now use the configured controller facility, and valid altimeters following another instruction remain in the combined staged command
- Staging explicitly focuses the lower eATS radio field before selecting and deleting existing text, preventing commands from being appended or an active upper-right data-block prompt from receiving the new command
- Live-context recovery now handles compact airline callsigns such as `AMERICAN1401`, common N-number transcriptions such as `Julia Charley`, `foot level`, and assigned STAR spellings such as `JONES Z5` for `JONZE5`
- Voice results keep a compact, stable layout instead of expanding the page with recording paths and repeated recovery/staging text
- Settings are vertically grouped beneath a persistent global save bar that clearly applies to every settings section
- Airline entries with trailing flight-number ranges, including Blue Streak, Endeavor, Piedmont, Brickyard, and SkyWest, are now loaded from `Airlines.txt`
- Stale snapshots retain non-authoritative callsign context for best-effort voice selection and staging
- Mixed word/digit altitude transcripts such as `one 3,000` are normalized without accepting ambiguous cardinal wording
- `cleared direct` and common `clear direct` transcriptions are recognized as proceed-direct instructions

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

[Unreleased]: https://github.com/gagostin1/eATS-Voice-Companion/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/gagostin1/eATS-Voice-Companion/releases/tag/v0.2.0
[0.1.0]: https://github.com/gagostin1/eATS-Voice-Companion/releases/tag/v0.1.0
