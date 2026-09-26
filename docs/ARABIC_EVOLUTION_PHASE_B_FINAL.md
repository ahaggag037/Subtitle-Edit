# ARABIC EVOLUTION — PHASE B FINAL REPORT

Spec: "PHASE B — ARABIC-FIRST PRODUCT EVOLUTION" (40 sections).
Branch: `arena/01a0db10-subtitle-edit`. Base: import of Subtitle Edit v5.3.0-beta12
(commit `13271fc`), Phase A audit `bc237f4` (`docs/ARABIC_EVOLUTION_AUDIT.md`).

## STATUS

**IMPLEMENTED (vertical slice complete, statically verified) — BUILD/TESTS BLOCKED by
environment.** Every compile-shape claim below was checked by reading the referenced
source, not by compiling. No test was ever executed. Nothing in this report should be
read as "it runs"; it should be read as "it is written, cross-checked against the real
APIs it calls, and ready for a compiler".

## BUILD

BLOCKED — no .NET SDK in the sandbox (spec §34 re-check performed twice in Phase A and
once in Phase B: `dotnet` absent). Static verification only.

## TESTS

BLOCKED (cannot run). Written: 6 test files, 1,124 lines
(`tests/libuilogic/Translate/Jobs/`): ArabicProfileTests (+Egyptian variants),
ArabicTextPostProcessorTests, SubtitleQaTests, TranslationMemoryTests (+GlossaryTests),
TranslationJobRunnerTests (incl. the §31 vertical slice). Pre-existing suites untouched
(`git diff 13271fc..HEAD -- tests/` shows only additions under
`tests/libuilogic/Translate/`), so "old tests green" (§32-1) is expected-but-unverified.
All test types are UNIT (in-process, engine faked); there are no INTEGRATION,
END_TO_END or EXPERIMENTAL executions to report, and none are claimed (§24).

## IMPLEMENTED (features and where they live)

| # | Feature (spec §) | Files | Commit |
|---|---|---|---|
| 1 | Arabic translation profiles + profile catalog (§6/§7) | `src/libuilogic/Translate/Arabic/` (ArabicTranslationProfile, ArabicProfileCatalog, ArabicTextPostProcessor) | `f5eb771` |
| 2 | Local translation memory + per-media glossary (§9) | `src/libuilogic/Translate/Memory/` | `3e2310a` |
| 3 | Arabic-aware QA engine, 14 checks + safe auto-fix (§11) | `src/libuilogic/Translate/Qa/` | `9497d89` |
| 4 | Bounded job orchestration: stages, progress, honest ETA, cancellation, checkpoint/resume, error WHAT/WHY/WHAT-TO-DO, SRT export (§4/§5/§20/§21) | `src/libuilogic/Translate/Jobs/` | `1762a16`, fixes `216beda` |
| 5 | §31 vertical slice tests: real SubRip parse → chunking → Arabic policies → QA → TM → checkpoint → SRT re-parse | `tests/libuilogic/Translate/Jobs/` | `84f276a` |
| 6 | Guided beginner wizard (§2/§3/§15): video pick, subtitle/transcript pick, LOCAL/ONLINE engine disclosure, real engine capability lists, MSA + Egyptian targets where engines support them, prompt-only styles, TM toggle, staged run, QA summary, open-in-editor | `src/ui/Features/TranslateVideo/` (ViewModel + Window), `MainViewModel.TranslateVideo.cs`, `InitMenu.cs`, `LanguageMainMenu.cs`, `DependencyInjectionExtensions.cs`, `Program.cs`, English + Arabic language JSON | `47494f0` |
| 7 | Phase B documentation (§36) | `docs/ARABIC_PRODUCT_ARCHITECTURE.md`, `TRANSLATION_MEMORY.md`, `ARABIC_QA.md`, `Egyptian-Arabic.md`, `BEGINNER_WORKFLOW.md` | `5504376` |

## VERIFIED vs NOT RUNTIME-VERIFIED (per-feature classification, §38)

Legend: **I+V** = implemented and verified; **I+NRV** = implemented, not runtime-verified
(static checks only); **PARTIAL**; **DEFERRED**; **BLOCKED**.

