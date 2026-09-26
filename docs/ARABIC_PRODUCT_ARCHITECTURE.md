# Arabic-First Product Architecture (Phase B)

Status: design + implementation notes for the Phase B "Arabic-first product evolution".
Everything here describes code that exists in this repository. Where something is design
intent rather than verified behavior, it is labeled. Build/runtime verification is
**BLOCKED** in the authoring environment (no .NET SDK, no outbound network); all
verification so far is static (source reading + structure checks). See
`ARABIC_EVOLUTION_PHASE_B_FINAL.md` for the per-feature classification.

## 1. Goals

Evolve the existing Subtitle Edit into an Arabic-first product without rewriting any
mature subsystem:

```
VIDEO --> TRANSCRIBE --> TRANSLATE --> ARABIC/EGYPTIAN --> QA --> REVIEW --> EXPORT SRT / VIDEO
```

- Primary user: a complete beginner (the guided "Translate video" path).
- Expert users: keep every existing workflow unchanged (Auto-translate, Batch convert,
  Speech-to-text, Fix common errors, Burn-in, ...).

## 2. Layering (what was added, where)

All new logic lives in additive layers; nothing under `src/libse/` was modified, and no
existing engine, ASR, burn-in or RTL code path was rewritten (spec §1).

```
src/libuilogic/Translate/
  Arabic/    ArabicTranslationProfile, ArabicProfileCatalog, ArabicTextPostProcessor
  Memory/    TranslationMemory, TranslationMemoryEntry, Glossary (JSON stores)
  Qa/        SubtitleQaService, QaSettings, QaIssue
  Jobs/      TranslationJobStage/Options/Progress/State/JobErrorInfo/TranslationJobRunner

src/ui/Features/TranslateVideo/
  TranslateVideoViewModel.cs   guided wizard (beginner path)
  TranslateVideoWindow.cs      declarative window (same idiom as other SE windows)
```

Wiring:

- `src/ui/Features/Main/MainViewModel.TranslateVideo.cs` — a new partial file holding only
  the `ShowTranslateVideo` command; `MainViewModel.cs` itself is untouched.
- `src/ui/Features/Main/Layout/InitMenu.cs` — "Translate video (guided)" item above
  Auto-translate in the Translate submenu (`vm.ShowTranslateVideoCommand`).
- `src/ui/DependencyInjectionExtensions.cs` — `TranslateVideoViewModel` registered.
- `src/ui/Program.cs` — `TranslationMemory.DefaultFolderPathProvider = () => Se.DataFolder`.

## 3. Translation profiles (spec §6/§7)

`ArabicTranslationProfile` is **data, not code paths**: punctuation conversion, numeral
system, quote style and a prompt addendum are profile properties. Engines stay generic.

- Modern Standard Arabic profile: target code `ar`, Arabic-Indic digits off by default
  in the profile addendum (digit policy is applied by the post-processor only where the
  profile asks for it).
- Egyptian Arabic profile: target code `arz`, colloquial addendum ("use natural Egyptian
  colloquial Arabic as spoken in daily life ...").
- The addenda intentionally contain **no** `{`/`}` characters: LLM engines substitute
  `{0}`/`{1}` placeholders into prompts (`string.Format` in classic engines, `.Replace()`
  in the advanced engines), and stray braces would throw or corrupt the prompt.
- Dialect naturalness is bounded by model capability; the profile cannot make a model
  speak Egyptian, it only instructs and post-processes. This is an explicit current
  limitation, not a missing UI.

## 4. Post-processing (Arabic text policies)

`ArabicTextPostProcessor` (pure functions, unit-tested):

- Gated by a "mostly Arabic" check (≥ 3 Arabic letters and Arabic ≥ Latin), so English
  lines and code-like lines pass through untouched.
- Skips `{...}` ASSA override blocks and `<...>` tag spans.
- Boundary-guarded punctuation conversion `? → ؟`, `, → ،`, `; → ؛`; straight quotes to
  guillemets «»; digit conversion 0-9 ↔ ٠-٩ per profile.
- Strips bidi controls (U+202A–U+202E, U+2066–U+2069); detects presentation forms
  (U+FB50–FDFF, U+FE70–FEFF) as QA findings, never rewrites them silently.
- Idempotent: applying it twice equals applying it once.

## 5. Job orchestration (spec §4/§5/§20)

`TranslationJobRunner` owns chunking, progress, cancellation, checkpoint/resume, TM
record-back, QA and export:

- Stage sequence: `Queued → Preparing → Transcribing* → Translating → QualityChecking →
  Reviewing* → Exporting → Completed | Failed | Cancelled` (* = stages the runner passes
  through when driven by a larger flow; the wizard-driven job currently starts at
  Preparing because ASR stays in the existing Speech-to-text window).
- Progress is row-based; ETA is only computed after ≥ 0.5 s with at least two progressing
  samples and a finite rate, otherwise `EstimatedSecondsRemaining` stays `null` (spec §20:
  no fake estimates).
- Checkpoints: `<output>.job.json` after every chunk and on cancel/fail (state schema
  v1; corrupt or wrong-version files load as `null`). Resume is
  `TranslationJobRunner.FromState(state, translator)`.
- Credential policy (spec §19): checkpoints persist rows, stage, options and paths —
  **never** API keys or headers. Keys stay in `Configuration.Settings` / `Se.Settings`,
  which are never serialized into job state.
- Errors are mapped to WHAT happened / WHY / WHAT you can do (+ technical details) by
  `JobErrorInfo.FromException` (cancellation, connection refused, offline, 401/403,
  404, empty translations, generic).

## 6. Guided wizard (spec §2/§3/§15)

`TranslateVideoViewModel` implements the 8-step flow in one small window:

1. Choose the video (native file picker; a sibling `.srt` is picked up automatically).
2. Choose the subtitle/transcript (a `.txt` transcript is imported with even timing
   spread). If the project has no transcript yet, the dialog explains the one-time path
   via *Video ▸ Speech to text* — the wizard does not hide a silent ASR download.
3. Engine + privacy: each entry carries an honest `LOCAL` / `ONLINE` / `LOCAL/ONLINE`
   label and one sentence about what data leaves the machine (spec §17 disclosure).
4. Target language: rebuilt from the selected engine's **real**
   `GetSupportedTargetLanguages()`. Arabic appears only if the engine offers it; when it
   does, one Modern Standard and one Egyptian entry (via `ArabicProfileCatalog`) are
   shown — no invented engine capabilities.
5. Optional style (prompt-only; disabled for phrase-based engines).
6. Optional translation-memory reuse.
7. Run: staged progress, honest ETA, cancel.
8. Result: SRT path, QA summary, "Open result in editor".

Advanced users keep the full Auto-translate window; the wizard adds no engine options
beyond a prompt addendum and restores the user's prompt when done
(`EnginePromptSnapshot`).

## 7. QA and review (spec §11/§14)

`SubtitleQaService` runs 14 checks (see `ARABIC_QA.md`). The wizard shows the summary;
safe auto-fixes are applied only for Arabic-profile jobs, and only punctuation/quote/
bidi-level rewrites — never semantic content. Full side-by-side review remains the
editor (open result) plus the existing translate-diff surfaces; a dedicated triple-view
window is future work, listed as PARTIAL in the final report.

## 8. What is intentionally NOT here

- No model/weights bundling (license policy, spec §28).
- No new ASR; transcription stays in the existing Speech-to-text flow (spec §1).
- No silent local→online fallback: engine selection is explicit and labeled.
- No back-translation rewrite loop in this phase (experimental feature stays
  flag-gated and non-destructive).
