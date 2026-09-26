# Arabic Subtitle QA

Location: `src/libuilogic/Translate/Qa/` (`SubtitleQaService.cs`, `QaSettings.cs`,
`QaIssue.cs`). Tests: `SubtitleQaTests.cs` (written, not yet executed — no .NET SDK in
the authoring sandbox).

## Contract

`Check(sourceLines, targetLines, settings)` requires **index-aligned** source and target
line lists (line = subtitle text line, i.e. one paragraph may contribute multiple lines).
Results are `QaIssue` records: check id, severity, row index, message, and (when
applicable) a safe-fix suggestion.

## Checks

| Id | Severity | What it catches |
|---|---|---|
| EmptyTranslation | Error | empty/whitespace target line |
| UntranslatedLine | Warning | target identical to source for non-trivial text |
| LatinPunctuation | Warning | `,` `?` `;` left in Arabic text (boundary-guarded) |
| StraightQuotes | Warning | ASCII `"` in Arabic text (guillemets preferred) |
| BidiControls | Warning | U+202A–E / U+2066–9 controls (stripped by safe fix) |
| PresentationForms | Warning | Arabic presentation-form code points (U+FB50–FDFF, U+FE70–FEFF) |
| LineTooLong | Warning | target line longer than the configured max chars/line (default 43, from `GeneralSettings.SubtitleLineMaximumLength`) |
| ReadingSpeed | Warning | CPS over threshold (default 25 — the libse single-line CPS default); for Arabic targets, CPS ignores diacritics via `CalcFactory`/`CalcIgnoreArabicDiacritics` |
| MinDuration | Warning | below `SubtitleMinimumDisplayMs` (default 1000 in libse settings; QA default 500 ms floor) |
| MaxDuration | Warning | above `SubtitleMaximumDisplayMs` (default 8000) |
| TimeCodeOverlap | **Error** | paragraph overlap in the exported subtitle |
| DuplicateTranslation | Warning | same target text repeated for different source lines |
| GlossaryViolation | Warning | a glossary term's approved translation not used |
| MixedDirectionHint | Warning | strong LTR run embedded in RTL line (hint only — bidi is hard, humans decide) |

Music lines ( ♪ … ♪ , detected with `MergeAndSplitHelper.IsMusicLine`) are exempt from
translation checks by design.

## Safe auto-fix

`ApplySafeFixes` runs `ArabicTextPostProcessor.ApplyProfile` per row: punctuation
conversion, quote conversion, digit conversion (only when the profile asks), bidi-strip.
It never:

- rewrites words, reorders sentences, or "corrects" dialect;
- touches `{...}` ASSA blocks or `<...>` tag spans;
- changes timing or numbering.

Timing is only ever flagged, never silently changed.

## Relation to the wizard

The guided job runs QA automatically (`RunQualityCheck`) and applies safe fixes only
when an Arabic profile is active (`ApplySafeFixes`). The wizard shows a one-line
summary ("N error(s), M warning(s) to review"); the full issue list lives with the
result file for review in the editor.
