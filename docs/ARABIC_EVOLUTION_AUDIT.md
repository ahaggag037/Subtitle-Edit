# ARABIC EVOLUTION AUDIT — Subtitle-Edit (ahaggag037/Subtitle-Edit)

**Audit type:** Read-only forensic architecture/product audit (Phase A).
**Date of execution:** 2026-09-26 (Africa/Cairo).
**Auditor:** AI agent on Arena.ai Agent Mode, session branch `arena/01a0db10-subtitle-edit`.
**Scope:** Architectural, code-path, UX, translation, Arabic-language, performance, reliability, testing, security, and product-design analysis in service of a future "English video → Arabic / Egyptian-Arabic subtitles" product evolution.
**Artifact policy compliance:** No source file was modified, no configuration/dependency changed, no implementation committed, no PR created. The only file created by this audit is this report (`docs/ARABIC_EVOLUTION_AUDIT.md`), committed alone (commit content limited to this file).

**Evidence labels used throughout:**
- REPOSITORY_FACT — verified directly in this repository (path + symbol given).
- EXTERNAL_EVIDENCE — verified against an external source on 2026-09-26 (source + date given).
- INFERENCE — reasoned conclusion from repository facts, labeled as such.
- RECOMMENDATION — proposed action, not a fact.
- HYPOTHESIS — untested claim that would need measurement/prototype.
- UNKNOWN / LIMITED — could not be verified; reason given.

---

## Table of Contents

1. Executive Summary
2. Audit Baseline
3. Repository Inventory
4. Repository Instruction Hierarchy
5. Architecture Map
6. Dependency Map
7. Coverage Ledger
8. Core Data Model
9. End-to-End Workflow Traces
10. Translation Architecture Audit
11. Arabic-First Audit
12. Egyptian Arabic Architecture
13. Beginner UX Audit
14. Proposed Modern UX
15. Local-First / Low-Bandwidth Audit
16. Performance Audit
17. Reliability Audit
18. Testing Audit
19. Security / Privacy / Network Audit
20. License / Dependency Audit
21. External Research
22. Project/Competitor Comparison
23. General Improvements
24. Arabic Improvements
25. Egyptian Arabic Improvements
26. UX Improvements
27. Performance/Reliability Improvements
28. Out-of-the-Box Ideas
29. Preserve/Change/Experiment Matrix
30. Target Architecture
31. Priority Matrix
32. Recommended Roadmap
33. Self-Challenge / Open Questions
34. Audit Limitations

---

# 1. Executive Summary

**What this repository is (REPOSITORY_FACT).** A full import of the Subtitle Edit 5 codebase — a cross-platform (Windows/Linux/macOS) subtitle editor written in C# on .NET 10 with an Avalonia 12 declarative-C# UI, organized as one solution with four shipping projects (`src/ui`, `src/libse` core engine, `src/libuilogic` app-level logic, `src/seconv` CLI) plus five test projects and a BenchmarkDotNet suite. ~662k lines of shipping C# across 2,701 source files; 1,022 test files (~155k lines); 429 `.cs` files under `SubtitleFormats/` (397 of which match `: SubtitleFormat` by grep — see §3 for the count discrepancy discussion).

**Why it matters for an Arabic-first product (REPOSITORY_FACT).** The hard part of "English video → Arabic subtitles" is already substantially built and battle-tested here: 30+ speech-to-text engines including local Whisper variants (whisper.cpp, Const-me, Faster-Whisper-XXL, CTranslate2, WhisperX) and a self-hosted "Crisp ASR" runtime (20+ model families, incl. one exposing Egyptian Arabic `arz_Arab`); **36 translation backends** behind one interface, including 8+ local-LLM paths (llama.cpp with a self-managed `llama-server`, Ollama, LM Studio, KoboldCpp, OpenAI-compatible); a batch/context translation protocol with rolling history, synopsis, glossary, formality and schema-forced JSON replies (`AdvancedTranslatorBase`); burn-in to video via ffmpeg/libass; batch conversion; and real RTL infrastructure (243 `RightToLeft` matches across 30 files, Arabic-aware CPS calculators, RTL waveform text shaping with tests, RTL mode with grid mirroring).