- ArabicTranslationProfile / catalog — **I+NRV**. Placeholder-safety of addenda verified
  by source reading (no `{`/`}` outside the `{0}`/`{1}` the engines substitute) and by
  unit test (not executed).
- ArabicTextPostProcessor — **I+NRV**. Idempotence, mostly-Arabic gate, ASSA/tag spans,
  punctuation/quote/digit conversions: implemented + unit-tested; never executed.
- TranslationMemory / Glossary — **I+NRV**. JSON store, normalization (tashkeel/tatweel
  stripping), latest-wins, style scoping, corrupt-tolerant load, empty-folder fallback to
  `DefaultFolderPathProvider` (wired to `Se.DataFolder` in `Program.cs`).
- SubtitleQaService — **I+NRV**. 14 checks; severity policy (overlap = Error, rest =
  Warning); music-line exemption; safe fixes limited to punctuation/quote/bidi/digits —
  never semantic content (§11 honored by construction).
- TranslationJobRunner — **I+NRV**. Chunking; source text to engine / target text to QA
  and export (the classic apply-back bug is guarded in code and by test); checkpoint
  `<output>.job.json` schema v1, corrupt-tolerant; resume via `FromState`; ETA only after
  ≥0.5 s with ≥2 progressing samples and finite rate; error mapping incl. cancellation.
  Credentials never serialized into job state (§19).
- §31 vertical slice — **I+NRV, with an honest scope note**: the orchestration chain is
  real (SubRip parse in, chunk boundaries, profile/QA/TM services, SubRip re-parse out),
  but the translation engine in the test is an inline `FakeJobEngine : IAutoTranslator`
  double. No online or local LLM engine was exercised end-to-end — impossible in this
  environment (no network, no SDK). This is exactly the IMPLEMENTED+NOT
  RUNTIME-VERIFIED case, not IMPLEMENTED+VERIFIED.
- Guided wizard UI — **I+NRV** (weakest verification area, stated plainly): every
  referenced member was cross-checked against its declaring source (engine class names
  and namespaces, `GetSupportedTargetLanguages`, `Configuration.Settings.Tools.*Prompt`
  — all 15 prompt fields verified in `ToolsSettings.cs`, `Se.Settings.AutoTranslate.
  LlamaCppAdvanced.Prompt/Glossary`, `MessageBox.Show(Window,...)` signature, both
  `NativePickers.OpenFilePickerAsync(TopLevel, FilePickerOpenOptions)` idiom,
  `WindowsService.ShowDialogAsync` creating the window via
  `Activator.CreateInstance(typeof(TWindow), viewModel)`, every `UiUtil.Make*` signature
  used, every bound property/command existing on the VM). But the window has never been
  rendered, no RTL text was ever visually checked, and no click-path was ever walked.
  Per §24: **no UI workflow is claimed tested.**
- Menu + language wiring — **I+NRV**. `Translate ▸ Translate video (guided)` above
  Auto-translate; new `TranslateVideo` string with default in `LanguageMainMenu` ctor +
  English.json + Arabic.json; the other 33 language files fall back to the English
  default string (standard SE behavior; JSON parse re-validated for all 35).
- Transcribe stage inside the wizard — **PARTIAL**. The wizard detects a missing
  transcript and directs the user to the existing Video ▸ Speech to text window; it does
  not drive ASR itself. Reason: ASR stays a mature subsystem (§1), and a headless ASR
  invocation would duplicate the download-disclosure flow (§17) in a second place
  without any way to test it here. The 8-step ideal flow is therefore 7 steps + an
  honest handoff.
- Side-by-side review (§14) — **PARTIAL**: result review today = wizard summary +
  "Open result in editor" (full editor, RTL mode, Fix common errors, burn-in all
  reachable). A dedicated original/translation sync-scroll view with diff and
  terminology highlighting is not built. Triple-view deferred entirely.
- Back-translation (§12) — **DEFERRED** (flag-don't-rewrite stance kept; O2 in the
  audit's idea list).
- Batch reuse (§18) — **PARTIAL/DEFERRED**: checkpoint/resume machinery exists and is
  unit-tested at the job level; no batch-queue UI integration this phase.
- Speaker styles (§10) — **DEFERRED**: requires diarization metadata that SE's ASR layer
  does not currently emit (audit idea E5 = arch change).
- Download disclosure (§17) — **I+NRV for the wizard's own path** (it never downloads
  anything and says so); ASR downloads keep the existing per-engine disclosure windows.
