# Safety and limitations

This page contains the operational details intentionally kept out of the main README.

## Operating model

eATS Voice Companion is a voice command-entry assistant, not an autonomous controller. By default, it types a recognized voice command in the eATS radio-command field and presses Enter automatically. The controller can disable automatic Enter in Settings to review staged commands before submitting them. Commands built on the manual Generate Command tab remain stage-only.

Verification only means the required simulator context was available—for example, a callsign was present in a recent snapshot or an assigned STAR matched. It cannot prove that Whisper heard the transmission correctly or that the resulting instruction is operationally appropriate.

For every non-empty recording, the app attempts to select the highest-ranked valid command hypothesis. When recognition is uncertain, that result is deliberately best-effort and may be wrong. If no supported instruction can be recovered, the fallback may be an acknowledgement command for the best matching active callsign. With automatic Enter enabled, these best-effort results are also submitted.

## Validation and staging

- Generated commands must use allowlisted eATS tokens and characters.
- Commands are revalidated immediately before automatic entry.
- Callsign and route verification depend on the freshness and completeness of local eATS files.
- Snapshot refresh races or stale simulator data can still affect a result.
- Existing text in the eATS entry field is cleared before a new command is entered, but simulator focus or timing problems can prevent reliable UI automation.
- The final Enter is sent only after text input completes and eATS is still the foreground window. This does not prove eATS accepted the command or that the command is correct. If an error occurs, check the simulator before retrying because submission may have partially completed.

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
