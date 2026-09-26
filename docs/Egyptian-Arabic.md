# Egyptian Arabic Support

Location: `src/libuilogic/Translate/Arabic/` (`ArabicProfileCatalog.Egyptian`,
`ArabicTextPostProcessor`). UI: the guided wizard offers an "Egyptian Arabic" target
whenever the selected engine's real language list contains Arabic.

## What the Egyptian profile does

- Target code `arz`; displayed as its own target so a beginner can *choose* dialect
  explicitly instead of hoping a model "sounds Egyptian".
- Prompt addendum instructing natural Egyptian colloquial Arabic (daily-life register,
  common idioms, light slang where the source is casual, humor preserved as intent not
  literal). The addendum is appended through the engine's existing prompt seam and never
  contains `{`/`}` placeholder-conflicting characters.
- Post-processing applies the Arabic text policies (punctuation `، ؟ ؛`, guillemets,
  digit policy) — identical machinery to the MSA profile; Egyptian-ness lives in the
  prompt and the model, not in string transforms.

## Honest capability statement (no invented numbers)

- Whether the output is genuinely Egyptian depends on the translation model's training,
  not on this profile. Small local models often answer in Modern Standard Arabic even
  when asked for dialect; this is a **current model limitation**, and the profile makes
  it visible rather than hiding it.
- Public ASR research (e.g. Whisper large-v3 evaluations on Egyptian Arabic reported in
  academic comparisons) shows far higher word error rates for Egyptian than for MSA —
  meaning the *transcription* step (Video ▸ Speech to text) is usually the weakest link
  for Egyptian content, before translation even starts. Those figures are per-model and
  per-corpus; Subtitle Edit does not hard-code any expectation.
- Character-gender and voice consistency for dubbing ("character voice") is not part of
  this phase; diarization-dependent speaker styles are future work (see the Phase B
  final report, DEFERRED items).

## Re-translating MSA → Egyptian ("Egyptianify")

The same profile machinery covers converting an existing MSA translation into Egyptian
colloquial with a prompt-capable engine, preserving timing and numbering: translate the
MSA file as the source text with the Egyptian target selected. Timing is never touched;
the QA pass runs on the result. (Ad-hoc usage today; a dedicated one-click action is
future work.)