- RTL flags audit (§22) — **DONE (audit only, no consolidation)**. Findings:
  `Se.Settings.Appearance.RightToLeft` (UI mirroring, toggled by the menu command) and
  `Configuration.Settings.General.RightToLeftMode` (write-side flag read only by
  `Cavena890.cs:955`) are the two overlapping flags. Consolidation was considered and
  **rejected**: mapping one to the other would silently change legacy format output
  (Cavena 890 visual reversal) whenever a user mirrors the editor UI — a behavior change
  in a mature format path with no way to regression-test here (§1 violation). Recorded
  as recommended upstream follow-up instead.
- Fonts (§23) — **DONE (policy)**: no fonts bundled; system fonts only; no license
  exposure added.
- Performance work (§26) — **DEFERRED**: no before/after evidence possible without a
  runtime; no perf claims made anywhere.

## SAFETY (§1)

- Zero modifications to `src/libse/` (verified: `git diff 13271fc..HEAD -- src/libse` is
  empty). Zero modifications to `MainViewModel.cs` (the new command lives in a new
  partial file), `IAutoTranslator`, `AdvancedTranslatorBase`, ASR, burn-in, RTL stack,
  existing tests, and expert workflows (Auto-translate window untouched).
- All additions are additive layers behind new namespaces; every existing default
  behavior is untouched. The only shared-file edits are one menu item, one DI
  registration, one Program.cs provider hook, and one language string — each trivially
  reviewable and reversible.

## LICENSE (§28)

- No new dependency was added; no model weights, corpora or fonts were bundled.
- Standing flags from Phase A remain: NLLB-200 (CC-BY-NC) and MADAR must never be
  bundled; WhisperX license claim (BSD-2 vs BSD-4 conflict in third-party sources) is
  unresolved — verify the repo LICENSE before any bundling claim; llama.cpp (MIT),
  faster-whisper (MIT), TranslateGemma (Gemma terms, commercial-OK with pass-through)
  documented in the audit.
- The two language JSON edits contain original UI strings — no translation was copied
  from a licensed source.

## ARABIC (actual current limitations, per standing epistemics)

- ACTUAL CURRENT LIMITATION: translation quality/dialect fidelity depends on the chosen
  model; small local models often answer MSA even when the Egyptian addendum asks for
  colloquial. The profile makes the request and the post-processing deterministic, but
  the "Egyptian-ness" is a model capability, not a string transform.
- ACTUAL CURRENT LIMITATION: ASR for Egyptian Arabic is the weak link (published WER
  figures are far higher than MSA; see audit §26 for sourced numbers — none invented
  here).
- POSSIBLE FUTURE IMPROVEMENT: dedicated side-by-side review window, speaker-conditioned
  translation, "Egyptianify" one-click action, batch queue UI, resume button in the
  wizard.

## KEY RISKS

1. **Nothing has compiled.** The largest risk is ordinary compile errors across ~5,767
   added lines despite the systematic API cross-checks (list of checked seams in the
   wizard bullet above; the same method was applied to Jobs/Qa/Memory/Arabic against
   libse and DoAutoTranslate/TranslateRow).
2. Test *expectations* may not all match runtime behavior (e.g., chunk-boundary merge
   heuristics, ETA edge values) — the vertical slice asserts stage sequence and re-parse
   equality, which is the best static proxy available, not proof.
3. The wizard is modal; a long translation blocks the main window while it runs (same as
   the existing Auto-translate window — consistent, but worth knowing).
4. `TranslationMemoryFolder = string.Empty` semantics (use default) rely on the
   empty-string fallback added in this phase — covered by intent, not by an executed
   test.

## FINAL RECOMMENDATION

Merge-worthy as an additive feature branch **contingent on a compiled environment**:
first action in any SDK-enabled environment is `dotnet build` + `dotnet test
tests/libuilogic`, then walk the wizard once with a local llama.cpp/Ollama engine and
once with a real SRT, before any user-facing release. Do not advertise the guided flow
in release notes until that walk has happened. The §31 vertical-slice claim to make in
reviews is precisely: *orchestration proven by unit-level composition, engine path
pending one live run.*
