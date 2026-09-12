# Safety and limitations

This page contains the operational details intentionally kept out of the main README.

## Operating model

eATS Voice Companion is a command-entry assistant, not an autonomous controller. It stages text in the eATS radio-command field but never presses the final Enter key. The controller is responsible for reviewing and transmitting every command.

Verification only means the required simulator context was available—for example, a callsign was present in a recent snapshot or an assigned STAR matched. It cannot prove that Whisper heard the transmission correctly or that the resulting instruction is operationally appropriate.

For every non-empty recording, the app attempts to select and stage the highest-ranked valid command hypothesis. When recognition is uncertain, that result is deliberately best-effort and may be wrong. If no supported instruction can be recovered, the fallback may be an acknowledgement command for the best matching active callsign.

## Validation and staging

- Generated commands must use allowlisted eATS tokens and characters.
- Commands are revalidated immediately before automatic staging.
- Callsign and route verification depend on the freshness and completeness of local eATS files.
- Snapshot refresh races or stale simulator data can still affect a result.
- Existing text in the eATS entry field is cleared before a new staged command is entered, but simulator focus or timing problems can prevent reliable UI automation.
- The app does not submit the command; the final Enter key always remains manual.

## Recognition and context limitations

- Background noise, clipped push-to-talk audio, accents, speech rate, and microphone quality affect transcription.
- Airline names, similar callsigns, phonetic letters, fixes, STARs, altitudes, and altimeter values can remain ambiguous.
- Context comes from the configured eATS installation and its current snapshot, log, airway, airline, and flight-plan data. Missing or outdated files reduce accuracy.
- Only a bounded set of relevant callsigns, fixes, and procedures is added to the Whisper prompt; the model is not retrained on simulator data.
- Best-effort ranking improves coverage but does not guarantee the spoken command was selected.

## Command limitations

- Only commands listed in the [command catalog](COMMAND_CATALOG.md) are intentionally supported.
- Unsupported phraseology may be mapped to the closest supported command or to the fallback acknowledgement.
- Clearances, aircraft-type callsigns, and holding instructions are outside the current intended scope.
- Complex or unusual combined instructions may need transcript correction or manual command generation.
- Simulator command syntax can differ from natural ATC phraseology; the catalog documents the translation used by the app.

## Distribution limitations

- The packaged release targets 64-bit Windows.
- The first transcription requires downloading the configured Whisper model.
- Windows security software may warn about an unsigned, independently distributed executable.
- eATS integration relies on local files and Windows UI automation and may need updates when the simulator changes.

Please use the dedicated forms to [report a bug](https://github.com/gagostin1/eATS-Voice-Companion/issues/new?template=bug_report.yml) or [report a speech-recognition problem](https://github.com/gagostin1/eATS-Voice-Companion/issues/new?template=speech_recognition.yml).