**The gap (REPOSITORY_FACT + INFERENCE).** There is **no end-to-end guided pipeline**: the pieces (Video → Speech-to-Text → Auto-translate → Burn-in) exist as separate windows with separate option surfaces. A beginner cannot reasonably discover this chain; verified pain points in §13 include engine/model/engine-download jargon, cloud-vs-local ambiguity, MSA-vs-dialect absence in the UI (Egyptian Arabic exists only as the raw language code `arz` in three engines' lists), and no progress-visible "one job" mental model. There is **no translation memory, no style profiles, no per-speaker/character conditioning, no dialect-aware QA**, and the only dialect-relevant controls today are a free-text prompt, synopsis, and glossary in the llama.cpp-advanced settings.

**Arabic-specific technical reality (REPOSITORY_FACT + EXTERNAL_EVIDENCE).** ASR is the weakest link for Egyptian Arabic: published benchmarks show whisper-large-v3 at ~59% WER on Egyptian dialect vs ~28% on MSA (arXiv 2412.13788, Dec 2024); LLM post-correction measurably helps dialectal Arabic (IU journal study, 2026). The editor side (RTL editing, bidi rendering, Arabic diacritic-aware CPS) is strong; the *dialect* side (MSA vs Egyptian as a first-class style target) is absent.

**Highest-leverage evolution (RECOMMENDATION, grounded in §29–§32).** (1) A "Translate Video" guided pipeline that orchestrates the *existing* STT → translate → QA → export/burn-in services with a job object, progress, and resume; (2) an Arabic/Egyptian *style layer* (style profile + glossary + TM) injected into the already-existing `SeLlamaCppAdvanced` settings and `LlamaCppAdvancedProtocol.BuildSystemPrompt` — the natural extension point; (3) honest local/online labeling and model size/disk previews (partial infrastructure exists: `DownloadSizeText`, `DownloadHashManager`, resumable `DownloadHelper`); (4) Arabic-aware text QA (diacritic-tolerant search, alef/ya normalization, bidi tag hygiene on export).

**Biggest risks (INFERENCE).** The 34,977-line `MainViewModel.cs` concentrates orchestration logic that every pipeline change touches; the AI-contributor rules in `.github/skills/.../SKILL.md` mandate surgical changes, so new abstractions must be additive and behind new files/services, not refactors of `MainViewModel`. `dotnet` SDK is not available in this sandbox, so **no build/test/benchmark evidence was produced by this audit**; all performance claims are labeled accordingly.

---

# 2. Audit Baseline

All commands were read-only (`git remote/log/status/ls-tree/show/diff`, `find`, `grep`, `wc`, `cat`). No tracked file was modified; no build was run inside the repository.

| Item | Value | Evidence |
|---|---|---|
| Repository | `ahaggag037/Subtitle-Edit` | `git remote -v` → `https://github.com/ahaggag037/Subtitle-Edit.git` |
| Current branch | `arena/01a0db10-subtitle-edit` | `git branch --show-current` |
| HEAD commit | `13271fcd999665d57458aff12a7879e37173264b` — "Import original Subtitle Edit source for independent development" (ahaggag037, 2026-09-26 02:54 +0300) | `git log -1` |
| History depth | 1 commit; **no tags** | `git log --oneline`, `git tag` (empty) |
| Working tree | clean, no local modifications at audit start | `git status --short --branch` |
| Tracked files | 4,455 | `git ls-tree -r HEAD \| wc -l` |
| Top-level entries | 17 (16 dirs/files + `.github`) | `git ls-tree --name-only HEAD` |
| Solution / projects | 1 solution (`SubtitleEdit.sln`, VS Format 12.00), 9 `.csproj` (4 src + 5 tests/benchmarks) | `git show HEAD:SubtitleEdit.sln`, `git ls-tree -r HEAD \| grep csproj` |
| .NET SDK in sandbox | **NOT INSTALLED** — `dotnet: command not found` | `dotnet --list-sdks` / `--list-runtimes` / `--version` all fail |
| Consequence | No `dotnet build/test/benchmark` evidence exists in this audit; all performance claims are code-inspection or labeled architectural inference | this section |
| OS of sandbox | Linux 6.1.158 x86_64 (Debian-based container) | `uname -a` |
| Version/release info in repo | No `git tags`; app version not found as a single constant in audited files (`UI.csproj` has no `<Version>`; external release info in §21 shows upstream v5.2.0 line as of Aug–Sep 2026) | REPOSITORY_FACT + EXTERNAL_EVIDENCE |
| License | MIT, "Copyright (c) 2026 Nikolaj Olsson" (root `LICENSE`; `src/libse/LICENSE.txt` same) | `head LICENSE` |

**Isolated-artifact rule compliance:** no build was run at all (SDK absent), so no artifacts exist anywhere. The only tree change is this report file under `docs/`.

---

# 3. Repository Inventory

Verified by `git ls-tree`, `find`, `wc`, and targeted `cat`. "Runtime-critical" = ships in the app and is on a core execution path; "Arabic-relevant" = affects the requested future product.

| Area | Purpose | Scope (verified) | Key files / projects | Runtime-critical | Arabic/product-relevant | Evidence |
|---|---|---|---|---|---|---|
| `src/ui` | Avalonia desktop app (`WinExe`, `net10.0`, AssemblyName `SubtitleEdit`) | 1,729 `.cs` files, ~441,956 LOC; 15 Feature areas; 314 `*ViewModel*` files; only 1 `.axaml` (Styles.axaml) — UI is built in C# via `Avalonia.Markup.Declarative` | `Program.cs`, `App.axaml`-less startup, `Features/{Main,Translate,Video,Tools,Ocr,Options,Edit,Files,Help,Shared,SpellCheck,Sync,Assa,Ssa,WebVtt}`, `Controls/{AudioVisualizerControl,VideoPlayer,SyntaxTextEditorControl}`, `Logic/Config` (Se* settings), `Logic/Download` (37 download services) | Yes | Yes (all UX) | `src/ui/UI.csproj`; `find src/ui -name '*.cs' \| wc -l`; `ls src/ui/Features` |
| `src/libse` | Core subtitle engine (also targets `netstandard2.1`) — parsing/writing ~300+ formats, timing, fixing, image formats | 755 `.cs` files, ~167,892 LOC; 429 files in `SubtitleFormats/` | `Common/Subtitle.cs`, `Common/Paragraph.cs`, `Common/TimeCode.cs`, `Common/Utilities.cs` (4,066 L), `SubtitleFormats/*`, `BluRaySup`, `VobSub`, `Cea608/708`, `CDG`, `ContainerFormats`, `DetectEncoding`, `Settings/` (core settings incl. `GeneralSettings.RightToLeftMode`) | Yes | Yes (format correctness, RTL text transforms) | `find src/libse -name '*.cs'`; `grep -c` as in §7 |
| `src/libuilogic` | Shared app logic: translation engines, ASR model drivers, OCR/SpellCheck engine, media/ffmpeg helpers, export image rendering, llama-server management | 171 `.cs` files, ~39,040 LOC | `AutoTranslate/*` (36 files, 30+ engines), `AudioToText/*` (12 files), `LlamaCpp/LlamaCppServerManager.cs` (1,158 L), `Translate/*` (orchestration), `Ocr/`, `SpellCheck/`, `Media/FfmpegGenerator.cs`, `Export/ImageRenderer.cs`, `Http/HttpClientFactoryWithProxy.cs` | Yes | Yes (translation/ASR core) | `find src/libuilogic` |
| `src/seconv` | Cross-platform CLI converter (Spectre.Console.Cli), batch headless use | 46 `.cs` files, ~12,764 LOC | `Program.cs`, `Commands/`, `Core/`, `Helpers/` | No (separate tool) | Moderately (batch translate/fix in CI-friendly form) | `src/seconv/Program.cs` |
| `tests/` | xUnit suites + benchmarks | 1,022 `.cs`, ~154,987 LOC: libse 181, libuilogic 88, seconv 53, UI 645 (headless Avalonia), benchmarks 55 | `tests/libuilogic/AutoTranslate/` (13 engine/orchestration test files), `tests/libuilogic/Translate/` (6), RTL tests (see §18), `tests/benchmarks/PerfHuntRound*` | No | Yes (guards) | `find tests -name '*.cs' \| wc -l`; `ls tests/libuilogic/AutoTranslate` |
| `docs/` | GitHub Pages documentation site (Jekyll) | 234 files; 109 `.md` | `overview.md`, `faq.md`, `features/ai-review.md`, `plugin.md`, `translating.md` | No | Yes (user education) | `docs/index.md` |
| `.github/` | CI/CD + AI contribution rules | 7 workflows + `copilot-instructions.md` + `skills/subtitleedit-ai-contributor-guidelines/SKILL.md` | `workflows/tests.yml` (dotnet 10, full suite, PR+main gate), `build-ui.yml`, `build-flatpak.yml`, `build-seconv.yml`, `deploy-docs.yml`, `publish-nuget.yml`, `stale.yml` | No | Indirectly | §4 |
| `installer/` | Windows WiX + Inno Setup, Flatpak (`dk.nikse.subtitleedit`), macOS bundle | `Product.wxs`, `WindowsInno/*.iss` + 9 `.isl` installer languages, `flatpak/*`, `macBundle/*` | — | No | Indirectly | `find installer -maxdepth 2` |
| `Dictionaries/` | Spell-check support lists (names, abbreviations, no-break-after) | many `*_<lang>.xml` incl. `ar_NoBreakAfterList.xml` | — | Yes (assets zipped at build) | Yes | `ls Dictionaries \| head` |
| `Ocr/` | Binary OCR template DB (Latin) | `Latin.db`, `Latin.nocr` | — | Yes (assets) | Latin-only (no Arabic OCR DB found) | `ls Ocr` |
| `Themes/` | Toolbar/theme icon PNGs | Classic + more, zipped at build by `src/AssetZips.targets` | — | Yes (assets) | No | `find Themes`; `src/AssetZips.targets` |
| `src/ui/Assets/Languages/` | UI translation JSONs | 35 files incl. `Arabic.json` (4,043 translated strings vs ~4,066 English keys ≈ 99.4% complete) | — | Yes (assets) | **Yes** (Arabic UI) | `ls src/ui/Assets/Languages \| wc -l`; key counts in §11 |
| `SubtitleEdit.sln` | Root solution wiring all projects + docs/installer items | Format 12.00, VS 18 | — | — | — | `git show HEAD:SubtitleEdit.sln` |
| Root files | README (privacy/offline claims, platform matrix), LICENSE (MIT), Changelogs (2), `Directory.Build.props` (CetCompat=false for Skia/HarfBuzz CET crash, issue #11062), `make-installer.bat` | — | — | — | — | §4, §19 |

**Format-count discrepancy (explicit).** `docs/overview.md` says "380+ formats"; `Program.cs` says warm-up "loads and JIT-compiles ~330 types"; `git ls-tree -r HEAD src/libse/SubtitleFormats` counts 429 `.cs` files of which 397 match `: SubtitleFormat` (grep; the remainder are helpers/abstract/base files). The three numbers are reconcilable only partially (abstract base + helpers vs concrete formats vs marketing rounding); the exact *concrete format* count is UNKNOWN (would require enumerating concrete subclasses — not done to keep this audit read-only-cheap). Reported as a documentation-precision issue, not an error.

**External integration code (verified locations):** ffmpeg (`FfmpegGenerator.cs`, `FfmpegHelper.cs`, `BurnInViewModel.cs`), mpv (`VideoPlayerControl.cs`, `LibMpvDownloadService.cs`), VLC (`VlcVideoPlayer.cs`), whisper family + Crisp ASR + Qwen3-ASR + Parakeet (`Features/Video/SpeechToText/Engines/*`, `libuilogic/AudioToText/*`), llama.cpp `llama-server` (`LlamaCppServerManager.cs`), TTS engines (~25 files under `Features/Video/TextToSpeech/Engines/`), OCR engines (`Features/Ocr/Engines/` incl. Paddle, Tesseract, Google Lens, CrispEmbed), cloud STT (Google Cloud, DashScope Qwen3, OpenAI-compatible, OpenRouter — `IOnlineSttEngine.cs`), yt-dlp (`YtDlpDownloadService.cs`).


---

# 4. Repository Instruction Hierarchy

## 4.1 Located instruction documents (verified by filename search across the tree)

| Document | Status | Content (verified by reading) |
|---|---|---|
| `.github/copilot-instructions.md` | Present | Points all AI agents to the SKILL.md below; demands "small, verified" changes tied to the request. |
| `.github/skills/subtitleedit-ai-contributor-guidelines/SKILL.md` | Present — **the primary developer/AI instruction set** | 7 rules: clarify before changing; keep changes surgical; prefer boring code; verify the actual risk (tests/manual paths); **protect user data & offline expectations — no telemetry/analytics/network unless explicitly reviewed; keep optional third-party integrations explicit and isolated**; useful PR notes; an "AI PR gate" (dedicated branch → PR → request Copilot review → resolve → merge only via that loop, never direct commits to `main`). Front-matter declares MIT license; attributes inspiration to `forrestchang/andrej-karpathy-skills` (not a vendored copy). |
| `AGENTS.md`, `CONTRIBUTING*`, `CODE_OF_CONDUCT`, `SECURITY.md`, coding-standard docs | **Absent** (filename search found none) | — |
| `Directory.Build.props` | Present (build config w/ explanatory comment) | `CetCompat=false` to prevent shadow-stack (CET) crashes from SkiaSharp/HarfBuzzSharp native deps (issue #11062). |
| `src/ui/UI.csproj`, `src/libse/LibSE.csproj`, `Directory.Build.props` | Present | Target frameworks (`net10.0`; libse also `netstandard2.1`), nullable enable, unsafe allowed, `AvaloniaUseCompiledBindingsByDefault`. |
| `docs/` | Present | User-facing documentation, not developer rules. |
| In-code issue references | Pervasive | Comments cite SE issue numbers (#11062, #11864, #11744, #11515, #13803, #13830, #13927, #15009, #15170, #12249, #14150, #9969, #14803…) — these are the de-facto architecture decision records. |

## 4.2 Instruction precedence map (as applied during this audit)

1. **User/scope rules of this engagement** (audit-only; only this report file may be created/committed).
2. **SKILL.md** — binding for any future implementation phase: surgical diffs, offline trust boundary, verification-first, PR+Copilot-review gate.
3. **copilot-instructions.md** — delegates to SKILL.md (consistent, no conflict).
4. **Build/config files** — constraints on how code must be structured (e.g., asset zips built from loose folders; never check zips in).
5. **In-code comments** — local invariants and history; highest fidelity for "why is it written this way".

## 4.3 Instruction conflicts found

- **SKILL.md AI-PR-gate vs this engagement's phase rules (explicit).** SKILL.md §7 mandates that AI-owned changes go through branch → PR → Copilot review → merge loop and forbids direct commits to `main`. This audit phase forbids PRs and (by engagement rule) pushes. No conflict for *this* phase (no implementation), but any Phase B implementation must reconcile: PR from the session branch is compatible with SKILL.md; direct pushes to `main` are forbidden by both. REPOSITORY_FACT, no action needed now.
- **Upstream/external claim mismatch (informational).** Third-party site `subtitleedit.org` states Subtitle Edit is "GPL v3" and advertises v4.0.15 (EXTERNAL_EVIDENCE, fetched 2026-09-26). The upstream repo `SubtitleEdit/subtitleedit` LICENSE at HEAD is **MIT** (Copyright 2026 Nikolaj Olsson) and its official FAQ states "released under the MIT license" (EXTERNAL_EVIDENCE). This fork's root LICENSE is MIT (REPOSITORY_FACT). Conclusion: the "GPL" claim is an outdated third-party error, not a repository inconsistency — but anyone doing license diligence should be pointed at the GitHub LICENSE, not that site.
- **No other conflicts found** among in-repo documents (there is only one prescriptive document).

---

# 5. Architecture Map

Reconstructed from actual code, not folder names. Each item lists file/symbol evidence.

## 5.1 Entry point & lifecycle (REPOSITORY_FACT)

- `src/ui/Program.cs` `Main(string[])` `[STAThread]`:
  1. `HarfBuzzNativeFix.Apply()` (Linux symbol deep-bind fix, #11864) → `ApplyLinuxDeadKeyInputFix()`.
  2. Global handlers: `AppDomain.UnhandledException` → `Se.LogError`; `Dispatcher.UIThread.UnhandledException` → log + `e.Handled = true` (app survives UI-thread exceptions, #11515).
  3. `ClassicDesktopStyleApplicationLifetime` with `ShutdownMode.OnLastWindowClose`.
  4. `Se.LoadSettings()` → JSON settings (`Se.SaveSettings` serializes the `Se` graph via `SeJsonContext`); `Encoding.RegisterProvider(CodePagesEncodingProvider)` (needed by CHK format, cp850).
  5. `SubtitleFormat.WarmUpAsync()` on a worker thread — comment: building the format list JIT-compiles "~330 types, ~40–90 ms on the UI thread" (moved off-thread intentionally).
  6. Spell-check config delegation (`SpellCheckConfig.DictionariesFolder` etc., #11744); `Se.LoadLanguage()` before any window (mac menu bar language, #11505).
  7. Platform font setup: Linux → embedded Inter + explicit CJK `FontFallbacks` (#11355); macOS → Helvetica Neue default + CJK fallbacks (caret bug workaround); Windows → system default.
  8. `SetupDependencyInjection()` → `ServiceCollection.AddSubtitleEditServices()` → `Locator.Services` (Microsoft.Extensions.DependencyInjection).
  9. `MainWindowFactory.Create(isPrimary: true)`; startup args parsed: subtitle path + `/video:<path>` `--video:`/`--video <path>`; special `--batchconvertui` mode launches only the Batch-Convert window (`SetupBatchConvertOnlyWindow`).
  10. macOS activation: Dock Reopen double-click → new window; FileActivated → `PendingFileToOpen` vs new-window decision governed by `StartupFileDecisionDone` + grace delay in `MainViewModel.OnLoaded`.
- **Multi-window:** `MainWindowFactory.OpenNewWindow()` — the app supports multiple independent editor windows (Windows-like per-process semantics on macOS handled in `SetupNativeMenu`).

## 5.2 UI architecture (REPOSITORY_FACT)

- **Avalonia 12.1.3 + `Avalonia.Markup.Declarative`**: windows/views are constructed in C# (e.g., `Features/Main/Layout/*`, `AutoTranslateWindow.cs`); only `Styles.axaml` is XAML. Consequences: UI is compiler-checked and diff-friendly, but there is no XAML hot-reload/designer story.
- **MVVM:** `CommunityToolkit.Mvvm` 8.4.2 (`ObservableObject`, `[ObservableProperty]` — e.g., `SpeechToTextJobItem`). ViewModels are partial classes; `MainViewModel` alone is 34,977 lines (split across partials: `MainViewModel.SpeechOnlyWaveform.cs` and inline regions).
- **DI:** service registrations centralized in `src/ui/DependencyInjectionExtensions.cs` (hundreds of `services.AddSingleton<...Window/ViewModel>` — file read; it is a flat registry, no modules).
- **Shared controls:** `TableView` grid + custom `SyntaxTextEditorControl`, `AudioVisualizerControl` (custom Skia waveform/spectrogram, 5,058 lines incl. bidi runs), `VideoPlayerControl` (mpv via libmpv; VLC variant; empty fallback).

## 5.3 Core domain & service boundaries (REPOSITORY_FACT)

- **Domain:** `Subtitle` = `List<Paragraph>` + `Header/Footer/FileName/OriginalFormat/OriginalEncoding`; conversions via `Subtitle.Parse(...)`/`ToText(format)`; ~300+ `SubtitleFormat` implementations in libse (see §3 for counts).
- **App logic layer (`libuilogic`):** deliberately UI-independent logic that is shared by `ui` and `seconv` — translation engines (`AutoTranslate/`), ASR drivers (`AudioToText/`), llama-server management (`LlamaCpp/`), OCR fix engine, spell check, ffmpeg arg generation (`Media/FfmpegGenerator.cs`), image-based export rendering (`Export/ImageRenderer.cs`), HTTP w/ proxy (`Http/`). Dependency direction: `ui → libuilogic → libse`; `seconv → libuilogic → libse` (verified via `using` statements in `Program.cs` of seconv and engines' namespaces).
- **Feature layer (`ui/Features/*`):** one folder per workflow (window+viewmodel), orchestrated by `MainViewModel` for the main editor.

## 5.4 State, configuration, persistence (REPOSITORY_FACT)

- **Settings:** one `Se` object graph (`src/ui/Logic/Config/Se*.cs`, `Se.cs` 1,371 lines) serialized to `Settings.json` in the app data folder (`Se.SaveSettings`, `SeJsonContext` source-gen). Core (libse) keeps a separate `Configuration.Settings` (e.g., `Tools.ChatGptApiKey`, `General.RightToLeftMode`, `General.FFmpegLocation`) — the two are bridged in places (e.g., `SpellCheckConfig` lambdas in `Program.cs`, #11744). **Two-settings-worlds is a real coupling hazard** (INFERENCE, evidence: both APIs used in the same call paths above).
- **Documents:** in-memory `Subtitle`; auto-backup via `Logic/AutoBackupService.cs` (416 L; `DispatcherTimer`, backs up both subtitle + `Settings.json`, restore UI in `Features/Files/RestoreAutoBackup`).
- **Undo/history:** show-history feature (`Features/Edit/ShowHistory`) — existence verified; internals not audited (see ledger).

## 5.5 Event flow, threading, async, cancellation (REPOSITORY_FACT)

- UI updates marshaled via `Dispatcher.UIThread.Invoke/Post` (e.g., `AdvancedTranslatorBase.TranslateChunkAsync` writes DataGrid-bound rows on UI thread; `AutoTranslateViewModel` same).
- Progress coalescing: `Logic/CoalescedUiUpdateQueue.cs` + `EnqueueTranslateProgress` in `AutoTranslateViewModel` (prevents UI flooding).
- Cancellation: `CancellationTokenSource` per job (`AutoTranslateViewModel._cancellationTokenSource`; `Cancel()` sets `_abort`, cancels token, stops local llama server via `StopLocalLlamaCppServerAfterCancel()`); ffmpeg runs get `Kill(true)` + `WaitForExit(3000)` (`BurnInViewModel.KillFfmpegProcess`); window close cancels running translation (#2628 region verified).
- Logging: `Se.LogError(...)` → `error-log.txt` in data folder; tools log via `Se.WriteToolsLog` (burn-in logs full ffmpeg command lines, `BurnInViewModel.StartFfmpegProcess`).
- Error handling style: exceptions logged then rethrown (`DoAutoTranslate` catch/log/throw); engines set `Error` string properties surfaced in `TranslationErrorWindow`.

## 5.6 Process execution & FFmpeg (REPOSITORY_FACT)

- 242 matches for `Process.Start|new Process` in `src` — the app orchestrates many native tools. Inspected sites consistently use `UseShellExecute=false`, `CreateNoWindow=true`, quoted args, UTF-8 redirected stdio (`SpeechToTextViewModel` lines ~4289–4310, `BurnInViewModel`).
- **Burn-in:** `BurnInViewModel.GetFfmpegProcess` → `FfmpegGenerator.GenerateHardcodedVideoFile(...)` builds `-filter_complex ... scale=W:H,ass=<file>` (libass) or input-piped image subtitles; two-pass mode writes to null device; per-run working directory = folder of the ASSA file; progress parsed from `frame=` via `FfmpegProgressTracker`; full command logged.
- **Audio extraction for ASR:** `-nostdin -y -i <in> -vn -ar 16000 -ac 1 <out.wav>` (SpeechIsolationModel; similar in STT VM) — 16 kHz mono PCM is the contract for engines.
- **Video playback:** libmpv; **macOS/Linux bundle ffmpeg+mpv** (README, flatpak manifest).

## 5.7 Extension points that actually exist today (REPOSITORY_FACT)

- `IAutoTranslator` — implement + register in `AutoTranslateCombos`/factory to add an engine (30+ precedents).
- `IBatchContextTranslator` — context-aware batch engines bypass line-merge heuristics.
- `ISpeechToTextEngine` (+ `WhisperEngineFactory`, `CrispAsrEngineBase`) — engine metadata, install/download, command line.
- `ITtsEngine` / `IPerLineCloneEngine` (TTS), OCR engines (`Features/Ocr/Engines/*` incl. `CrispEmbedBackend`).
- Batch convert functions enum `BatchConvertFunctionType` (+ `BatchConvertFunction` registry incl. `AutoTranslate`, `FixRightToLeft`).
- Plugins UI exists (`Features/Options/Plugins`, `MenuPlugins`) — plugin *runtime* mechanism not audited (ledger: LIMITED).

## 5.8 Invariants & fragile assumptions worth protecting (INFERENCE from facts)

- Paragraph↔row index alignment during translation (rows are DataGrid-bound; batch engines promise 1:1 line alignment — comments in `AdvancedTranslatorBase`/`IBatchContextTranslator`).
- `<br/>` placeholder encoding of line breaks in classic LLM engines (`LlmTranslatePrompt` doc comments; MiLMMT-46 mirroring bug #13803/#13927 notes).
- Stable-prefix prompt ordering for llama-server KV cache reuse (`LlamaCppAdvancedProtocol` header comment) — any style-layer addition must append *after* stable parts or cache hit-rates collapse.
- UI-thread-only writes to bound rows (Dispatcher.Invoke) — breaking this races the grid.
- ASSA override blocks must be stripped from translation input (`StrippedLine.Strip`) — models "normalize" tags (#13927).
- The `Arabic.json` UI language and `Languages.zip` asset pipeline (`AssetZips.targets`) — translations ride the same zip discipline as themes/dictionaries.


---

# 6. Dependency Map

## 6.1 Project graph (verified from csproj/solution and namespaces)

```
SubtitleEdit.sln
├── src/libse            (netstandard2.1; net10.0)   ← zero UI deps; core formats/engine
├── src/libuilogic       (net10.0)  → libse          ← engines, ffmpeg args, downloads helpers
│     └── references Avalonia types only at the margins (e.g., Dispatcher in AdvancedTranslatorBase lives in ui; LlamaCppServerManager stays pure)
├── src/ui               (net10.0, WinExe) → libuilogic → libse
│     └── Avalonia 12.1.3 (+Desktop, +Markup.Declarative, +Themes.Fluent, +Fonts.Inter, +Diagnostics[Debug]),
│         CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.DependencyInjection 11.0.0-preview.2,
│         FFmpeg.AutoGen 9.0.1.1, Google.Cloud.TextToSpeech.V1 3.18.0, SharpCompress 0.50.4,
│         SkiaSharp (+HarfBuzz pin 8.3.1.5 w/ ABI-mismatch comment re #11864), Optris.Icons.*
├── src/seconv           (CLI, Spectre.Console) → libuilogic → libse
└── tests/{libse,libuilogic,seconv,UI} + tests/benchmarks (BenchmarkDotNet-style PerfHunt suites)
```

## 6.2 External native/tool dependencies at runtime (verified)

| Dependency | Used for | How obtained | Evidence |
|---|---|---|---|
| ffmpeg | burn-in, audio extraction, remux/cut, probes | bundled (mac dmg/flatpak), system on Linux, or in-app download (`FfmpegDownloadService`) | `BurnInViewModel`, README, `Logic/Download` |
| libmpv (mpv) | video playback | same as above (`LibMpvDownloadService`) | `VideoPlayerControl.cs` |
| whisper.cpp / Const-me / Purfview Faster-Whisper-XXL / CTranslate2 / WhisperX builds | local ASR | in-app download w/ pinned URLs (§10.4) | `WhisperDownloadService.cs` |
| `llama-server` (llama.cpp) + curated GGUF models | local LLM translate/OCR/AI-review | in-app download; SE starts/stops server | `LlamaCppServerManager.cs` |
| Crisp ASR runtime (self-hosted engine family, ~20 model cards in `src/ui/Assets/SpeechToText/*.txt`) | local ASR/align | in-app download, variant by GPU (`CrispAsrDownloadService`, size table in `CrispAsrEngineBase`) | engines folder |
| yt-dlp | open video from URL | in-app download | `YtDlpDownloadService.cs` |
| Tesseract / PaddleOCR / Google-Lens OCR binaries & models | OCR | in-app download | `TesseractDownloadService.cs`, `PaddleOcr.cs` |
| TTS runtimes (Kokoro/Chatterbox/Qwen3-TTS/F5/IndexTTS/OmniVoice/… via "AudioCpp"/"Crisp" runtimes) + cloud TTS (Azure/Google/ElevenLabs/Edge/Murf/Mistral) | dubbing | in-app download or cloud API | `Features/Video/TextToSpeech/Engines/*` |

## 6.3 Coupling observations (INFERENCE)

- `ui` ↔ libse `Configuration.Settings` and `ui` `Se.Settings` are two config universes with manual bridges (evidence in §5.4). New pipeline features should pick one (the `Se` graph) and bridge explicitly.
- `AdvancedTranslatorBase` (in `ui`) references `Avalonia.Threading` — the most advanced translation engine is UI-project code, so headless `seconv` cannot use it (its batch loop in `DoAutoTranslate` does support `IBatchContextTranslator` generally — engine availability differs by host). Verified: `DoAutoTranslate.cs` is in libuilogic and generic; the advanced engines are in `ui/Features/Translate/LlamaCppAdvanced/`.

---

# 7. Coverage Ledger (condensed)

Depth scale: **Deep** = implementation read at method level; **Medium** = structure + key methods read; **Skim** = existence + purpose + spot reads; **Not audited**.

| Subsystem | Files inspected (examples) | Key types/methods verified | Depth | Call path verified | Risks noted | Evidence |
|---|---|---|---|---|---|---|
| App startup/lifecycle | `Program.cs` (full read) | `Main`, DI setup, activation, args | Deep | Yes (launch→MainWindow) | platform fork complexity | §5.1 |
| Main editor VM | `MainViewModel.cs` (partial reads, structure) | grid bindings, RTL keyboard handlers (~30388–30860), preview reloaders | Skim (34,977 L — deliberately sampled) | Partially | god-class risk | §5.2, §16 |
| Subtitle core | `Subtitle.cs`, `Paragraph.cs`, `TimeCode.cs` | public APIs, Parse/ToText, Adjust, RecalculateDisplayTimes | Medium | Yes (used by translate flows) | 20 MB cap, format-specific corners | §8 |
| Subtitle formats | `Pac.cs`, `Cavena890.cs`, `Ebu.cs`, `Idx.cs` (RTL excerpts) | Arabic byte encoders, reversal, U+200E | Skim+Arabic excerpts | No (format-specific) | legacy visual-reversal semantics | §11 |
| Translation orchestration | `DoAutoTranslate.cs` (full), `MergeAndSplitHelper.cs` (signatures + key logic), `Formatting.cs`, `TranslationHelper.cs` | merge/split heuristics, single-line escalation, no-progress cap | Deep | Yes (A/B traces §9) | heuristic split can misalign | §10 |
| Translation engines | `ChatGptTranslate.cs` (full), `OllamaTranslate.cs` (full), `DeepLTranslate.cs` (excerpt), `GoogleTranslateV2.cs` (excerpt), `LlamaCppTranslate.cs`, `AdvancedTranslatorBase.cs` (full), `LlamaCppAdvancedProtocol.cs` (full), `LlmTranslatePrompt.cs` (full), `AutoTranslateUrl`/`TranslateStrategy` (headers) | prompt construction, retries, `<think>` stripping, JSON schema, language lists | Deep (8 of 36 engines) / Skim (rest) | Yes | manual JSON building; engine drift | §10 |
| Local LLM server mgmt | `LlamaCppServerManager.cs` (first 100 L + model tables) | curated models, prompts, lifecycle | Medium | Yes (UI wiring) | model-license mixing | §10.4, §20 |
| ASR | `WhisperChoice.cs`, `IWhisperModel.cs`, `WhisperHelper.cs` (signatures), `WhisperDownloadService.cs` (URL pins), `SpeechToText/Engines/*` (42 files listed; `ISpeechToTextEngine.cs`, `CrispAsrEngineBase.cs` read), `SpeechToTextViewModel.cs` (structure + process exec ~4270–4360), `OpenAiSttChunker.cs` (full header) | engine abstraction, chunking, process exec | Medium | Yes (C trace §9) | dialect WER (external) | §9, §10.4 |
| Burn-in / video | `BurnInViewModel.cs` (excerpts 300–1200, 3005), `FfmpegGenerator.cs` (filter graph excerpts) | ass= filter, two-pass, kill logic, tools log | Medium | Yes (C trace §9) | ffmpeg version drift | §9 |
| RTL/Arabic editor | `RightToLeftHelper.cs` (full), `Utilities.cs` RTL excerpts, `LanguageAutoDetect.cs` (RTL words), `CalcIgnoreArabicDiacritics.cs` (full), `SkiaBidiRuns.cs` (existence + tests) | FlowDirection mirroring, U+202B embedding, CPS | Deep (RTL) | Yes (UI wiring read) | visual-reversal legacy formats | §11 |
| Batch convert | `BatchConvertFunctionType.cs` (full), `BatchConvertFunction.cs` (excerpt), `BatchConverter.cs` (grep) | 31 functions incl. AutoTranslate, FixRightToLeft | Medium | Partially | — | §9-D |
| seconv CLI | `Program.cs` (first 40 L) | arg canonicalization, UTF-8 console | Skim | No | — | §3 |
| Settings/config | `Se.cs` (excerpts), `SeAutoTranslate.cs` (full), `SeLlamaCppAdvanced.cs` (full), `ToolsSettings.cs` (prompt excerpts), `GeneralSettings.cs` (RTL) | persistence, keys, rate limits | Deep | Yes | plaintext keys | §19 |
| Downloads | `DownloadHelper.cs` (range/resume excerpts), `DownloadHashManager.cs` (full header), `WhisperDownloadService.cs` | resumable + SHA-256 registry | Medium | Yes | pin rot | §15, §19 |
| Tests | 19 test files listed by name across RTL/translate; suites counted | see §18 | Medium (inventory) | n/a | coverage gaps | §18 |
| OCR subsystem | folder listings, engine names, `Ocr/` assets | engines exist (NOcr, binary, Tesseract, Paddle, Lens, CrispEmbed) | Skim | No | Arabic OCR DB absent | §11.6 |
| TTS/dubbing | engine listing, `OmniVoiceLanguages.cs` excerpt (arz) | ~25 engines | Skim | No | out of core scope | §12 |
| Undo/history, plugins runtime, waveform internals, image subtitle export pipeline, MKV/TS containers | listings only | — | Not audited / LIMITED | No | unknowns recorded | §34 |

---

# 8. Core Data Model

REPOSITORY_FACT — `src/libse/Common/{Subtitle,Paragraph,TimeCode}.cs`.

- **`Subtitle`**: `List<Paragraph> Paragraphs` (private set), `Header`, `Footer`, `FileName`, `OriginalFormat`, `OriginalEncoding`, `MaxFileSize = 20 MB` (loads guard). Key operations verified: `Parse(Stream/lines/fileName[, encoding][, format][, formatsToLookFor])`, `LoadSubtitle(..., out Encoding, ...)` (batch mode, frame-rate override), `ToText(SubtitleFormat)`, `AdjustDisplayTimeUsing{Percent,Seconds}`, `RecalculateDisplayTimes(maxCharPerSec,...,enforceDurationLimits)`, `ChangeFrameRate`, `Sort(criteria)`, `Renumber`, `InsertParagraphInCorrectTimeOrder`, `RemoveEmptyLines`, `GetFastHashCode*`, `GetAllTexts`.
- **`Paragraph`**: `Number`, `Text`, `StartTime`/`EndTime` (`TimeCode`), computed `Duration`; `Forced`, `Extra`, `IsComment`, `Actor`, `Region`, `MarginL/R/V`, `Effect`, `Layer`, `Id (Guid?)`, `Language`, `Style`, `NewSection`, `Bookmark`; `GetCharactersPerSecond()` (+ `ICalcLength` overload — the Arabic-aware calculators plug in here).
- **`TimeCode`**: double milliseconds base unit; `ParseToMilliseconds` accepts `: , .` separators; HHMMSSFF and HHMMSS parse variants; max 99:59:59.999.
- **Editor row model:** `SubtitleLineViewModel` (ui) wraps paragraph state for the grid; `TranslateRow` (`libuilogic/Translate/TranslateRow.cs`) = {Number, Show, Hide, Duration, Text, TranslatedText} — the translation grid contract.
- **Batch/job models:** `SpeechToTextJobItem` (input video, size, status, `FfmpegMediaInfo`), `BurnInJobItem` (in/out files, ASSA temp file, resolution, codecs), `BatchConvert*` (function enum + per-file items).

**Implication for the Arabic product (INFERENCE):** the data model already carries `Paragraph.Language`, `Actor`, `Style`, `Layer` — enough hooks for per-line dialect/style tagging and per-speaker styles **without schema changes**; what's missing is anything that *fills or consumes* them for dialect purposes.


---

# 9. End-to-End Workflow Traces (real call chains)

Legend: ✅ = verified by reading the code at that hop; ⚠️ = hop not fully verified (named in §34).

## A) Video → ASR → subtitle creation → post-processing → editor

```
ENTRY: ui Features/Video/SpeechToText window (Video ▸ Speech to text)
  → SpeechToTextViewModel.cs (5,953 L)
    ✅ job queue: List<SpeechToTextJobItem> {InputVideoFileName, Size, Status, MediaInfo}  (SpeechToTextJobItem.cs)
    ✅ engine resolution: WhisperEngineFactory / CrispAsrEngineBase subclasses
       (42 files: WhisperEngineCpp(CuBlas/Vulkan), ConstMe, CTranslate2, WhisperX, OpenAi,
        OpenAiCompatible, OpenRouter, DashScope, GoogleCloud, Qwen3AsrCpp, CrispAsr{Parakeet,Canary,
        Cohere,Qwen3,GigaAm,GLM,FireRed,FunAsrNano,FunAsrMltNano,Granite,Omni,Kyutai,Madlad,Mega,
        MossDiarize,SenseVoice,Ark,Voxtral})
    ✅ audio extraction: ffmpeg `-nostdin -y -i <in> -vn -ar 16000 -ac 1 <wav>`
       (SpeechIsolationModel.cs:46; per-run subfolder, SpeechToTextViewModel ~4821–4867)
    ✅ process exec: StartInfo(executable, args), UTF-8 stdio redirect, engine folder on library
       path, ffmpeg dir added to PATH (SpeechToTextViewModel.cs 4289–4360)
    ✅ parse engine output → ResultText (libuilogic/AudioToText/ResultText.cs; WhisperCppJson.cs)
    ✅ post-processing: SpeechToTextPostProcessor.cs / SpeechToTextPostProcessingViewModel.cs,
       SpeechToTextTimingFixer.cs, SpeechToTextQualityReport.cs (names + wiring verified at listing
       level; internals ⚠️ not line-audited)
  → OUTPUT: new Subtitle merged into the editor (MainViewModel grid)   ⚠️ final handoff method not read line-by-line
```

## B) Existing subtitle → translation → translated subtitle → export

```
ENTRY: ui Features/Translate/AutoTranslateWindow ("Auto-translate")
  → AutoTranslateViewModel.cs (2,676 L)
    ✅ engine pick (AutoTranslateCombos), language pick (TranslationPair), Initialize()
    ✅ DoTranslate(onlyCurrentLine) → StartTranslation(translator) → loop (1496–1660):
        · if translator is AdvancedTranslatorBase → TranslateBatchAsync(Rows, index, …)
          → AdvancedTranslatorBase.TranslateChunkAsync → LlamaCppAdvancedProtocol.BuildSystemPrompt/
            BuildUserContent/BuildResponseFormatJson → LlamaCppAdvancedClient.ChatAsync (HTTP)
          → ParseTranslations → UIThread row writes; bisect batch on failure (count/2)
        · else → MergeAndSplitHelper.MergeAndTranslateIfPossible (libuilogic) →
          MergeMultipleLines (sentence-period counting w/ abbreviations) → translator.Translate(text,
          src, tgt, ct) [per-engine HTTP/CLI] → SplitMultipleLines(mergeResult, translatedText, lang)
          [length/duration heuristics] → row updates
        · error escalation: 3 errors → force single-line; 3 no-progress → exception; cancel via
          _abort + CancellationTokenSource; Cancel() also stops local llama-server
    ✅ settings shared: Se.Settings.AutoTranslate (SeAutoTranslate.cs) incl. RequestMaxBytes /
       RequestDelaySeconds rate limiting, KeepMusicLinesUntranslated (IsKeptUntranslated ♪ check,
       #9969), per-engine EngineStrategies (TranslateStrategy.cs)
  → OUTPUT: TranslatedText per row → user applies back into Paragraph.Text (apply path ⚠️ not line-audited)
  → EXPORT: File ▸ Save as / export to any of the ~300+ formats via Subtitle.ToText (verified API;
    per-format writers ⚠️ not audited individually)
```

## C) Video → transcription → translation → formatting → burn-in

```
Same as (A) then (B), then:
ENTRY: ui Features/Video/BurnIn (BurnInWindow/BurnInViewModel.cs, ~3,000+ L)
  ✅ converts current subtitle to ASSA temp file (WriteAllText(_tempPreviewAssaFileName, assaText) :3005)
  ✅ GetFfmpegProcess → FfmpegGenerator.GenerateHardcodedVideoFile(..., assaFileName, W, H, codec,
     preset, pixfmt, crf, audioEncoding, stereo, samplerate, tune, bitrate, pass, …)
     → builds `-filter_complex ... scale=W:H,ass=<assa file>` (libass render) or image-subtitle
       input path (FfmpegGenerator.cs 357–386)
  ✅ two-pass analyze (writes null device; _ffmpegWritesOutputFile=false), optional parameter
     prompt per pass, progress via `frame=` parsing (FfmpegProgressTracker), full cmd logged via
     Se.WriteToolsLog("Burn-in encode: …")
  ✅ failure/cancel: exit-code check (380–400), Kill(true)+WaitForExit(3000) (KillFfmpegProcess :461)
  → OUTPUT: encoded video with burned subtitles
```

## D) Batch processing

```
ENTRY: Tools ▸ Batch convert (or app launched with --batchconvertui → SetupBatchConvertOnlyWindow)
  ✅ BatchConvertWindow/ViewModel; function registry BatchConvertFunction.cs
     (MakeFunction(BatchConvertFunctionType.AutoTranslate, …, ViewAutoTranslate.Make(vm), …))
  ✅ 31 functions verified in BatchConvertFunctionType.cs — incl. FixCommonErrors, MultipleReplace,
     AutoTranslate, FixRightToLeft, MergeShortLines, ApplyDurationLimits, AutoBalanceLines,
     AssaChange*, SortBy…
  → BatchConverter.cs applies per file  ⚠️ per-function execution loop not line-audited
  → seconv CLI (src/seconv) exposes the same core headlessly (Spectre.Console.Cli commands;
    legacy /convert arg compatibility verified in Program.cs)
```

## E) Long-video processing

```
STT side:  ✅ per-file job queue (SpeechToTextJobItem list, sequential processing in
           SpeechToTextViewModel loop) — one video at a time; chunking exists for upload-capped
           engines: OpenAiSttChunker splits at silences under a 24 MB threshold (23 MB chunks),
           boundary-snap + ffmpeg parse functions unit-tested (comment header).
Local whisper side: engines stream their own sliding window; SE feeds the whole wav  ⚠️ (per-engine
           memory behavior unknown — not measured; labeled UNKNOWN in §16).
Translate side: ✅ streaming row-by-row loop with progress + resume-from-selected-row
           (DoTranslate starts at SelectedTranslateRow) — interruption-safe-ish: translated rows
           persist in the grid; a re-run can start at selection.
Burn-in side: ✅ batch job list (BurnInJobItem) + per-job ffmpeg, two-pass, queue continues across
           files (StartFfmpegProcess resets per-pass timers — comment).
⚠️ No single "resume a half-finished pipeline job" object exists across STT→translate→burn-in.
```

## F) Cancellation and recovery

```
✅ Translation: CancellationTokenSource per run; window close cancels (2628–2632); OperationCanceledException
   filtered so Cancel is not surfaced as error (1642–1646); local llama-server stopped on cancel (758–770).
✅ ffmpeg: kill + wait; progress timer decoupled; tools log written per stage.
✅ STT: process exec wrapped; ⚠️ mid-run cancel of a specific whisper child process not line-audited.
✅ App-level: Dispatcher.UnhandledException logged+handled (crash resistance); AutoBackupService
   (DispatcherTimer; subtitle + Settings.json backups; Restore Auto-backup UI).
⚠️ Partial completion: if the app dies mid-translation, translated-so-far lives only in the grid/UI
   state — no on-disk translation checkpoint found (INFERENCE from absence in audited files; grep
   for TM/checkpoint found nothing — §10.6).
```

---

# 10. Translation Architecture Audit

## 10.1 Abstraction (REPOSITORY_FACT)

- **`IAutoTranslator`** (`src/libuilogic/AutoTranslate/IAutoTranslator.cs`): `Name`, `Url`, `Error`, `MaxCharacters`, `Initialize()`, `GetSupportedSourceLanguages()`, `GetSupportedTargetLanguages()`, `Task<string> Translate(text, srcCode, tgtCode, CancellationToken)`.
- **`ILineBreakPreservingTranslator`** (marker): engines that keep per-request line breaks; enables boundary-accurate row split (comment cites #14803).
- **`IBatchContextTranslator`** (`Translate/IBatchContextTranslator.cs`): `Task<int> TranslateBatchAsync(rows, index, src, tgt, ct)` — "always at least 1, or throws"; callers must skip merge/split heuristics.
- **Orchestrator:** `DoAutoTranslate.DoTranslate(...)` (libuilogic) — pure logic; the UI variant (`AutoTranslateViewModel.DoTranslate`) duplicates the loop with UI bindings (verified parallel code — mild duplication risk, INFERENCE).

## 10.2 Engine inventory (36 files verified by listing; 8 read in depth)

Online classic MT: Google V1/V2, Microsoft (Bing), DeepL (+DeepLX), LibreTranslate, MyMemory, Baidu, Papago, Nvidia, SeamlessM4T, NoLanguageLeftBehind API/Serve (NLLB), Lara, ApiRoute, AvalAi.
Online LLM: ChatGPT, Gemini, Anthropic, DeepSeek, Groq, Mistral, Perplexity, OpenRouter, OpenAI-compatible.
Local LLM: Ollama, LM Studio, KoboldCpp, LlamaCpp (+ **"llama.cpp advanced (local LLM)"** and **"Ollama advanced"** in ui), CrispAsrMadlad (local MADLAD via Crisp runtime).

Per-backend contract table (deep-read subset; rest verified at list/config level):

| Backend | File | Config | Input contract | Output contract | Context | Failure behavior | Offline? |
|---|---|---|---|---|---|---|---|
| ChatGPT | `ChatGptTranslate.cs` | `ChatGptUrl/ApiKey/Model/Prompt` | `{0}=src,{1}=tgt` prompt + text; ≤1500 chars; manual JSON body | chat `content` → `FixNewLines`→`RemovePreamble` (`<think>` aware)→`DecodeUnicodeEscapes` | none (single call) | retries {2555,5007,9013} ms on `ShouldRetry` (DeepL-shared predicate); else throw w/ body logged | No (cloud) |
| Ollama | `OllamaTranslate.cs` | `OllamaUrl/Model/Prompt` | `/api/generate` or chat-completions if URL ends v1/chat/completions; ≤1000 chars; `<br/>` break encoding | `response`|`content` field, same post-processing | none | 25-min timeout; EnsureSuccessStatusCode; Error body kept | **Yes if user runs Ollama locally** (URL configurable → remote too) |
| llama.cpp advanced | `LlamaCppAdvancedTranslate.cs` + `AdvancedTranslatorBase.cs` + `LlamaCppAdvancedProtocol.cs` | `SeLlamaCppAdvanced` (BatchSize=10, HistoryPairs=12, Synopsis, Glossary, Formality, KeepLineBreaks, Prompt, Temperature/TopP/TopK/RepeatPenalty/MaxTokens, ContextSize=16384, ServerArguments) | numbered `{"history":[…12 pairs],"lines":[{"n":1,"text":…}]}`; ASSA tags stripped (`StrippedLine.Strip`); music lines skipped | **json_schema strict grammar** `{“1”: “…”, “2”: …}` parsed to map; restore tags per line | rolling history + synopsis + glossary + formality | retry once → bisect batch → HttpRequestException; single-line path context-free | **Yes** (SE-managed llama-server or remote URL) |
| DeepL | `DeepLTranslate.cs` (excerpt) | `DeepLApiKey/Url/Formality` | per-engine limits; `MakeTranslationPair("Arabic","ar",false)` verified | standard DeepL response | none | `ShouldRetry` shared w/ ChatGPT path | No |
| Google V2 | `GoogleTranslateV2.cs` (excerpt) | `GoogleApiV2Key` | batched strings ≤ MaxCharacters | Google list-of-translations parse (tests: `GoogleTranslateV2ParseTests`) | none | retries (V1RetryTests) | No |
| NLLB serve/api | `NoLanguageLeftBehind{Api,Serve}.cs` | `NllbApiUrl/NllbServeUrl` | single lines only (always-single-line forced in `DoAutoTranslate`) | translated line | none | "seems to miss some text" comment → forced single-line | Yes if self-hosted |
| Crisp MADLAD | `CrispAsrMadladTranslate.cs` | Crisp runtime | one CLI process per line (always single-line) | stdout parse | none | process-per-line cost | Yes (local) |

## 10.3 Context strategy (REPOSITORY_FACT, detailed)

- **Classic engines:** none beyond the editable per-engine prompt (all default to `"Translate from {0} to {1}, keep punctuation as input, keep line breaks exactly the same, do not censor the translation, give only the output without comments:"` — `ToolsSettings.cs:155–160`).
- **Advanced local engines:** rolling window of `HistoryPairs` (default 12) previous {source,target} pairs; explicit instruction "use them only for context and consistency (names, pronouns, formality), do not re-translate them" (`ProtocolText`); **synopsis** ("About the content being translated"), **glossary** ("always translate these terms exactly as given", `source = target` lines), **formality** (formal/informal sentence), **KeepLineBreaks** sentence; system prompt deliberately stable-parts-first for llama-server `cache_prompt` KV reuse; **single-line calls go context-free** because TranslateGemma echoed history into lone-line outputs (comment).
- **What does not exist:** previous/next *untranslated source preview as sliding source context for classic engines*, scene-level context, speaker/character conditioning, translation memory, per-episode glossary files, QA gates. (Verified by absence: grep `TranslationMemory|translation memory` → no matches in `src`.)

## 10.4 Model selection, download, rate limits, caching (REPOSITORY_FACT)

- Curated local translate models in `LlamaCppServerManager.TranslateModels`: TranslateGemma 4B (Q4_K_M 2.5 GB / Q5 2.8 GB / Q8 4.1 GB), 12B (Q4 7.3 GB / Q5 8.5 GB) — HF GGUF repos (SandLogicTechnologies, NikolayKozloff) with `ChatTemplate:"gemma", NoJinja:true`; MiLMMT-46 4B Q4/Q8 (Xiaomi 2026, `PromptTemplate` + `Temperature:0` + `CompletionOnly:true`); Hy-MT2 family w/ Tencent official prompt template; per-model recommended sampling; `NoThinking` for Gemma-4-style models; `ChatTemplate/NoJinja` overrides; `ServerArguments`/`ServerArgumentsOnly` escape hatch (#13830/#13865).
- **Model-manager infrastructure:** `LlamaCppServerManager` starts `llama-server` (port pick, health probe, kill-on-exit); downloads via `DownloadHelper` (**HTTP Range resumable**, 206 validation, Content-Range totals — lines 57–192); integrity awareness via `DownloadHashManager` (SHA-256 registry → Unknown/UpToDate/UpdateAvailable states).
- **Rate limiting:** `RequestMaxBytes`, `RequestDelaySeconds` in `SeAutoTranslate` (consumed by merge logic — max text size parameter of `MergeMultipleLines`).
- **Caching:** none for translations (no disk cache/TM found); llama-server side benefits only from prompt-prefix KV reuse.

## 10.5 Post-processing & line handling (REPOSITORY_FACT)

- Tag protection: `Formatting.cs` strips/restores formatting around engines (ASSA blocks for classic engines); `StrippedLine` for advanced path; `AssaTagStripper` (+tests).
- Merge/split: `MergeAndSplitHelper` (1,460 L): sentence-period counting with per-language abbreviations (`AbbreviationsForLanguage` delegate), `MergeMultipleLines` (maxTextSize, noSentenceEnding flags, `joinContinuousRowsWithLineBreak`), `SplitMultipleLines` (length/duration heuristics), `RebalanceLines(text, target)`; `LanguagesAllowingLineMerging` allowlist in `Formatting.cs`.
- Cleanup: `ChatGptTranslate.RemovePreamble` (preamble regex + `<think>` blocks, cut-off reasoning → empty → retry), `DecodeUnicodeEscapes` (models emitting `\uXXXX`), quote-trim heuristic, `FixNewLines`.
- Retry/failure UX: `TranslationErrorWindow`/`ViewModel`; "translate again" prompt path (1210 region).
- QA: none automated post-translation (no length/CPS gate on translated text before apply — INFERENCE from absence in the loop; *editor-side* limit tools exist separately, e.g., `ApplyDurationLimits`, `Control_CHARS`-style fixers in libse — different subsystem).

## 10.6 Explicit gaps for the Arabic product (REPOSITORY_FACT of absence + INFERENCE)

1. No translation memory / no persistence of translated pairs (grep clean).
2. No glossary beyond the advanced-LLM free-text box (not file-based, not per-project).
3. No style profiles; dialect target is just a language code string.
4. No per-line metadata conditioning (Actor/Style exist in the model but are not fed to engines).
5. No automated QA pass (length/CPS/punctuation/bidi) between translation and apply.
6. No checkpoint/resume artifact for long jobs.


---

# 11. Arabic-First Audit

Method: full-file reads of every Arabic/RTL hit found by grep (30 files, 243 `RightToLeft` matches), plus targeted absence-checks. Every claim below carries its evidence.

## 11.1 Unicode / encoding (REPOSITORY_FACT)

- Encoding detection is a first-class subsystem (`src/libse/DetectEncoding/`); `Subtitle.LoadSubtitle(..., out Encoding encoding, Encoding useThisEncoding)` returns the detected codepage; `Encoding.RegisterProvider(CodePagesEncodingProvider)` at startup enables legacy codepages (CHK needs cp850 — `Program.cs` comment). Arabic legacy codepages (e.g., Windows-1256) are handled by the generic detection path — specific cp1256 coverage ⚠️ not individually verified (NOTED, see §34).
- RTL/bidi control characters are explicitly managed: `Utilities.FixRtlViaUnicodeChars` wraps each line in U+202B…U+202C, markup-aware (skips `{...}` ASSA blocks at line start/end, skips `{\\p1}` drawings, idempotent — removes previous markers first; comment documents #14150). Counterpart removal commands exist ("Remove RTL Unicode tags" — `LanguageGeneral.cs:1327`).

## 11.2 UI RTL mode (REPOSITORY_FACT — `RightToLeftHelper.cs`, full read)

- `Se.Settings.Appearance.RightToLeft` (ui) and `Configuration.Settings.General.RightToLeftMode` (libse) both exist — two flags for related purposes (editor mirroring vs. format-level reversal); both verified present. The duplication is a confusion risk (INFERENCE).
- `SetRightToLeftForDataGridAndText` recursively applies `FlowDirection` but deliberately skips `ComboBox`, `NumericUpDown`, `TimeCodeUpDown`, `SecondsUpDown` (numeric/time inputs stay LTR).
- Text boxes **follow content direction**: `GetContentDirection` flips to RTL when `LanguageAutoDetect.ContainsRightToLeftLetter(text)` — so an Arabic line inside an LTR-mode editor still renders RTL; empty boxes keep the mode's side so typing starts correctly.
- The main edit grid is physically mirrored by **reversing `ColumnDefinitions` and remapping child columns + swapping asymmetric margins** (`MirrorTextEditGrid`, issue #12249) — FlowDirection alone doesn't rearrange a Grid, a real Avalonia constraint documented in-code.
- Per-line mixed content: grid cells get per-line flow direction converters (`TableViewTextCellFlowDirectionTests`).
- RTL caret/selection semantics implemented in `MainViewModel` (~lines 30388–30860): visual Left/Right inversion, word-wise moves, per-line RTL detection — a substantial investment (verified excerpts).
- Shortcut: `ToggleRightToLeft` (`LanguageSettingsShortcuts.cs:443`); SE4 shortcut migration maps `MainEditReverseStartAndEndingForRTL` (`Se4ShortcutsImporter.cs:94`).

## 11.3 Rendering / shaping (REPOSITORY_FACT)

- Text shaping is delegated to the Avalonia/Skia+HarfBuzz stack (the repo pins `SkiaSharp.HarfBuzz` to managed HarfBuzzSharp 8.3.1.5 to avoid an ABI crash on Linux — `UI.csproj` comment, issue #11864). Arabic shaping is therefore handled by HarfBuzz in the editor and preview.
- Waveform/graphics text has **explicit bidi run handling**: `SkiaBidiRuns.cs` + `SkiaWaveformRenderer.cs` in `AudioVisualizerControl`, with tests (`SkiaBidiRunsTests`, `AudioVisualizerRtlTextTests`, `SkiaTextCacheBidiTests`) — measured draw order for RTL is respected.
- Image-based export (`libuilogic/Export/ImageRenderer.cs` ~496–517): measures segments "in the SAME order the line is drawn in" and **reverses segments for RTL languages** — with a comment explaining a past padding-measure bug on RTL lines.
- Burn-in via ffmpeg `ass=` filter → rendering is libass's (bidi via FriBidi upstream) — the app writes the ASSA file and lets libass shape; SE-side responsibility is producing clean text (see 11.1). ⚠️ End-to-end Arabic burn-in pixel output not verified in this audit (no ffmpeg in sandbox) — LIMITED.

## 11.4 Timing / width / CPS for Arabic (REPOSITORY_FACT)

- `TextLengthCalculator` has Arabic-aware `ICalcLength` implementations: `CalcIgnoreArabicDiacritics` (excludes U+064B–U+0653 harakat, bidi controls, ZWSP/ZWNBSP), `CalcIgnoreArabicDiacriticsNoSpace`, `CalcIncludeCompositionCharacters(+NotSpace)` — plugged via `CalcFactory`, which references Arabic (verified file list). This makes CPS/duration math correct for vocalized Arabic.
- `NoBreakAfterItem`/abbreviation lists and `Dictionaries/ar_NoBreakAfterList.xml` exist for line-break decisions.

## 11.5 Legacy format-level Arabic (REPOSITORY_FACT)

- `Pac.cs`: `GetArabicBytes(...)` + `Utilities.FixEnglishTextInRightToLeftLanguage(text, "0123456789abc…XYZ")` on read/write — visual-order handling for the legacy PAC format incl. digit/Latin reversal.
- `Cavena890.cs`: reversal gated on `RightToLeftMode` (write-side `ReverseStartAndEndingForRightToLeft`).
- `Ebu.cs` references Arabic (STL); `Idx.cs` inserts U+200E for RTL scripts in VobSub IDX comments.
- `Utilities.ReverseStartAndEndingForRightToLeft` performs *visual* reversal (pre/post punctuation, tags, ♪ handling) — a legacy technique that **conflicts conceptually** with Unicode-mark approaches (11.1). Both coexist by design (different renderers/formats). Confusion risk for users (INFERENCE), but each has UI wording ("Fix RTL via Unicode tags" vs "Reversed start and endings…").

## 11.6 Spell check, search, OCR, fonts (REPOSITORY_FACT + gaps)

- Spell check: Hunspell; `src/ui/Assets/HunspellDictionaries.json` includes LibreOffice `ar/ar.aff`, `ar/ar.dic` download URLs (verified); in-repo `Dictionaries/ar_NoBreakAfterList.xml` verified. Arabic dictionary *download* path exists; on-device Arabic spell-correct quality not verified (dictionary content external).
- **Search:** no Arabic normalization found — grep for `RemoveDiacritics|NormalizeArabic|alef` in `FindService.cs`/`StringExtensions.cs` → no matches. Searching for "اكتب" won't match "اﻛﺘﺐ"-shaped presentation forms or hamza-variant differences (أ/ا/إ) or tashkeel-bearing text. ACTUAL CURRENT LIMITATION (absence verified in the two inspected files; broader search-normalization elsewhere not found).
- OCR: `Ocr/` ships **Latin-only** templates (`Latin.db`, `Latin.nocr`); `PaddleOcr.cs` mentions Arabic (model list — `PaddleOcrModels.cs` includes Arabic). So image-subtitle OCR for Arabic depends on Paddle models (download), not the built-in NOcr DB. ACTUAL LIMITATION for offline-first Arabic OCR.
- Fonts: no Arabic-specific fallback configured in `Program.cs` (fallbacks there are CJK-only; Arabic falls to system fontconfig/system default). On minimal Linux installs without Arabic fonts, Arabic may render as boxes — same class of problem documented for CJK (#11355). ACTUAL CURRENT LIMITATION (Linux minimal installs) — macOS/Windows fine.

## 11.7 UI language & dictionaries (REPOSITORY_FACT)

- `src/ui/Assets/Languages/Arabic.json`: **4,043** translated strings vs **~4,066** English keys (grep counts) ≈ 99.4% — an essentially complete Arabic UI. Verified by counting.
- Whisper ASR language list includes `("ar","arabic")` (WhisperLanguage.cs:58). No `arz` in the generic Whisper list (only via CrispAsrOmni, §12.2).

## 11.8 Arabic weaknesses triage (each: ACTUAL CURRENT LIMITATION vs POSSIBLE FUTURE IMPROVEMENT)

| # | Item | Classification | Evidence |
|---|---|---|---|
| 1 | No diacritic/hamza-insensitive search & replace | ACTUAL LIMITATION | §11.6 grep; FindService inspected |
| 2 | No Arabic OCR template DB bundled (Latin only); Paddle path is a download | ACTUAL LIMITATION | §11.6 |
| 3 | Two overlapping RTL flags (UI mirror vs format reversal) | ACTUAL LIMITATION (UX confusion) | §11.2 |
| 4 | Visual-reversal legacy behaviors (PAC/Cavena) can corrupt modern Unicode workflows if misused | ACTUAL LIMITATION (by design of legacy formats) | §11.5 |
| 5 | No Arabic font fallback on minimal Linux | ACTUAL LIMITATION | §11.6, #11355 precedent |
| 6 | Translation engines receive no Arabic-specific instructions; Arabic punctuation (،؛؟) left to model | ACTUAL LIMITATION (prompt defaults are punctuation-neutral; "keep punctuation as input" default actually *pushes* Latin punctuation into Arabic output) | `ToolsSettings.cs:155–160` |
| 7 | No Arabic-aware CPS warning presets shipped (calculator exists; whether Arabic presets/profiles auto-select by language not verified) | PARTLY UNKNOWN | CalcFactory inspected; preset selection path ⚠️ |
| 8 | Diacritics inflate `Text` but are correctly excluded from CPS | solid base | §11.4 |
| 9 | Egyptian Arabic as a translation target (`arz`) only in 3 engine language lists; not surfaced as a styled option | ACTUAL LIMITATION | §12.2 |
| 10 | Arabic translation QA (bidi tag hygiene, punctuation conversion ،؛؟, Arabic quotes «») absent | ACTUAL LIMITATION | absence verified in translate loop |
| 11→16 | Ideas for improvement (auto bidi-tag cleanup on export, Arabic quote normalization, tashkeel removal helper, Arabic number-to-words QA, dialect glossary, Arabic font pack) | FUTURE IMPROVEMENTS | §24 |

---

# 12. Egyptian Arabic Architecture (design, grounded in the audited code)

## 12.1 What "Egyptian Arabic" must mean here (EXTERNAL_EVIDENCE + INFERENCE)

`arz` (Egyptian Arabic) is a distinct ISO code; NLLB-200 ships `arz_Arab`; dialect intelligibility/registers differ from MSA in lexicon, syntax, humor, and register. Published ASR reality: whisper-large-v3 WER ≈ 59% Egyptian vs ≈ 28% MSA (arXiv 2412.13788), and LLM post-correction reduces Egyptian WER (IU journal 2026, mean Δ 0.183). Any Egyptian pipeline must therefore (a) choose the target register deliberately, (b) expect to *correct* ASR output for Egyptian speech, and (c) not treat `ar` and `arz` as interchangeable strings.

## 12.2 What the repository already supports (REPOSITORY_FACT)

| Capability | Where | Note |
|---|---|---|
| `arz` as translation target code | `ChatGptTranslate.ListLanguages` ("Egyptian Arabic","arz"), `CrispAsrMadladLanguages` (arz), `NoLanguageLeftBehindServe` (arz_Arab) | LLM engines inherit this list too (`AdvancedTranslatorBase.GetSupportedTargetLanguages → ChatGptTranslate.ListLanguages`) |
| Egyptian ASR | `CrispAsrOmni.cs`: `("arz_Arab","arabic (egyptian)")` | one engine, one model family |
| Egyptian TTS | `OmniVoiceLanguages.cs`: `("Egyptian Arabic","arz")`, `("Eastern Egyptian Bedawi Arabic","avl")` | dubbing path |
| Dialect conditioning hooks | `SeLlamaCppAdvanced.Prompt/Synopsis/Glossary/Formality` → `BuildSystemPrompt` | the only dialect levers today |
| Batch convert RTL fixing | `BatchConvertFunctionType.FixRightToLeft` | post-translation hygiene exists mechanically |

## 12.3 Where dialect behavior belongs (design, mapped to real extension points)

| Stage | Belongs here? | Mechanism on current architecture | Classification |
|---|---|---|---|
| Preprocessing (source) | Partly | expand slang/idiom glossary into engine prompt; keep `StrippedLine` tag hygiene as-is | SUPPORTED (glossary exists) |
| Context assembly | Yes — central | `LlamaCppAdvancedProtocol.BuildSystemPrompt`: add a **Style profile block** after glossary (stable-ish; still after KV-stable prefix) | SUPPORTED (additive) |
| Translation call | Yes | per-language prompt override: today every engine's prompt is generic; add Arabic/Egyptian prompt defaults per target code (`{1}`-aware) | SUPPORTED (data + small code) |
| Speaker conditioning | **Not supported** | requires: per-line speaker detection (diarization exists for some ASR engines e.g. MossDiarize/Crisp), mapping speaker→character name/style, injecting per-speaker glossary/voice notes into batch protocol (`history`/`lines` schema unchanged; add `speakers` array) | REQUIRES ARCHITECTURAL CHANGE (new pipeline data + protocol field + UI) |
| Style profiles (named, shareable) | **Not supported** | `SeLlamaCppAdvanced` is singleton global; needs a profile store + selector in Translate window | REQUIRES ARCHITECTURAL CHANGE (moderate: new settings class + files + picker) |
| Translation memory | **Not supported** | new: persist {source,target,lang,model} per line hash; reuse on re-runs; feeds context | REQUIRES ARCHITECTURAL CHANGE (new store + lookup in loops) |
| Post-processing | Yes | new Arabic post-pass: Latin `?,;:` → `؟،؛` where appropriate; quote normalization; bidi-tag cleanup; tashkeel policy (strip/keep) — implement as `IBatchContextTranslator`-independent row transformer after each batch, or as batch-convert functions | SUPPORTED (new pure functions + one hook) |
| LLM review pass | Partly exists | `Features/Tools/AiReview` + `AiAssistant` (LLM proofread via llama.cpp/Ollama — docs/overview + folder verified); dialect-aware review = prompt/profile work | SUPPORTED (prompt-level) |
| QA gate | **Not supported** | new: length/CPS re-check with Arabic calculators (exist), bidi control-char balance check, glossary-compliance scan, punctuation-locale check | REQUIRES NEW CODE (pure logic; low risk) |
| Glossary (file-based, per project) | **Not supported** (single global textbox) | new: project-level glossary files (e.g., JSON next to subtitle), loaded into `Glossary` | REQUIRES ARCHITECTURAL CHANGE (small) |

## 12.4 Egyptian-specific linguistic concerns (design constraints; HYPOTHESIS where noted)

- **Register mixing:** default MSA output will read stiff for dialogue; the *style profile* must instruct conversational Egyptian (e.g., use of بـ prefix forms, يا addressing, common contractions) without slang overload — HYPOTHESIS: prompt-level control will get 80% there with modern LLMs; measurable only via eval set (§21 research supports LLM sensitivity to such instructions).
- **Idioms/humor/sarcasm:** literal MT fails; the advanced engine's synopsis field is the existing vehicle for "this scene is sarcastic" — needs to become per-scene/per-line notes (schema addition).
- **Profanity:** default prompts already say "do not censor" (all engines) — good baseline for authentic Egyptian dialogue; REVIEW pass should allow per-profile strength.
- **Gender/pronouns:** English "you/she" → Egyptian gendered verbs/adjectives; history window (12 pairs) helps short-range consistency; long-range character gender needs the speaker-conditioning stage above.
- **Names/URLs/technical terms:** `StrippedLine` protects tags, not names; glossary covers names; URLs/emails should be added to the "keep untranslated" predicate next to music lines (`IsKeptUntranslated`) — RECOMMENDATION (small, safe).
- **Numerals:** Egyptian subtitles conventionally use Arabic-Indic (٠-٩) or Western digits depending on publisher; add a style-profile numeral policy + post-pass converter — RECOMMENDATION.
- **Cultural adaptation:** beyond scope of code; belongs in glossary + synopsis + human review. Not automatable safely (HYPOTHESIS).

## 12.5 Capability classification summary

- **SUPPORTED BY CURRENT ARCHITECTURE:** arz target codes; local-LLM advanced batch translation w/ glossary/synopsis/formality; local ASR incl. one Egyptian model; music-line skipping; RTL fixing passes; Arabic CPS calculators; AI-review prompt tweaks.
- **REQUIRES ARCHITECTURAL CHANGE (moderate):** style profiles; project glossary files; translation memory; Arabic post-processing pass; QA gate; per-target-language prompt defaults.
- **REQUIRES ARCHITECTURAL CHANGE (larger):** speaker diarization→character mapping → per-speaker conditioning; scene/shot context assembly (shot-change detection exists for timing — could anchor scene boundaries); checkpoint/resume job store.
- **REQUIRES NEW MODEL CAPABILITY (external):** better Egyptian ASR (fine-tuned models; benchmark evidence shows the gap); Arabic-eg credit/terms dictionaries; dialect ID to auto-select Egyptian vs MSA (CAMeL Tools ADIDA is MIT-licensed Python — EXTERNAL_EVIDENCE; integrating a Python runtime is a real cost, see §20).


---

# 13. Beginner UX Audit

Simulated user: *"I have an English video. I want an Arabic subtitle or an Arabic-subtitled video. I know nothing about subtitle software."*

Method: each pain point below was checked against the actual windows/options code. "CURRENT UX" = what the code shows a user sees today.

| # | Stage | CURRENT UX (evidence) | USER CONFUSION | WHY IT HAPPENS | PROPOSED UX REQUIREMENT |
|---|---|---|---|---|---|
| 1 | Where to begin | Entry points are scattered: `Video ▸ Speech to text` window (`Features/Video/SpeechToText`), `Auto-translate` (`Features/Translate`), `Video ▸ Burn-in` (`Features/Video/BurnIn`) — three separate windows, no connector | Doesn't know the chain STT→translate→burn-in even exists; tries to find "make subtitles" and finds an editor | Feature-oriented menu mirrors pro workflows; no task-oriented entry | A "Translate a video" task entry that owns the whole chain (§14) |
| 2 | Transcription vs translation | STT window and Translate window share nothing; beginner must know to run STT first, save, open translate | "I clicked translate — why is nothing translated?" (there's no source text yet) | Pipeline implicit in the user's head, not in the app | Pipeline state: the job knows whether source text exists and offers the next step |
| 3 | Engine choice | STT engine picker exposes **30+ engines** (`WhisperChoice` constants + Crisp ASR family + cloud): "OpenAI", "CPP", "CPP cuBLAS", "CPP Vulkan", "WhisperX", "Const-me", "CTranslate2", "stable-ts", "Purfview's Faster-Whisper-XXL", "Qwen3 ASR CPP", "Parakeet.cpp", 16 "Crisp ASR *", "OpenAI Compatible", "OpenRouter", "Alibaba Qwen3-ASR", "Google Cloud Speech-to-Text" | "What is CTranslate2? Vulkan vs cuBLAS?? Which one is *good*?" | Engineering taxonomy leaked into UI | Beginner mode: one recommended local engine + one cloud option, one line of explanation each; advanced mode keeps all |
| 4 | Model choice & size | Model lists per engine with size strings (e.g., curated GGUF sizes "2.5 GB"; `ISpeechToTextEngine.DownloadSizeText` "~8 MB – 692 MB" ranges for Crisp) | Doesn't know tiny/small/medium/large trade-offs, nor disk impact before download | Sizes present but consequence (disk, RAM, speed) not | Size + "runs on your PC? [yes/no/likely]" chips + disk-free check before download |
| 5 | Internet requirements | No single at-a-glance online/offline state; cloud engines (OpenAI-compatible, OpenRouter, DashScope, Google Cloud, DeepL, ChatGPT…) sit beside local ones in the same combos (`AutoTranslateCombos`, `WhisperChoice`) | "Will this send my movie to someone?" | Trust boundary documented in README, not shown in-flow | Per-step LOCAL/ONLINE badge; explicit consent on first cloud use (SKILL.md trust boundary honored) |
| 6 | Source language | Whisper language list defaults/`--language` param; auto-detect exists | Usually OK, but if auto-detect misfires (accented audio), beginners have no idea output language can silently flip | Silent auto-detect | Show detected language prominently + override |
| 7 | Target language / MSA vs Egyptian | Translate window language combo lists "Arabic" and (for LLM/NLLB/Madlad engines) "Egyptian Arabic" as raw list items among ~150 languages | A Cairo user won't know MSA (فصحى) vs Egyptian (عامية) or which to pick; most engines list *only* "Arabic" (e.g., DeepL `ar`) — choosing Egyptian silently unavailable there | Language codes ≠ product concept; register choice invisible | Arabic target selector: "Modern Standard Arabic / Egyptian Arabic (spoken style)" with availability per engine and graceful fallback messaging |
| 8 | Progress & feedback | STT: job list + status; translate: per-row progress + coalesced counter (`EnqueueTranslateProgress`); burn-in: frame= progress + ETA | Cross-window: three progress bars in three places, no overall % | No job concept spanning stages | One job view: "Transcribing (45%) → Translating (12/350) → Rendering (67%)" |
| 9 | Errors | Engine errors land in `Error` string → `TranslationErrorWindow` (raw JSON/body shown in many cases), logs to `error-log.txt` | Raw API JSON is meaningless to a beginner | Error text is engine-authored | Human-phrased causes + fixes ("API key invalid — add key in Settings ▸ …"), with "copy technical details" expander |
| 10 | Preview | Editor has video preview + burn-in preview window ("Render the preview with ffmpeg/libass (same engine as the generated video), debounced" — `BurnInViewModel.cs:2578`) | Excellent once found; discoverability of *preview before export* is low | Preview lives inside Burn-in dialog | Show RTL-correct live preview inside the pipeline at review step |
| 11 | Subtitle file vs burned video | Save (any format) vs Burn-in encode are different dialogs; both powerful (codec/preset/CRF…: `BurnInSettingsViewModel`, `VideoEncodingItem`, `OutputContainer`) | "I just want a video with subtitles on it" → meets an encode settings wall | Pro encode options first-class | Beginner: two buttons ("Save .srt", "Save video with subtitles"); advanced: full encode panel |
| 12 | Output location | Save dialogs + tools log; burn-in writes next to chosen output (`BurnInJobItem.OutputVideoFileName`) | Mild; standard file dialogs | — | "Show in folder" affordance after completion |

**Net verdict (INFERENCE):** every *piece* exists and several are excellent; the *journey* requires expert knowledge of the chain. The pipeline object proposed in §14/§30 is the single highest-value UX fix.

---

# 14. Proposed Modern UX (design)

**Principles:** task-first for beginners, zero loss of expert power (SKILL.md: "Keep UI changes consistent with existing workflows… do not redesign screens as part of a bug fix" — so the design adds a layer, it does not replace editor workflows). RECOMMENDATION.

## 14.1 Two modes, one app

- **BEGINNER MODE (new "Translate Video" task):** a wizard-style job flow with defaults chosen by hardware detection.
- **ADVANCED MODE:** today's windows, unchanged; the wizard exposes "Open in advanced view" links that jump into the corresponding existing window pre-filled with the job's parameters (keeps parity, avoids duplicated settings code — the wizard *drives the same services*).

## 14.2 The Translate Video workflow (ideal path)

1. **Select video** — drop file(s) or URL (yt-dlp already integrated). Multi-file → job queue (reuses `SpeechToTextJobItem`-style list).
2. **Source language** — auto-detect shown with confidence; override combo. If Arabic-ish audio detected, offer "Egyptian Arabic transcript" when engine supports it (CrispAsrOmni today).
3. **Target language & style** — big selector: Arabic (MSA) / Arabic (Egyptian — conversational). Style chips: Neutral / Casual / Formal + "cinematic" toggle (slang tolerance, keep humor). Availability badges per engine.
4. **Quality / speed** — three presets mapping to real parameters: *Fast* (small local model, e.g., TranslateGemma-4B Q4; bigger batches), *Balanced* (12B Q4), *Best* (cloud LLM or largest local). Shows est. model size, disk needed (existing `DownloadSizeText`/`Size` strings), and time hints (HYPOTHESIS — needs measurement).
5. **Privacy / local-online** — explicit radio: "On this device (no internet)" vs "Cloud (sends text to provider)". Selecting cloud shows provider name + a one-time consent note. No silent fallback in either direction (SKILL.md boundary). Engine picker filtered accordingly.
6. **Review** — side-by-side grid (exists: original/translation columns) with RTL-correct text rendering (exists), inline AI-review ("Tools ▸ AI review" exists) pre-scoped to changed lines, QA panel (CPS/length — Arabic-aware calculators exist), glossary hit list.
7. **Export** — "Save subtitles (.srt/.ass/…)" and/or "Video with burned subtitles" (Burn-in service, libass preview). Beginner defaults; "Advanced…" expands today's full panels.

**Job object:** a persisted `TranslationJob` (files, stage states, parameters, timestamps) enabling resume after crash/close (see §30). HYPOTHESIS for storage: JSON next to output or in app-data job folder — matches `Settings.json` patterns.

---

# 15. Local-First / Low-Bandwidth Audit

## 15.1 What already exists (REPOSITORY_FACT)

| Proposal area | Current support | Evidence |
|---|---|---|
| Strict offline mode | Fully local chains exist: whisper.cpp/Const-me/CTranslate2/Faster-Whisper-XXL ASR; llama.cpp server + curated GGUF translate models; Ollama; Crisp MADLAD/NLLB-local; local TTS runtimes. App has no telemetry (grep negative §19). | §6.2, §10.2 |
| Resumable downloads | `DownloadHelper` implements HTTP Range resume (checks `Accept-Ranges`, 206 validation, Content-Range totals, appends, doesn't reset position on retry) | `DownloadHelper.cs:57–192` |
| Version/integrity awareness | `DownloadHashManager` SHA-256 registry → Unknown/UpToDate/UpdateAvailable; WhisperX built from pinned ref of a standalone repo (build reproducibility comments) | `DownloadHashManager.cs`; `WhisperDownloadService.cs` comments |
| Model size preview | `ISpeechToTextEngine.DownloadSizeText` (platform-aware ranges); curated model `Size` strings ("2.5 GB") | `ISpeechToTextEngine.cs`, `LlamaCppModel.Size` |
| Engine self-managed models | `DownloadsOwnModels` flag (WhisperX pulls into HF cache; SE must not duplicate) | `ISpeechToTextEngine.cs` comment |
| Custom models | `SupportsCustomModels`/`ImportCustomModel` (whisper.cpp, XXL, CTranslate2) | same file |
| Download-manager UX | dedicated download windows per domain (`DownloadSpeechToTextEngine/Models`, `DownloadLlamaCpp`, TTS/OCR variants) | folder listings |

## 15.2 Gaps (REPOSITORY_FACT of absence unless noted)

1. **Disk-space preview before download** — not found (sizes shown, free-space check not).
2. **No global online/offline indicator** or per-engine network taxonomy in one place.
3. **No silent-cloud-fallback audit:** none found in translate loop (engines throw on failure rather than switching — verified `DoAutoTranslate`); still worth an explicit product statement. UNKNOWN: whether *any* engine internally falls back to a second endpoint (DeepLX→DeepL etc.) — engines are independent by code read; labeled UNKNOWN-for-all-36 (8 audited deeply).
4. **No lightweight-model guidance** (which model for which hardware) beyond size strings.
5. **Model reuse across features:** llama-server shared between translate/OCR/AI-review (same manager) — good; ASR models are per-engine folders (no dedup across whisper variants — INFERENCE from folder naming in `GetAndCreateWhisperModelFolder`).
6. **Bandwidth-throttle for downloads:** not found.

## 15.3 Mapping proposals → architecture (RECOMMENDATION, each tied to a real hook)

- Offline indicator + consent → new status service reading engine metadata (`CanBeDownloaded`, cloud flags) — no behavior change.
- Disk-space gate → `DownloadHelper` pre-check `DriveInfo` + size string (exists) → confirm dialog.
- Hardware-aware recommendation → one-time probe (RAM/GPU via existing platform checks; SE already picks CUDA/Vulkan/CPU variants for Crisp — precedent in `CrispAsrDownloadService`).
- Throttle → `HttpClient` per-read delay in `DownloadHelper` (single choke point).
- Model cache dedup/cleanup → extend `DownloadHashManager` registry with sizes/last-used; a "Models" management page (data all exists).


---

# 16. Performance Audit

**No SDK in sandbox → no measurements.** Classification per claim: CONFIRMED (code/logic evidence, no timing) / LIKELY (architectural inference) / UNKNOWN (needs profiling).

| Area | Finding | Class | Evidence |
|---|---|---|---|
| Startup | Format warm-up moved off UI thread; measured 40–90 ms JIT cost documented in-code | CONFIRMED (author-measured, quoted in comment) | `Program.cs` comment |
| Startup | DI builds + Avalonia parse on UI thread remain (by design, parallel to warm-up) | LIKELY (fine) | `Program.cs` |
| Editor responsiveness | `MainViewModel.cs` 34,977 L with broad responsibilities → compile-time and cognitive hotspots; UI update paths already use coalescing (`CoalescedUiUpdateQueue`) | LIKELY (risk, not proven bottleneck) | §5.2; Logic listing |
| Translation throughput | Merge-translates whole sentences → fewer HTTP calls (bounded by `MaxCharacters` per engine, `RequestMaxBytes`); advanced engine: batch 10 default, clamped 1–50; history window 12 pairs; **prompt ordered stable-first for llama-server KV-cache reuse** (explicit design) | CONFIRMED (design) | `DoAutoTranslate`, `AdvancedTranslatorBase`, `LlamaCppAdvancedProtocol` header |
| Translation throughput | Classic path re-encodes full request JSON manually per call; retry backoff {2.5,5,9}s | CONFIRMED (code) | `ChatGptTranslate.cs` |
| Translation failure cost | Bisect-on-bad-batch halves work rather than retrying whole batch | CONFIRMED | `TranslateChunkAsync` |
| ASR | 16 kHz mono wav pre-extraction; sequential job loop (one video at a time) | CONFIRMED (structure) | §9-A/E |
| ASR long video | Chunking only for upload-capped online engines (24 MB threshold, silence-snap); local engines: whole-file wav handed to engine | CONFIRMED for online; UNKNOWN RAM footprint for local engines | `OpenAiSttChunker.cs`; §9-E |
| Burn-in | Two-pass with null-device analysis; per-pass timer reset; `frame=` progress parsing; kill+wait teardown | CONFIRMED (structure) | `BurnInViewModel` |
| Downloads | Streaming + Range resume; SHA registry | CONFIRMED | §15.1 |
| GPU | Native acceleration present per engine (CUDA/Vulkan/Rocm builds; `-ngl` llama flag; Crisp variant pick by GPU) — selection is per-engine, no global GPU advisor | CONFIRMED (existence); UNKNOWN (optimal choice guidance) | §6.2, §15.3 |
| RAM | Curated model table ships sizes; no runtime memory guard before model load | LIKELY gap | `LlamaCppModel.Size`; no DriveInfo/RAM check found |
| Disk IO | Asset zips deflate 6.8 MB of language JSONs (~5 MB exe saving) — build-time optimization documented | CONFIRMED | `AssetZips.targets` |
| Benchmarks | 55 benchmark files (`PerfHuntRound*` incl. real-file benchmarks) — but **not runnable in this sandbox** (no SDK; CI skips benchmarks) | UNKNOWN (results) | `tests/benchmarks` listing; `tests.yml` comment "benchmark projects are not test projects and are skipped" |
| Concurrency | UI-row writes marshaled; translation loop is sequential per subtitle; batch convert iterates files sequentially (structure read); no parallel-translation option found | CONFIRMED (sequential); LIKELY opportunity | §9; §27 P-ideas |

---

# 17. Reliability / Failure Audit

Each entry: FAILURE — TRIGGER — MECHANISM — USER IMPACT — EVIDENCE — MITIGATION (proposed).

1. **Runaway translation loop** — engine returns empty repeatedly — `noProgressCount>3` throws (`DoAutoTranslate`) — job stops with error dialog — REPOSITORY_FACT `DoAutoTranslate.cs` — already mitigated; add engine health-check before start (proposed §27).
2. **Reasoning-model truncation** — `<think>` without close (token budget) — `RemovePreamble` returns empty → retried as no-progress — wasted tokens then error — REPOSITORY_FACT `ChatGptTranslate.RemovePreamble` comment — mitigation exists (empty→retry); `NoThinking` model flags & chat-template overrides for known families (`LlamaCppModel.NoThinking`).
3. **Bad batch from small local model** — schema-violating reply — retry once → bisect → throw — partial batch results still written for successful halves — REPOSITORY_FACT `AdvancedTranslatorBase` — good; consider per-batch checkpointing (§27).
4. **ffmpeg burn-in hang/failure** — version drift, bad args — exit-code check + tools log of exact command + kill+wait(3 s) + output-file verification — diagnosable but terminal for job — REPOSITORY_FACT `BurnInViewModel.StartFfmpegProcess/KillFfmpegProcess` — proposed: auto-retry without optional filters.
5. **Two-pass prompt resets** — "prompt for ffmpeg parameters" re-prompts for pass 2/batch files; cancelling any aborts whole run (documented in-code) — REPOSITORY_FACT — acceptable; document in UI.
6. **Headless test contamination** — Avalonia dispatcher crash poisons later tests — CI retries once in fresh process; real failure blocks merge — REPOSITORY_FACT `tests.yml` comments — already mitigated.
7. **Race on grid rows** — background thread writing bound rows — all audited paths marshal via `Dispatcher.UIThread` — would corrupt UI if new code skips it — REPOSITORY_FACT + INFERENCE — new engines must follow the pattern (SKILL.md reviewable).
8. **KV-cache regression** — prompt prefix destabilized by frequently-changing parts — comments document ordering fix — slower translation only — REPOSITORY_FACT `LlamaCppAdvancedProtocol` header — keep stable-first ordering invariant (§5.8).
9. **Partial completion across pipeline** — app close mid-translation loses grid state (no disk checkpoint found) — REPOSITORY_FACT-of-absence — proposed job checkpoint (§30).
10. **External process spawn failures** — missing engine/binary — `IsEngineInstalled()` gates + `MissingSharedLibrary.cs` helper + in-app downloads — REPOSITORY_FACT — good; per-engine diagnostics page proposed (§27).
11. **Download corruption/partial** — network drop — Range resume + SHA registry state machine — REPOSITORY_FACT — good.
12. **Settings corruption** — `Settings.json` write failure — `AutoBackupService` backs up Settings.json on interval — REPOSITORY_FACT `AutoBackupService` — good.
13. **Crash resistance** — UI-thread exceptions logged & swallowed (`e.Handled=true`) — possible degraded state after swallow — REPOSITORY_FACT `Program.cs` — trade-off documented (#11515); acceptable.
14. **Format edge cases** — 300+ parsers; suite size (181 libse test files) implies coverage, specific unknowns — UNKNOWN per format — out of scope.

---

# 18. Testing Audit

Inventory (REPOSITORY_FACT): 1,022 test `.cs` files (~155k LOC): `tests/libse` 181, `tests/libuilogic` 88, `tests/seconv` 53, `tests/UI` 645 (headless Avalonia; `xunit.runner.json` present), `tests/benchmarks` 55 (excluded from test runs).

Verified named coverage relevant to this audit:

- **Translation engines:** `AutoTranslateEngineTests`, `AutoTranslateUrlTests`, `ChatGptTranslateRemovePreambleTests`, `DeepLTranslateTests`, `GeminiTranslateNoTextTests`, `GoogleTranslate{V1,V2,Language}Tests`, `GoogleTranslateV1RetryTests`, `LlamaCppTranslateTests`, `LlamaCppServerArgumentsTests`, `LlmTranslatePromptTests`, `PerplexityTranslateTests`, `CrispAsrMadladLanguageTests`.
- **Translation orchestration:** `DoAutoTranslateTests`, `MergeAndSplitHelperTests`, `FormattingTests`, `AssaTagStripperTests`, `CopyPasteTranslatorTests`.
- **RTL/Arabic:** `UtilitiesFixRtlViaUnicodeCharsTest`, `SkiaBidiRunsTests`, `AudioVisualizerRtlTextTests`, `SkiaTextCacheBidiTests`, `TableViewTextCellFlowDirectionTests`, `AssaTagRtlAvaloniaCanaryTests`, `TextDiffHighlighterTests`, `FontFamilyHelperTests`, `FontTrimmerTests`, `UtilitiesTest`.
- **Editor/translation UI:** `AutoTranslateSelectedLinesTests`, `MainTranslationOriginalTests`, `LlamaCppEngineSettingsButtonTests`, plus broad `tests/UI/Features/**` suites.
- **CI:** `tests.yml` runs the whole suite on PR & push to main (ubuntu, .NET 10, one retry for dispatcher contamination; benchmarks skipped; language-JSON tests included — `LanguageJsonFilesTests`, `SeLanguageJsonContextTests` cited in workflow comments).

Coverage gaps tied to real areas (each names target symbols):

1. **No Arabic-script *content* fixtures in translation tests found** (audited test files exercise engines/parsing, not Arabic output correctness) — target: `MergeAndSplitHelper.SplitMultipleLines` with Arabic text (RTL + diacritics), `RebalanceLines(text, TranslationPair("ar"))`. INFERENCE from file names/reads; a full-text scan of all 88 libuilogic test files for Arabic literals was not performed (LIMITED).
2. **No end-to-end STT→translate→burn-in test possible** (external binaries) — target: pure slices already tested; propose golden tests for `FfmpegGenerator.GenerateHardcodedVideoFile` arg graphs w/ Arabic ASSA.
3. **Cancellation:** `DoAutoTranslateTests` exists; cancellation-path coverage for `AdvancedTranslatorBase.TranslateChunkAsync` bisect path not found by name — target that class.
4. **No test for `LlamaCppAdvancedProtocol.ParseTranslations` adversarial JSON** beyond compilation of grammar — target malformed model outputs (missing keys, wrong types, nested quotes).
5. **Search normalization:** no tests because no feature (§11.6) — write failing tests first when adding Arabic-normalized search.
6. **`DownloadHelper` Range/206 paths** — logic-dense; dedicated tests not found by name — target boundary-snap and 206-fallback branches.

---

# 19. Security / Privacy / Network Audit

| Topic | Finding | Class | Evidence |
|---|---|---|---|
| Telemetry/analytics | **None found.** grep for telemetry/analytics/sentry/app-insights → no relevant hits (only unrelated identifiers). Matches README privacy claims and SKILL.md rule 5. | CONFIRMED (in-repo) | §5.1, README |
| Network calls | Only feature-initiated: translation engines, cloud STT/TTS/OCR, downloads (engines/models/ffmpeg/mpv/yt-dlp), check-for-updates feature (`Features/Help/CheckForUpdates`), docs site. Editor core is offline. | CONFIRMED (structure) | §6.2 |
| Cloud API keys | Stored in `Settings.json` in **plaintext** (e.g., `ChatGptApiKey`, `DeepLApiKey`, `GoogleApiV2Key`, `OpenRouterApiKey` in `SeAutoTranslate`) — user-profile file, no DPAPI/keychain integration found. Proxy password is obfuscated (`ProxySettings.EncodePassword/DecodePassword`) but keys are not. | CONFIRMED | `SeAutoTranslate.cs`; `ProxySettings.cs`; `Se.SaveSettings` |
| Proxy handling | Respect system proxy + bypass list; explicit proxy w/ credentials; careful comment: `DefaultProxyCredentials` only for proxy 407, never machine credentials to target servers (avoids offering machine creds to external hosts) | CONFIRMED (good) | `HttpClientFactoryWithProxy.cs` |
| Subprocess invocation | 242 sites; audited samples use `UseShellExecute=false`, `CreateNoWindow`, quoted args, UTF-8 redirects; no `cmd.exe /c` patterns found in samples | CONFIRMED (samples; not all 242 audited — LIMITED) | §5.6 |
| Command injection surface | File paths embedded in arg strings with escaping quotes; a filename containing a quote could break args in un-audited sites — HYPOTHESIS (needs sweep); SE's own dialogs sanitize by OS file picker but CLI/URL-opened files are user-provided | UNKNOWN (full sweep not done) | §34 |
| Downloaded binaries | Pinned URLs (release tags), SHA-256 registry, SE-controlled support-files rebuilds for Linux/mac (with build verification workflows cited in comments) | CONFIRMED | `WhisperDownloadService.cs` |
| Model licenses downloaded at runtime | Mixed: MIT/BSD/Apache engines; **model weights** vary (Gemma terms for TranslateGemma; CC-BY-NC for NLLB-serve if user self-hosts; MiLMMT-46 license not verified here) — SE downloads to user machine; redistribution-by-download generally, not bundled | CONFIRMED (existence); per-model terms in §20 | §20 |
| Temporary files | ASSA temp files, extracted wavs, mka probes in `%TEMP%` with cleanup in finally blocks (sampled) | CONFIRMED (samples) | `BurnInViewModel` excerpt |
| Subtitle-content leakage | Subtitle text leaves the machine only when a cloud engine/lookup is used (README states provider-policy applies); error logs may contain engine response bodies (e.g., ChatGPT error JSON logged via `SeLogger.Error`) — could include snippets of user text in rare API error bodies | CONFIRMED mechanism; impact LOW, but local logs are a privacy surface (INFERENCE) | `ChatGptTranslate` error path |
| Unsafe input parsing | 300+ format parsers ingest untrusted files; sharp corners historically (Jet DB reader, XLSX zip entries) — no fuzzing evidence in repo | UNKNOWN | §34 |

---

# 20. License / Dependency Audit

## 20.1 This repository (REPOSITORY_FACT)

- Root + libse: **MIT** ("Copyright (c) 2026 Nikolaj Olsson"). No THIRD-PARTY-NOTICES file found in tree (absence verified by listing; NuGet packages' licenses are the user's install-time concern).

## 20.2 Major shipped NuGet dependencies (versions from csproj; licenses from knowledge + marked)

| Package | Version (repo) | License | Note |
|---|---|---|---|
| Avalonia (+Desktop/Markup.Declarative/Themes.Fluent/Fonts.Inter) | 12.1.3 / 12.1.1 | MIT | UI stack |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | |
| Microsoft.Extensions.DependencyInjection(+.Abstractions, .Http) | 11.0.0-preview.2 | MIT | preview channel — noted |
| FFmpeg.AutoGen | 9.0.1.1 | LGPL-2.1+ (binding); ffmpeg itself LGPL/GPL per build | dynamic use |
| Google.Cloud.TextToSpeech.V1 | 3.18.0 | Apache-2.0 | cloud TTS |
| SharpCompress | 0.50.4 | MIT | 7z/zip unpack of engines |
| SkiaSharp(+HarfBuzz pin) | 3.119.4 / HB 8.3.1.5 | MIT | |
| Spectre.Console(+Cli) | (seconv) | MIT | |

License types above are from general knowledge, not fetched per-package (labeled UNKNOWN-verified? — see §34 limitation; the *versions* are REPOSITORY_FACT).

## 20.3 External components the app can download (REPOSITORY_FACT of integration; license per EXTERNAL_EVIDENCE fetched 2026-09-26 unless noted)

| Component | Role | License | Source/date | Redistribution/commercial notes |
|---|---|---|---|---|
| llama.cpp `llama-server` | local LLM runtime | **MIT** | github.com/ggml-org/llama.cpp LICENSE (fetched) | free commercial redistribution |
| Ollama | local LLM runtime | MIT (per llama.cpp ecosystem table + project) | §21 search | free |
| faster-whisper / CTranslate2 | local ASR runtime | **MIT / MIT** | §21 fetched | free |
| WhisperX | ASR+align+diarize | **BSD-2-Clause** per repo badge (context7, star-history); one secondary source (clore.ai) says BSD-4-Clause — **UNRESOLVED, verify against LICENSE file before bundling** | §21 fetched | permissive either way but 4-clause adds advertising clause |
| Const-me Whisper (Win) | ASR | MIT (project known; not re-fetched — UNKNOWN-verified) | — | |
| Purfview Faster-Whisper-XXL | ASR dist | bundled upstream release; license of the *distribution* not verified here | — | verify before repackaging |
| **TranslateGemma GGUFs** | local MT weights | **Gemma Terms of Use** — commercial use allowed, not OSI; prohibited-use policy applies; distribution must pass terms + Notice file; outputs not restricted (secondary analysis: sadō blog) | the-decoder 2026-01-15; Ollama license blob (fetched) | shipping *downloads pointing at* HF is low-risk; *bundling weights* triggers notice obligations |
| MiLMMT-46 (Xiaomi 2026) | local MT weights | not verified in this audit (model card not fetched) | repo cites paper | UNKNOWN — verify before curation changes |
| Hy-MT2 (Tencent) | local MT weights | not verified | — | UNKNOWN |
| **NLLB-200 (via NLLB API/serve integrations)** | MT weights | **CC-BY-NC-4.0 — non-commercial** | §21 fetched (multiple sources) | SE only *calls* a user-hosted server; never bundle weights |
| MADLAD-400 | local MT (Crisp Madlad) | Apache-2.0 (known; not re-fetched — UNKNOWN-verified) | — | permissive |
| whisper.cpp binaries | ASR | MIT runtime; OpenAI whisper **model weights MIT** | §21 | free |
| **MADAR corpus/lexicon** (candidate for Egyptian glossary seeding) | data | **non-commercial license** (NYU AD) | §21 fetched | cannot ship in MIT app; research-only |
| **CAMeL Tools / ADIDA** (candidate dialect-ID) | toolkit | **MIT** | github README (fetched) | usable; Python runtime cost |
| yt-dlp, mpv, ffmpeg, Tesseract, PaddleOCR | media/OCR | permissive/LGPL/GPL mix per component | not re-verified this audit | existing practice |

## 20.4 Rules going forward (RECOMMENDATION)

1. Never vendor weights into the repo; keep the *download-at-runtime* pattern (already the house style) — keeps MIT clean.
2. Before adding any curated model, record its license next to the `LlamaCppModel` record (a comment field) — cheap compliance.
3. Verify WhisperX license text directly if its standalone build is ever redistributed beyond current practice.


---

# 21. External Research (performed 2026-09-26 via web search; primary sources preferred)

Format: CLAIM — SOURCE (type, date) — RELEVANCE — LICENSE — INTEGRATION IMPLICATION — CONFIDENCE — COUNTER-EVIDENCE/LIMITS.

1. **llama.cpp is MIT-licensed and actively releases** — SOURCE: `github.com/ggml-org/llama.cpp` LICENSE file + repo page (primary, fetched 2026-09-26; release listing shows v0.4.1 2026-09-15 via secondary tracker). RELEVANCE: the app's local-LLM engine is built on it (REPOSITORY_FACT `LlamaCppServerManager`). LICENSE: MIT. INTEGRATION: none new. CONFIDENCE: HIGH. LIMITS: version currency of the *bundled/downloaded build* is pinned elsewhere in SE support-files (not audited).
2. **faster-whisper (MIT) + CTranslate2 (MIT), ~4× faster than reference Whisper** — SOURCE: SYSTRAN/faster-whisper GitHub (via multiple secondary pages + context7 license tag; v1.2.1 Oct 31 2025 noted). RELEVANCE: SE ships Purfview Faster-Whisper-XXL & CTranslate2 engines (REPOSITORY_FACT). CONFIDENCE: HIGH (license), MEDIUM (perf claim — vendor-adjacent sources). LIMITS: perf numbers are self-reported; no independent benchmark fetched.
3. **WhisperX license: BSD-2-Clause (repo badge) vs BSD-4-Clause (one secondary)** — SOURCE: context7.com/m-bain/whisperx + star-history (badges) vs docs.clore.ai (claim). CONFLICT RECORDED. INTEGRATION: verify LICENSE file before any redistribution change. CONFIDENCE: MEDIUM.
4. **NLLB-200 weights are CC-BY-NC-4.0 (non-commercial), research release** — SOURCE: multiple independent model pages/analyses (aimodels.fyi ×2, spikeseed blog, promptlayer) — consistent; primary Meta card not fetched directly. RELEVANCE: SE's NLLB engines call user-hosted NLLB; curation must never bundle. CONFIDENCE: HIGH (consistent multi-source). LIMITS: primary source not fetched (noted).
5. **TranslateGemma released 2026-01-15 (4B/12B/27B, 55 languages, Gemma Terms of Use; commercial OK w/ prohibited-use policy; outputs unrestricted)** — SOURCE: the-decoder (secondary, dated), Ollama license blob (primary text of terms), aicybr/wavespeed (secondary). RELEVANCE: TranslateGemma is SE's flagship curated local translate model (REPOSITORY_FACT). CONFIDENCE: HIGH on terms text; MEDIUM on release details. LIMITS: performance claims (12B > Gemma3-27B) are Google-reported — not independently verified.
6. **Gemma-outputs license nuance (datasets of outputs may create "Model Derivatives")** — SOURCE: shujisado.org analysis (secondary, 2025-07-22). RELEVANCE: only matters if SE ever trained on outputs — it does not. CONFIDENCE: MEDIUM. No action.
7. **Arabic ASR: whisper-large-v3 WER — MSA 27.95%, Egyptian 59.28%, Hijazi 49.99%, Khaliji 59.92%, Najdi 48.58% (open-source Arabic ASR leaderboard)** — SOURCE: arXiv:2412.13788 (primary preprint, Dec 2024). RELEVANCE: quantifies the dialect gap underpinning §12. CONFIDENCE: HIGH (paper), with caveat: single benchmark suite (probably MASC/SADA-adjacent sets; dialect portions small). COUNTER-EVIDENCE: fine-tuned/alternative systems do much better (next item).
8. **LLM post-correction reduces dialectal Arabic ASR errors (Egyptian mean WER Δ = 0.183, CI [0.050, 0.400]; Whisper-Large+GPT best across 5 dialects)** — SOURCE: Islamic University Journal of Applied Sciences VIII(1) 2026 (primary-ish journal PDF). RELEVANCE: validates a "transcribe → LLM-correct → translate" Egyptian path using SE's existing AI-review plumbing. CONFIDENCE: MEDIUM-HIGH (small sample, effect sizes modest, SemDist gains not significant for Egyptian).
9. **Fine-tuned Arabic Whisper models reach ~9.5% WER on MGB-2 broadcast MSA (vs 10.85 zero-shot large-v3); Quran SOTA 0.33% WER** — SOURCE: Hugging Face model card `Abu-Dju/whisper-large-v3-ar` (primary card, 2026, Zenodo DOI cited). RELEVANCE: fine-tuning route exists for Arabic quality; MSA-focused. CONFIDENCE: MEDIUM (self-reported evals, one identical normalizer claimed). 
10. **CAMeL Tools: MIT-licensed Arabic NLP toolkit incl. dialect identification (ADIDA); CAMeLBERT dialect taggers (Egyptian/Gulf/Levantine)** — SOURCE: github.com/CAMeL-Lab/camel_tools README (primary) + NYU AD resources page. RELEVANCE: candidate for auto MSA-vs-Egyptian routing (§12). INTEGRATION: Python runtime — real cost; alternatively port heuristics. CONFIDENCE: HIGH (license), MEDIUM (accuracy claims untested here).
11. **MADAR corpus/lexicon (25-city dialect parallel data + 1,045-concept lexicon incl. Egyptian) — non-commercial license** — SOURCE: NYU AD MADAR project pages (primary). RELEVANCE: best-in-class Egyptian glossary seed — **cannot ship in an MIT app**. CONFIDENCE: HIGH.
12. **pyvideotrans: GPL-3.0, ~19.1k stars, one-click video translate+dub (faster-whisper → MT → TTS → mux)** — SOURCE: HelloGitHub listing + official docs (pyvideotrans.com/guide, fetched) + ai-tldr (stars). RELEVANCE: the closest open-source embodiment of the §14 wizard. INTEGRATION: **GPL-3.0 — no code reuse in MIT repo**; ideas only. CONFIDENCE: HIGH (existence/license/flow), MEDIUM (star count).
13. **VideoLingo: Apache-2.0; WhisperX-based; 3-step Translate-Reflect-Adapt; AI+custom terminology; Netflix-style single-line subtitles; Streamlit UI; dubbing via GPT-SoVITS/Azure/OpenAI** — SOURCE: github.com/Huanshere/VideoLingo README (primary, fetched). RELEVANCE: architectural ideas (reflect loop, terminology generation) map onto `AdvancedTranslatorBase` (history/glossary) + AI-review. INTEGRATION: Apache-2.0 code *could* be referenced with attribution, but stack is Python/Streamlit — idea transfer only. CONFIDENCE: HIGH.
14. **Upstream Subtitle Edit: v5.0.0 2026-06-22, v5.1.0 2026-07-29 (AI review, generic OpenAI-compatible engine, remote llama.cpp, DeepL formality), v5.2.0 line Aug–Sep 2026; LICENSE = MIT at HEAD; official FAQ states MIT** — SOURCE: github discussions #11744/#12929 (primary posts by niksedk), newreleases tracker, LICENSE blob (fetched). RELEVANCE: this fork tracks an actively moving upstream; upstream already landed several features present in this tree (AiReview folder REPOSITORY_FACT). CONFIDENCE: HIGH. LIMITS: fork divergence point = import commit (single commit).
15. **Third-party subtitleedit.org claims GPL v3 / v4.0.15 (Feb 2025)** — SOURCE: subtitleedit.org (fetched). CLASS: outdated/incorrect third-party info vs MIT LICENSE at upstream HEAD (item 14). Recorded as discrepancy, per audit rule 16. CONFIDENCE: HIGH that the conflict exists; the site is unofficial.

**WEB_RESEARCH_UNAVAILABLE:** not applicable — web research was available and used. All external claims above carry sources; items with unverified specifics are marked in-line.

---

# 22. Project/Competitor Comparison

Labels: [RF]=repository fact, [EE]=external evidence, [I]=inference, [R]=recommendation.

| Project | What it does better | What it does differently | Idea worth studying | Reusable? | License | Integration difficulty | Risk |
|---|---|---|---|---|---|---|---|
| **pyvideotrans** [EE] | True one-click video→translated+dubbed video pipeline (the journey SE lacks) [I] | Python desktop app; channel-based engines; clone-voice dubbing | Single-job orchestration incl. dubbing; settings→channel abstraction | No code (GPL-3.0) [EE] | n/a | n/a | copying UX *ideas* only; license contamination risk if code is copied |
| **VideoLingo** [EE] | Translation *quality loop* (translate→reflect→adapt), auto-generated terminology, one-line-subtitle discipline | Streamlit pipeline; Netflix-style constraints | Reflect/adapt second pass; terminology auto-generation feeding glossary | Ideas yes; code Apache-2.0 w/ attribution possible but wrong stack [EE] | Apache-2.0 | High (stack mismatch) | scope creep; token cost of reflect passes |
| **Upstream Subtitle Edit** [EE/RF] | Ships faster (5.0→5.2 within ~3 months) [EE]; large user base for bug discovery | Same codebase family — this fork is an import of it | Release cadence & beta/RC channel discipline | Same code | MIT | Low (tracking upstream) | divergence drift between fork and upstream |
| **WhisperX** [EE] | Word-level timestamps + diarization + batched speed | Forced-alignment stage | Speaker-timing → per-speaker conditioning input for §12 | Already integrated as engine [RF] | BSD-2 (verify) [EE] | Already in | pyannote model license/HF token for diarization [EE] |
| **Buzz / local whisper GUIs** (not fetched this session) | Simpler single-task UX | — | Minimal wizard patterns | n/a | — | — | UNKNOWN (not researched this session — stated honestly) |
| **CAMeL Tools / ADIDA** [EE] | Arabic dialect identification, Arabic morphological QA tooling | Python toolkit | Dialect router for target-language suggestion [R] | Apache/MIT-licensed toolkit usable; runtime cost | MIT [EE] | Medium-High (Python runtime) | offline packaging weight |

**Gap statement [I]:** no audited competitor combines *pro-grade subtitle editing* + *guided Arabic dialect translation*; SE has the former (strongest in class) and none of the competitors have the latter as a first-class concept. That intersection is the differentiation opportunity.

---

# 23. General Improvements (quality-filtered; each = real problem → real hook)

Common fields per idea: PROBLEM / SOLUTION / EVIDENCE / USER VALUE / FEASIBILITY / ARCH IMPACT / COMPLEXITY / DEPENDENCIES / RISKS / ROLLBACK.

**G1. Guided "Translate Video" pipeline job**
- PROBLEM: STT→translate→burn-in are 3 disconnected windows (§13-1).
- SOLUTION: new `TranslationJob` service orchestrating existing `SpeechToTextViewModel`-equivalent engine calls, `DoAutoTranslate`, `BurnInViewModel` encoding service, with stage states + resume.
- EVIDENCE: §9 traces; all stages have callable services (libuilogic/engines).
- VALUE: the product journey exists. FEASIBILITY: high (composition only). ARCH: additive (new feature folder; `MainViewModel` untouched). COMPLEXITY: medium-high. DEPS: none new. RISKS: duplicate logic with windows → mitigate by extracting invokable services per stage. ROLLBACK: feature-flag the menu entry.
**G2. Pipeline checkpoint/resume** — persist per-stage state (translated rows to disk after each batch) — EVIDENCE §17-9 — VALUE: long-video safety — COMPLEXITY low-medium — ROLLBACK: ignore file.
**G3. Per-target-language default prompts** — generic prompt pushes "keep punctuation as input" (Latin) into Arabic (§11.8-6) — add language-aware default in `ToolsSettings` consumption point — VALUE: better Arabic output by default — COMPLEXITY low — ROLLBACK: setting revert.
**G4. Project glossary files** — global textbox only today (§10.6) — load `{video}.glossary.json` next to media into `SeLlamaCppAdvanced.Glossary` — VALUE: consistency across episodes — COMPLEXITY low — DEPS: G-none.
**G5. Translation memory (per-project)** — absence verified (§10.6) — SQLite/JSON store keyed by source hash+lang+model; reuse in re-runs — VALUE: cheap re-translations, consistency — COMPLEXITY medium — RISKS: stale TM — add "retranslate" override.
**G6. Post-translation QA pass** — CPS/length/bidi checks before apply using existing Arabic calculators + `ErrorList` UI — EVIDENCE §10.5, §11.4 — VALUE: fewer broken exports — COMPLEXITY low-medium.
**G7. Keep-untranslated predicate extension** — URLs/emails/numbers-only lines join music lines in `IsKeptUntranslated` — EVIDENCE `MergeAndSplitHelper.IsKeptUntranslated` (music-only) — VALUE: fewer mangled URLs/names — COMPLEXITY trivial.
**G8. Engine health pre-flight** — before StartTranslation, ping endpoint / check binary — EVIDENCE §17-1 — VALUE: fail fast with clear message — COMPLEXITY low.
**G9. Structured engine errors** — map HTTP status/JSON to human phrases in `TranslationErrorWindow` — EVIDENCE §13-9 — VALUE: self-service fixes — COMPLEXITY low-medium (per-engine map).
**G10. Hardware advisor** — one-time RAM/GPU probe recommending engine/model preset — EVIDENCE §15.3 precedent (Crisp variant pick) — VALUE: beginners get working defaults — COMPLEXITY medium — RISKS: wrong advice on exotic GPUs → always allow override.
**G11. Disk-space guard on downloads** — `DriveInfo` check + size strings (exist) — EVIDENCE §15.2-1 — VALUE: no failed 700 MB downloads — COMPLEXITY trivial.
**G12. Global model manager page** — unified list (installed engines/models/sizes/cleanup) using `DownloadHashManager` data — EVIDENCE §15.3 — VALUE: disk control, trust — COMPLEXITY medium.
**G13. Download throttle setting** — single choke point in `DownloadHelper` — EVIDENCE §15.2-6 — VALUE: low-bandwidth users — COMPLEXITY trivial.
**G14. Batch translate parallelism (opt-in)** — sequential loop today (§16) — run N engines/connections concurrently for cloud engines w/ rate-limit respect (`RequestDelaySeconds`) — VALUE: 3-5× wall-clock for big files — COMPLEXITY medium — RISKS: API bans → default off, per-engine lock.
**G15. Checksum-verified engine updates UI** — surface `UpdateAvailable` state (already computed) as a badge — EVIDENCE §15.1 — VALUE: freshness trust — COMPLEXITY low.
**G16. Unified progress model** — one job view aggregating stage progresses (§14-8) — VALUE: clarity — COMPLEXITY medium — DEPS: G1.
**G17. Session restore of translation grid** — persist rows/settings of open translate session across app restarts — EVIDENCE §17-9 — VALUE: resilience — COMPLEXITY low-medium — DEPS: G2.
**G18. Format-coverage doc truth** — reconcile "380+"/"~330"/429 counts (§3) with a generated count — VALUE: accurate docs — COMPLEXITY trivial.
**G19. `Configuration.Settings` ↔ `Se.Settings` bridge doc/test** — two config worlds (§5.4) — add cross-check test that shared keys stay in sync — VALUE: prevents drift bugs (#11744 class) — COMPLEXITY low.
**G20. CLI parity for pipeline** — expose `seconv translate` stage using the same job service — EVIDENCE seconv exists — VALUE: automation/CI use — COMPLEXITY medium — DEPS: G1 service extraction.
**G21. Structured logs (JSONL option) for jobs** — `Se.WriteToolsLog` already per-stage (§9-C) — add job-id correlation — VALUE: support/debuggability — COMPLEXITY low.
**G22. Crash-only jobs: mark incomplete jobs dirty at startup** — pairs with G2 — COMPLEXITY low.
**G23. "Retry failed lines only"** — the loop knows `index`; persist failed row numbers; offer targeted retry — EVIDENCE §10.5 error flow — VALUE: saves tokens/time — COMPLEXITY low.
**G24. Rate-limit backoff honoring `Retry-After`** — engines use fixed backoff {2555,5007,9013} ms (REPOSITORY_FACT `ChatGptTranslate`) — respect server header when present — VALUE: fewer bans — COMPLEXITY low.
**G25. Engine smoke-test button** in settings — sends one tiny string per configured engine — VALUE: setup confidence — COMPLEXITY low.
**G26. Documentation: "Translate a video" tutorial page in docs/** — docs site exists (Jekyll) — VALUE: beginner onboarding — COMPLEXITY trivial.
**G27. Auto-update check for curated model list** — `LlamaCppServerManager` list is code-frozen per release — remote manifest (signed) — VALUE: fresh models without app update — COMPLEXITY medium — RISKS: supply chain → pin + hash (infrastructure exists, §15.1).
**G28. Window-level "what will leave my machine" summary** for cloud engines — EVIDENCE SKILL.md trust boundary + §19 — VALUE: privacy clarity — COMPLEXITY low.
**G29. Standardize `<br/>` break placeholder docs for engine authors** — marker interface exists (`ILineBreakPreservingTranslator`) — add contract doc + tests — VALUE: fewer integration bugs (#14803 class) — COMPLEXITY low.
**G30. Pluralized/CPS-aware auto-split for translated lines** — reuse `RebalanceLines` post-apply when target overflows limits (exists but check invocation in UI apply path — ⚠️ apply path not audited) — VALUE: valid layouts — COMPLEXITY low-medium.
**G31. Preferences: default target language + engine per app** — remembers last used (AutoTranslateLast* exist) — extend to profiles — VALUE: fewer clicks — COMPLEXITY low.
**G32. CI: publish benchmark deltas on main** — benchmarks exist but skipped in CI (§16) — nightly job — VALUE: regression visibility — COMPLEXITY low-medium.


---

# 24. Arabic Improvements (15+; each evidence-tied)

**A1. Arabic-normalized search & replace** — PROBLEM: hamza/alef/ya/marbuta variants + tashkeel break matches (§11.6, absence verified) — SOLUTION: optional "Arabic-tolerant" mode in Find/Replace normalizing أإآ→ا, ى→ي, ة→ه(option), strip U+064B–U+0655 — EVIDENCE `FindService.cs` inspected, no normalization found — VALUE: core editor utility for Arabic users — FEASIBILITY high — ARCH: additive — COMPLEXITY low-medium — DEPS: none — RISKS: false positives when off-by-default flag misread — ROLLBACK: toggle off.
**A2. Arabic punctuation locale post-pass** — `?,;:` → `؟،؛` in Arabic lines (and back on EN) as fix function + translate post-pass — EVIDENCE: default prompt literally says "keep punctuation as input" (`ToolsSettings.cs:157`) — VALUE: native-looking subtitles — COMPLEXITY low — ROLLBACK: batch-convert function only.
**A3. Arabic quotation normalization** — `"` → `«»` or `” “` per style profile; normalize existing mixed quotes — EVIDENCE: no Arabic quote logic found anywhere (grep Arabic files §11) — VALUE: typography — COMPLEXITY low.
**A4. Tashkeel policy tool** — strip/keep/restore diacritics incl. CPS recalc using `CalcIgnoreArabicDiacritics` (exists §11.4) — VALUE: control over vocalized text — COMPLEXITY low-medium.
**A5. Bidi-tag linter & cleaner** — validate U+202A–U+202E/U+2066–U+2069 balance per line; one-click clean (remove/fix via `FixRtlViaUnicodeChars` which already exists §11.1) wired into Fix-common-errors — EVIDENCE: commands exist separately (§11.2) but no *validation report* — VALUE: corrupted-RTL recovery — COMPLEXITY low.
**A6. Arabic font fallback pack (Linux)** — extend `Program.cs` FontFallbacks (CJK-only today §5.1) with common Arabic families (Noto Naskh/Kufi/Sans Arabic) — EVIDENCE #11355-class failure documented for CJK — VALUE: no boxes on minimal installs — COMPLEXITY trivial — RISK: embedded font size → use fallbacks, not embed.
**A7. Arabic-aware line-width preview accuracy** — ensure the editor's "too wide" syntax highlight uses Arabic calculators for measurement in all code paths (CalcFactory exists; verify every consumer ⚠️) — VALUE: correct warnings — COMPLEXITY low — UNKNOWN: consumer coverage (flagged §34).
**A8. Arabic RTL burn-in test matrix + golden samples** — ship test ASSA (Arabic + Latin mixed + digits) rendered through libass in CI (ffmpeg available in CI runners) — EVIDENCE §11.3 LIMITED (no pixel-level verification this audit) — VALUE: regression guard for the highest-risk Arabic path — COMPLEXITY medium.
**A9. Arabic OCR for image subtitles** — bundle/curate Paddle Arabic model (PaddleOcrModels mentions Arabic §11.6) in download UI with size/disk info — VALUE: Arabic DVD/Blu-ray OCR — COMPLEXITY low-medium — DEPS: model license check.
**A10. Arabic hunspell dictionary defaults + custom user dict UX** — dictionary URLs exist (§11.6); add "Arabic" quick-pick and user-dictionary add flow to spellcheck — VALUE: fewer false spellcheck errors — COMPLEXITY low.
**A11. Mixed-direction (Arabic + Latin/URLs) editing guard** — when typing/inserting Latin inside Arabic line, auto-insert U+200E/U+200F isolates per `Idx.cs` precedent — EVIDENCE §11.5 — VALUE: no scrambled mixes — COMPLEXITY medium — RISK: invisible chars annoy — make optional.
**A12. Arabic date/number formatting QA** — flag Western digits in Arabic lines per style profile; optional Arabic-Indic conversion — VALUE: consistency — COMPLEXITY low.
**A13. RTL keyboard navigation tests** — MainViewModel visual-caret logic (§11.2, 30388–30860) is subtle; add headless caret tests — VALUE: protect rare complex code — COMPLEXITY medium.
**A14. Arabic subtitle samples & docs page** — `docs/` add Arabic workflow guide w/ RTL screenshots (docs site exists) — VALUE: onboarding — COMPLEXITY trivial.
**A15. Arabic-specific "Fix common errors" rules** — e.g., wrong common-alefaat, tatweel misuse (ـ) stretching, double-space after kashida — libse `FixCommonErrors` framework exists (`Forms/FixCommonErrors/`) — VALUE: cleaner imports — COMPLEXITY medium — RISKS: false positives → rule-by-rule opt-in.
**A16. Arabic CPS/duration presets auto-select** — when paragraph language = ar, auto-apply Arabic length calc presets (CalcFactory) + a CPS ceiling suited to Arabic reading speed (needs research; mark HYPOTHESIS for the number) — VALUE: better timing defaults — COMPLEXITY low.

---

# 25. Egyptian Arabic Improvements (10+)

**E1. Target-language style selector (MSA / Egyptian) in Translate window** — maps to `ar` vs `arz` target code *when engine supports it*; per-engine availability derived from `GetSupportedTargetLanguages()` (REPOSITORY_FACT lists §12.2); engines without arz → graceful "will output MSA" notice — VALUE: the core product decision surfaced — COMPLEXITY low-medium — RISKS: model quality variance for arz → default to cloud-LLM engines when selected.
**E2. Egyptian style profile preset** — a shipped profile feeding `SeLlamaCppAdvanced` (Prompt/Synopsis/Glossary/Formality — hooks exist §12.2): "conversational Egyptian, keep humor, don't censor, use common spoken forms, avoid fuṣḥā constructs" — EVIDENCE: profile fields exist — VALUE: instant quality lift — COMPLEXITY low (content work).
**E3. Egyptian glossary seed file (MIT-clean)** — curate from open data (Wiktionary/CC datasets) not MADAR (non-commercial §21-11): 200–500 entries EN→masri (e.g., "guys=يا جماعة", "okay=تمام/ماشي") — loaded via G4 project glossary — VALUE: consistency & authenticity — COMPLEXITY medium (content) — LEGAL: verify each entry source.
**E4. ASR output corrector stage (dialect-aware)** — optional LLM pass before translation to normalize/fix Egyptian ASR errors, justified by §21-8 (WER Δ 0.183) — implemented as a special `IAutoTranslator`-independent transform using the existing AI-review/llama plumbing — VALUE: better source text → better translation — COMPLEXITY medium — RISKS: overcorrection → keep original column for diff.
**E5. Per-speaker conditioning (larger arc)** — diarization (MossDiarize/Crisp engines exist §6.2; WhisperX §21-3) → character table (name, gender, formality, register per character) → injected per batch into `BuildUserContent` as a `speakers` array + prompt block — VALUE: pronoun/gender consistency, character voice — COMPLEXITY high — CLASSIFICATION: REQUIRES ARCHITECTURAL CHANGE (§12.5).
**E6. Scene-context assembly** — use shot-change detection (exists: `Features/Video/ShotChanges` §3) to bucket lines; add 2-line scene synopsis to `Synopsis` automatically per bucket — VALUE: pronoun/tense continuity — COMPLEXITY medium — HYPOTHESIS: quality gain needs eval.
**E7. Egyptian QA dictionary** — flag MSA-only constructs (e.g., "لذا", "حيث" formalisms) in Egyptian-styled targets; suggest conversational equivalents — VALUE: register consistency — COMPLEXITY medium — DEPS: E3 data.
**E8. "Egyptian" TTS voice routing** — when dubbing Arabic, route `arz` to OmniVoice Egyptian voice (exists §12.2) by default — VALUE: authentic dubbing — COMPLEXITY low.
**E9. Egyptian transcript language auto-suggest** — dialect-ID heuristic (word-list based, in-proc; CAMeL ADIDA is the reference [EE, MIT]) to propose Egyptian ASR/translation targets — VALUE: smart defaults — COMPLEXITY medium — RISKS: mis-ID → always a suggestion, never silent.
**E10. Egyptian eval set + benchmark harness** — 100–300 line EN→arz golden set (Netflix-style rules); measure engines (ChatGPT/Gemini/TranslateGemma/Ollama models) periodically; store results in repo — VALUE: objective engine ranking for the product's core promise — COMPLEXITY medium — RISKS: copyright of source lines → use public-domain/CC sources.
**E11. Numerals policy per style profile** — Arabic-Indic vs Western for Egyptian — post-pass converter (§12.4) — COMPLEXITY low.
**E12. "Spoken-register" doc + prompt library** — docs page with copy-paste prompts (informal/casual/children/religious registers) for the existing per-engine prompt fields — VALUE: immediate user power, zero code — COMPLEXITY trivial.

---

# 26. UX Improvements (10+; beyond §14 macro-design)

**U1. One-window job dashboard** (G1/G16) — stage timeline w/ per-stage open-in-advanced link.
**U2. Local/Online badge on every engine item** — derived from engine metadata (cloud list verifiable: `IOnlineSttEngine`, cloud translate engines) — SKILL.md-aligned consent.
**U3. First-run "goal picker"** — "Edit subtitles / Translate a video / Both" sets home-screen emphasis (does not hide menus) — RISKS: none (cosmetic).
**U4. Arabic preview correctness indicator** — warn when output font lacks Arabic glyphs before burn-in (font enumeration exists: `FontFamilyHelper`) — VALUE: prevents "boxes in video" surprises — COMPLEXITY low-medium.
**U5. Inline diff on corrected lines** (E4/G23) — show original vs corrected vs translated columns (grid already multi-column, §5.2).
**U6. Progress realism** — STT/translate ETA from measured rows/sec (coalesced counter exists) — VALUE: trust — COMPLEXITY low.
**U7. Error cards** with "copy details" + "open log" + "fix it" deep links (settings page, engine download) — replaces raw JSON dialogs (§13-9) — COMPLEXITY low-medium.
**U8. Keyboard-first review mode** — arrows through rows w/ accept/fix/flag keys; grid/undo infrastructure exists — VALUE: speed for translators — COMPLEXITY medium.
**U9. Wizard "download what's missing" step** — consolidates existing download windows (§15.1) with total size + disk check + resume — VALUE: no dead-ends — COMPLEXITY medium — DEPS: G11/G12.
**U10. "What stayed in Arabic-MSA?" report** — after translate-with-Egyptian-selected, list lines whose target couldn't be dialect (engine limitation) — VALUE: honesty about output — COMPLEXITY low — DEPS: E1.
**U11. Export presets ("YouTube SRT", "Netflix-ish", "Burn-in 1080p")** — map to existing save/export parameters — VALUE: beginner defaults — COMPLEXITY low — RISKS: brand-name misuse → use generic names.
**U12. Undo-safe apply of translations** — apply path (⚠️ not audited) must land in show-history/undo; add test — VALUE: no data loss — COMPLEXITY low.

---

# 27. Performance/Reliability Improvements (10+)

**P1. Parallel cloud-translation with per-engine concurrency caps** (G14) — sequential today (§16) — VALUE: wall-clock — RISKS: 429s → caps + backoff honoring (G24).
**P2. Batch checkpointing for advanced engine** — after each accepted `TranslateChunkAsync`, flush rows to disk (G2) — VALUE: crash safety — COMPLEXITY low.
**P3. Pre-flight llama-server readiness** — health probe exists in manager (§10.4); reuse for wizard start — VALUE: no mid-job stalls — COMPLEXITY low.
**P4. KV-cache-safe style injection** — any new style/profile text must join the *stable* prefix (invalidate only on profile change) — EVIDENCE §5.8 invariant — VALUE: keeps throughput — COMPLEXITY low (design rule).
**P5. ffmpeg failure auto-retry (drop optional filters)** — e.g., retry without `-tune`/logo filter on nonzero exit — EVIDENCE §17-4 — VALUE: higher completion rate — COMPLEXITY low-medium — RISKS: output differs from requested → inform user.
**P6. Sequential→overlapped pipeline stages** — start translating lines while STT still running for later chunks (job architecture G1 enables) — VALUE: 20–40% wall-clock (HYPOTHESIS) — COMPLEXITY medium-high — RISKS: complexity; gate behind "fast mode".
**P7. WAV extraction reuse** — cache extracted wav per video hash (per-run subfolder exists §9-A; cross-run reuse not found) — VALUE: repeat jobs faster — COMPLEXITY low — RISKS: disk growth → LRU cleanup.
**P8. Memory guard before model load** — check free RAM vs model size string (data exists) — VALUE: fewer OOM crashes — COMPLEXITY low — UNKNOWN: actual RAM need per quant (label estimate).
**P9. UI-thread audit for new pipelines** — enforce Dispatcher marshaling via analyzers/review checklist (§17-7) — VALUE: no grid corruption — COMPLEXITY low (process).
**P10. Regression suite for `MergeAndSplitHelper` with Arabic fixtures** (§18-1) — VALUE: protects the most translation-sensitive heuristic — COMPLEXITY low-medium.
**P11. `tests.yml`: shard + cancel-in-progress** — suite is heavy (645 UI test files) and reruns fully on retry — VALUE: CI time — COMPLEXITY low.
**P12. Burn-in preset validation** — reject impossible codec/pixel-format combos before 2-pass start (`OutputContainer.GetAudioEncodingFor` precedent) — VALUE: fail-fast — COMPLEXITY low.
**P13. Rate-limit telemetry (local counters only, no upload)** — per-engine success/429 counters shown in settings — VALUE: self-diagnosis — COMPLEXITY low — PRIVACY: stays on disk.

---

# 28. Out-of-the-Box Ideas (10; each honest about feasibility)

**O1. "Dialect dailies" mode** — generate both MSA and Egyptian translations in one run (two target passes, shared cache/TM), present as parallel columns for the user to pick per line — VALUE: Egypt's real bilingual-subtitle workflow; FEASIBILITY: high with G5 TM (second pass nearly free) — RISKS: doubled cloud cost → local-model default.
**O2. Reverse-check ("back-translation") QA** — run translated→English via a second cheap engine; flag lines whose back-translation diverges (embedding-free: token overlap score) — maps to existing engine registry — VALUE: automatic quality triage — RISKS: false positives; show score, don't block.
**O3. Read-aloud review** — TTS engines (25+ exist §6.2) speak the Arabic translation during review; catch grammar/tone by ear — FEASIBILITY: high (Edge TTS free tier, local Kokoro/Chatterbox) — VALUE: accessibility + proofing.
**O4. Terminology auto-harvest** — mine frequent proper nouns from source lines (existing names-list infrastructure: `AddToNamesList`, `Dictionaries/*_names.xml`) → pre-fill glossary — VALUE: glossary cold-start — COMPLEXITY low-medium.
**O5. Subtitle "register dial"** — one slider (فصحى ←→ عامية) that re-renders the *current* translation toward either register via the LLM review engine — VALUE: instant tone control — DEPS: AI-review + style profiles; HYPOTHESIS on quality.
**O6. Community style profiles as signed JSON** — shareable Egyptian/religious/children's profiles; signature check to satisfy SKILL.md trust rules — VALUE: ecosystem — RISKS: prompt injection via profiles → sandbox to the three existing fields only.
**O7. Audioless pre-render preview** — burn-in preview at low resolution (already debounced ffmpeg/libass preview §13-10) extended to *whole-file fast preview* at 1 fps to check layout before real encode — VALUE: time saved on long videos — COMPLEXITY medium.
**O8. "Egyptianify my MSA" one-click** — for existing MSA subtitle files: batch-convert function running the local LLM in register-conversion mode (no English needed) — VALUE: huge existing corpus of MSA subs — COMPLEXITY low-medium (new batch function; engines exist).
**O9. Watch-folder auto-pipeline** — point at a folder; new videos enter the G1 job queue automatically (batch infra exists §9-D) — VALUE: creators' automation — RISKS: runaway cloud spend → local-only default.
**O10. Idiom heat-map** — highlight source lines containing likely idioms/humor (LLM-classified once, cached) so translators review those first — VALUE: effort triage — HYPOTHESIS: precision unknown; needs eval set E10.


---

# 29. Preserve / Change / Experiment Matrix

Classifications carry evidence; "years of accumulated functionality" treated with the SKILL.md conservatism.

| Area | Verdict | Evidence / rationale |
|---|---|---|
| libse subtitle core + formats | **PRESERVE** | 429 format files, 181 test files; the asset that makes SE irreplaceable (§3, §18) |
| RTL editor infrastructure (`RightToLeftHelper`, caret logic, `FixRtlViaUnicodeChars`, bidi waveform) | **PRESERVE** (small IMPROVE: A5/A13) | deep, tested, issue-hardened (§11) |
| `IAutoTranslator` + 36 engines | **PRESERVE** interface; **IMPROVE** orchestration around it | interface is proven; orchestration duplicates logic (§10.1) |
| Advanced local-LLM translation (`AdvancedTranslatorBase`, protocol, server manager) | **PRESERVE** core; **IMPROVE** by adding style-profile/TM hooks (E1/E2/G4/G5) | best-in-repo extension point; invariants documented (§5.8, §10.3) |
| STT engine abstraction + Crisp/Whisper families | **PRESERVE** | 42 engines, download integrity in place (§6.2, §15.1) |
| Burn-in pipeline | **IMPROVE** (P5/P12, U4) | solid two-pass/log design (§9-C); Arabic golden tests missing (A8) |
| Batch convert | **PRESERVE**; extend via new `BatchConvertFunctionType` entries only | registry pattern clean (§9-D) |
| `MainViewModel.cs` (34,977 L) | **REFACTOR LATER** — extract pipeline-callable services opportunistically (G1 demands only new code) | god-class risk documented (§5.2); mass refactor violates SKILL.md "surgical" and risks regressions |
| Settings (`Se` graph + core `Configuration`) | **IMPROVE** incrementally (bridge test G19); no rewrite | dual-world coupling (§5.4) |
| UI framework & declarative-C# style | **PRESERVE** | compiler-checked UI, house style (§5.2); migration would be catastrophic scope |
| Legacy RTL visual reversal (PAC/Cavena) | **PRESERVE** (format-correct), document better (A5) | required by those formats (§11.5) |
| OCR subsystem | **IMPROVE** Arabic coverage (A9) | Latin-only bundled DB (§11.6) |
| TTS/dubbing | **EXPERIMENTAL-use for E8 only** | large surface; not core to subtitles product (§6.2) |
| Classic translation engines' default prompts | **IMPROVE** (G3) | punctuation-into-Arabic defect (§11.8-6) |
| seconv CLI | **PRESERVE**; extend (G20) | headless parity asset (§3) |
| Docs site | **IMPROVE** (G26/A14) | beginner journey absent (§13) |

---

# 30. Target Architecture

CURRENT → PROPOSED (all proposed items are **additive**; existing seams named in each row).

```
┌────────────────────────────── UI (Avalonia, Features/*) ─────────────────────────────┐
│ Existing windows (editor, STT, translate, burn-in, batch)  [PRESERVE]                │
│ NEW: TranslateVideo wizard (Beginner)  ──links──▶ existing windows (Advanced)        │
│ NEW: JobDashboard (stage timeline, resume, errors)                                   │
└──────────────┬───────────────────────────────────────────────────────────────────────┘
               │ services (NEW, extracted-from-window logic where needed)
┌──────────────▼──────────────── Application layer (libuilogic + new) ─────────────────┐
│ NEW TranslationJobService     (state machine: Probe→STT→Translate→QA→Export;          │
│                                checkpoint file per job; crash-only resume [G2/G22])  │
│ EXISTING DoAutoTranslate / IBatchContextTranslator loops        [REUSED]              │
│ NEW StyleProfileStore         (named profiles → SeLlamaCppAdvanced fields)  [E2]      │
│ NEW GlossaryService           (project .glossary.json → Glossary field)      [G4]     │
│ NEW TranslationMemory         (SQLite/JSON, keyed source-hash+lang+model)    [G5]     │
│ NEW ArabicPostProcessor       (punctuation/quotes/numerals/bidi lint)        [A2–A5]  │
│ NEW QaGate                    (CPS via ICalcLength, length, bidi balance)    [G6]     │
│ EXISTING LlamaCppServerManager / DownloadHelper / DownloadHashManager  [REUSED]      │
│ NEW ModelCatalog              (remote signed manifest; per-model license field)[G27]  │
└──────────────┬───────────────────────────────────────────────────────────────────────┘
┌──────────────▼──────── Core engines ─────────────────────────────────────────────────┐
│ libse Subtitle/Paragraph/TimeCode + 300+ formats                [PRESERVE]            │
│ IAutoTranslator (36 impls) / ISpeechToTextEngine (42) / ITtsEngine / OCR engines     │
│ NEW optional: IRegisterRewriter (MSA↔Egyptian register conversion)  [O8]             │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

- **STABLE CORE:** libse domain+formats; `IAutoTranslator`/`IBatchContextTranslator` contracts; STT engine abstraction; ffmpeg/libass burn-in; settings graph.
- **EXTENSION POINTS (existing, reused):** engine interfaces; batch-convert registry; `SeLlamaCppAdvanced` fields as the style surface; `AssetZips` pipeline for new assets (glossaries, profiles).
- **NEW ABSTRACTIONS:** `TranslationJobService` (+ job file), `StyleProfileStore`, `GlossaryService`, `TranslationMemory`, `ArabicPostProcessor`, `QaGate`, `ModelCatalog`, optional `IRegisterRewriter`.
- **HIGH-RISK CHANGES:** anything touching `MainViewModel` internals (avoid), grid row-write threading (keep Dispatcher rule), prompt-prefix ordering (KV cache), `MergeAndSplitHelper` semantics (guarded by tests + new Arabic fixtures P10).
- **LOW-RISK CHANGES:** all new folders/services; batch-convert functions; docs; language JSONs; settings additions (defaults preserve behavior).
- **Backend replaceability:** guaranteed by keeping every new service programmed against `IAutoTranslator`/`ISpeechToTextEngine`; no service may import a concrete engine.

---

# 31. Priority Matrix

Scale: impact 1–5 (user value), risk L/M/H, complexity S/M/L. Priority = reasoned order, not a vague label.

| ID | Problem (short) | Change | Evidence | Impact | Risk | Cx | Deps | Touchpoints | Rationale | Rollback | Test required |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | G3 | Language-aware default prompts (Arabic punctuation) | §11.8-6 | 4 | L | S | — | ToolsSettings consumers | cheapest quality win for every Arabic translation | revert default | prompt-snapshot tests |
| 2 | A1 | Arabic-normalized search | §11.6 | 5 | L | M | — | FindService, StringExtensions | top editor gap for Arabic users | flag off | unit tests incl. tashkeel cases |
| 3 | G7 | Extend keep-untranslated (URLs/emails) | §10.5 | 3 | L | S | — | MergeAndSplitHelper | tiny, prevents mangled lines | revert | tests |
| 4 | A2/A3/A11 | Arabic punctuation/quotes post-pass | §24 | 4 | L | S/M | — | libuilogic post-transform + batch fn | native-looking output; reversible | function-only | unit + fixture tests |
| 5 | E1/E2 | Egyptian target selector + style profile preset | §12.2 | 5 | M | M | G4 | AutoTranslate combos, SeLlamaCppAdvanced | core product promise; additive | hide selector | UI tests + prompt tests |
| 6 | G1/G2 | TranslationJobService + checkpoint | §9,§17-9 | 5 | M | L | service extraction | new feature folder | the journey exists; resume safety | menu flag | job state-machine tests |
| 7 | G4/G5 | Project glossary + TM | §10.6 | 4 | M | M | — | libuilogic services | consistency; cheap re-runs | delete store files | store tests |
| 8 | G6/A5 | QA gate + bidi linter | §10.5/§11 | 4 | L | M | A-libs | apply path, ErrorList | catches broken exports early | gate optional | fixture tests |
| 9 | A6 | Linux Arabic font fallbacks | §11.6 | 3 | L | S | — | Program.cs | parity with CJK handling (#11355) | remove entries | manual matrix |
| 10 | G11/G13/G12 | Download guard/throttle/model page | §15 | 3 | L | S/M | — | DownloadHelper, new page | low-bandwidth trust | hide page | logic tests |
| 11 | U2/G28 | Local/Online badges + consent | §13-5,§19 | 4 | L | S | — | engine metadata | SKILL.md trust boundary honored | hide | — |
| 12 | G14/G24 | Parallel cloud translate + Retry-After | §16 | 3 | M | M | — | DoAutoTranslate loop | big wall-clock win (opt-in) | default off | race/cancel tests |
| 13 | P2/G17 | Batch checkpointing + session restore | §17 | 4 | L | M | 6 | AdvancedTranslatorBase, VMs | crash resilience | ignore files | resume tests |
| 14 | A8/P10 | Arabic burn-in golden tests + split fixtures | §18 | 3 | L | M | CI ffmpeg | tests | protects riskiest Arabic paths | n/a | the tests |
| 15 | E4 | ASR dialect corrector stage | §21-8 | 4 | M | M | 6 | new transform + AI-review svc | evidence-backed Egyptian quality lift | off by default | diff tests |
| 16 | E10/O2/O10 | Eval set + back-translation QA + idiom heatmap | §25/§28 | 3 | M | M | E2 | new tools | objective quality compass | research-only | harness tests |
| 17 | E5 | Speaker conditioning | §12.5 | 4 | H | L | 6, diarization | protocol + UI | unlocks character voice | feature incomplete→ship behind flag | schema tests |
| 18 | G27 | Remote model catalog | §10.4 | 2 | M/H | M | hash infra | ModelCatalog, supply chain | freshness; security-sensitive | keep code list | signature tests |

---

# 32. Recommended Roadmap

**Phase 0 — Foundations (no product change):** G19 bridge test; G18 doc counts; A8/P10 test seeds; P9 review checklist. (SKILL.md-sized diffs; each with tests.)

**Phase 1 — Arabic editor excellence (1–2 dev-months equiv.):** A1, G3, G7, A2/A3, A5, A6, A4, U(export presets). Gate: Arabic test fixtures in CI.

**Phase 2 — The pipeline (2–3 dev-months equiv.):** extract stage services → G1 job wizard (Beginner), G2/G17 checkpoint/resume, G16 dashboard, U2/G28 badges, G9 error cards, G11/G12/G13 download guardrails. Advanced windows untouched.

**Phase 3 — Egyptian Arabic layer (2–3 dev-months equiv.):** G4 glossary files, E1/E2 selector+profile, E3 glossary seed, G5 TM, G6 QA gate, E4 corrector, E8 TTS routing, E12 prompt library, E10 eval set → publish first engine scorecard.

**Phase 4 — Scale & power:** G14/G24 parallel cloud, P6 overlapped stages, E5 speaker conditioning (schema first, UI later), O1 dual-register columns, G27 model catalog (only with signed manifests).

**Continuous:** track upstream MIT Subtitle Edit for relevant fixes (divergence ledger), run E10 scorecard per new curated model, enforce §20.4 license rules on every new curated model.

---

# 33. Self-Challenge / Open Questions

**What did I assume without verifying?**
- That the translation *apply-back* path writes through undo/history (U12) — apply code not read line-by-line (⚠️ §9-B).
- That "no translation memory exists" holds repo-wide — verified by grep over `src`, not by reading all 36 engines (reasonable confidence, stated as grep-based).
- Package licenses in §20.2 from general knowledge, not per-package fetches — flagged there.
- Perf numbers in §21 items 2/5 are vendor/secondary-reported.

**Which recommendation rests on the weakest evidence?**
- O2 back-translation QA and O10 idiom heat-map — quality gains are HYPOTHESIS; E10 eval set must precede them. Similarly P6 overlapped stages ("20–40%") is an unmeasured guess.

**Which proposal may be unnecessarily complex?**
- E5 speaker conditioning (protocol+UI+diarization) — high complexity; an alternative is prompt-only conditioning via glossary/synopsis for v1. Also G27 (remote model catalog) — supply-chain risk may outweigh freshness benefit; could stay code-frozen.
- The full wizard may be over-engineering if a single well-orchestrated dialog with 4 steps achieves 90% of the value — build G1 as *dialog-first*, expand only on evidence.

**Which external source could be biased?**
- Vendor-adjacent performance claims (faster-whisper ×4; TranslateGemma-vs-Gemma3 comparisons are Google-reported). The arXiv leaderboard (21-7) is academic but its dialect subsets are small; the IU journal study (21-8) has modest effect sizes and self-citation risk — treat directionally, not numerically.

**Which current subsystem might already solve the problem I proposed to solve?**
- "Style profiles" — partially covered today by editable per-engine prompts + advanced synopsis/glossary; my proposal adds persistence/sharing, but users *can* approximate E2 today by pasting a prompt. The wizard's privacy labels — partially covered by README claims; the delta is in-flow visibility, which is genuinely absent.
- Checkpointing — auto-backup already protects *subtitle files*; my G2 adds job-state, not file safety, which exists.

**Which idea is duplicated elsewhere in the repository?**
- AI-based proofreading exists (Tools ▸ AI review — docs + folders, REPOSITORY_FACT). E4 corrector and O5/O8 must *reuse* that engine, not build a parallel one. Batch-convert already has FixRightToLeft — A5's linter must complement, not duplicate, it.

**Strongest argument against the proposed direction?**
- "SE is an *editor*; pipeline products (pyvideotrans/VideoLingo) already exist and compete on speed; investing there dilutes the editor's superiority." Counter: the audit shows the pipeline parts are 80% present in-repo and uniquely Arabic-dialect-aware UX is unbuilt anywhere audited; the wizard *composes* existing strengths rather than replacing the editor. But the counter stands for scope: Phase 2 must not regress editor quality (SKILL.md surgical rule).

**What did I fail to inspect?**
- Undo/show-history internals; plugin runtime; waveform renderer internals; MKV/TS container parsers; all 36 translation engines (8 deep); all 242 Process.Start sites; apply-back path; per-format Arabic corners beyond PAC/Cavena/Ebu/Idx; Docker/flatpak build contents; upstream diff (fork vs upstream HEAD). All are recorded in §7/§34, and none of the report's conclusions silently depends on them.

---

# 34. Audit Limitations (explicit; gate rule 27)

1. **No .NET SDK in the sandbox** → no build, no test run, no benchmark numbers produced by this audit. All performance classifications reflect code evidence or labeled inference. The completion-gate item "external research performed" is satisfied; "measured behavior" claims are absent by construction and marked as such.
2. **Sampling depth:** of 36 translation engines, 8 were read in full and the rest at list/config level; of 242 process-spawn sites, ~10 audited; `MainViewModel.cs` (34,977 L) sampled, not fully read. Subsystems marked Skim/Not-audited in §7 must not be cited as "deeply analyzed".
3. **Single-commit history:** provenance vs upstream verified only externally (release posts); no commit-level diffing between this import and upstream HEAD was performed (would require network fetch of upstream — out of scope for this phase).
4. **⚠️ marks** in §9 denote hops not verified line-by-line (STT hand-off to grid; translate apply-back; batch per-function loop; per-engine cancel).
5. **License lookups:** primary licenses fetched where stated (llama.cpp, Gemma terms, upstream SE); several marked UNKNOWN-verified (§20) must be checked before any redistribution decision.
6. **No Arabic-content rendering was visually verified** (no GUI/ffmpeg here); RTL rendering conclusions rest on code + tests present in-repo.
7. **External search snippets vs primary sources:** where only secondary sources agreed (NLLB CC-BY-NC), the primary model card was not fetched; confidence recorded accordingly.
8. **Numbers with provenance:** tracked files 4,455; top-level 17; src ui/libse/libuilogic/seconv LOC 441,956/167,892/39,040/12,764; tests 1,022 files/154,987 LOC; Arabic.json 4,043 vs ~4,066 English keys; SubtitleFormats 429 files / 397 `: SubtitleFormat` grep matches; RightToLeft 243 matches/30 files. All reproducible with the commands shown in §2–§3/§7/§11.

**Completion gate assessment:** all §27 conditions are met with the limitations above recorded rather than hidden; no gate item was silently skipped. No source implementation files were modified; no implementation changes committed; the only artifact is this report (plus its single-file commit).

*End of audit.*
