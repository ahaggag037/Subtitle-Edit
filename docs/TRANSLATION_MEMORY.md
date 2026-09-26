# Translation Memory & Glossary

Location: `src/libuilogic/Translate/Memory/` (`TranslationMemory.cs`,
`TranslationMemoryEntry.cs`, `Glossary.cs`). Tests: `tests/libuilogic/Translate/...`
(see `TranslationMemoryTests.cs`). Verification: unit tests written, **not yet executed**
(no .NET SDK in the authoring sandbox — see the Phase B final report).

## Translation memory

- Store: one JSON file, `translation_memory.json`, in the folder returned by
  `TranslationMemory.DefaultFolderPathProvider` (wired to `Se.DataFolder` in
  `Program.cs`). A corrupt file is tolerated: the memory starts empty rather than
  crashing the app.
- Entry: source text, target text, style/profile id, engine name, UTC timestamp.
- Matching (`NormalizeForMatch`): trim, collapse whitespace, lowercase, strip Arabic
  diacritics (tashkeel U+064B–U+0653) and tatweel (U+0640). "Marhaba" and "مرحبا"
  variants therefore match across vocalization differences.
- `Add` is latest-wins: re-translating the same source updates the entry instead of
  duplicating it.
- `Lookup` is style-aware: an entry recorded with a style/profile id is preferred for
  lookups in the same style; plain entries are the general fallback.
- Disabled entries are kept but never matched (lets a user retire a bad pair without
  losing history).
- The job runner uses TM as a **prefill**: rows found in TM never hit the engine, and
  they are not recorded back (avoiding self-confirming loops). Toggle in the wizard
  ("Reuse previous translations").

## Glossary

- One JSON file per media/subtitle: `<name>.glossary.json` next to the subtitle
  (`Glossary.GetDefaultFilePath`). Terms are `term / translation / notes / enabled`.
- `FindMatches` normalizes with the same rules as the TM for Arabic text.
- `ToPromptText` renders "term = translation" lines for prompt-capable engines; the job
  runner appends it to the engine prompt through the existing prompt seams
  (`EnginePromptSnapshot` in the wizard; `Configuration.Settings.Tools.*Prompt` for
  classic engines, `Se.Settings.AutoTranslate.LlamaCppAdvanced.Prompt` for the advanced
  engines).
- Glossary violations surface as QA findings (`GlossaryViolation`) — advisory
  (WARNING), never auto-rewritten.

## What this is not

- No cloud sync, no shared multi-user store, no automatic terminology mining. The
  glossary is hand-maintained (or seeded by tools later). Keeping it local is also the
  privacy posture: subtitle text never leaves the machine because of the TM/glossary
  files themselves.
