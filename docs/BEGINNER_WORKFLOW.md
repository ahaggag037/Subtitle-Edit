# Beginner Workflow: Translate a Video (guided)

Menu: **Translate ▸ Translate video (guided)**.

This path is designed so a first-time user never sees an engine field, an API key field
or a settings tab. Experts: everything you know is still there — this window only
*orchestrates* existing features.

## The 8 steps, as the window asks them

1. **Choose the video** — pick your file. Nothing is uploaded; this is only used to find
   a matching subtitle automatically.
2. **Subtitle or transcript** — if a `.srt` with the same name sits next to the video it
   is picked up automatically. Otherwise pick any subtitle file, or a plain `.txt`
   transcript (each text line becomes one subtitle, evenly timed).
   *No transcript yet?* Use **Video ▸ Speech to text** first. If the speech engine needs
   a one-time download, that window tells you exactly what will be downloaded, how big
   it is and where it goes before anything happens.
3. **Engine and privacy** — each choice is labeled `LOCAL`, `ONLINE` or `LOCAL/ONLINE`
   with one honest sentence about what leaves your computer:
   - `LOCAL` (e.g. llama.cpp / Ollama running on your machine): nothing leaves.
   - `ONLINE` (e.g. ChatGPT / Gemini / DeepL / Google): the subtitle *text* is sent to
     the service; video and audio never are.
   - `LOCAL/ONLINE` (OpenAI-compatible URL): depends on the address you configured.
4. **Translate to** — the list shows only languages the selected engine really
   supports. For engines that support Arabic you get Modern Standard Arabic and
   Egyptian Arabic as separate choices.
5. **Style** (optional, prompt-capable engines only): Default / Formal / Conversational /
   Cinematic / Documentary / Technical. Styles are instructions to the engine, nothing
   more.
6. **Reuse previous translations** — a local translation memory, so re-running a job is
   faster and consistent. Stored in your app data folder; never synced.
7. **Translate** — staged progress (Preparing → Translating → Quality checking →
   Exporting) and an honest time estimate only when one can actually be computed.
   Cancel any time; progress is checkpointed and a failed/cancelled job can be resumed
   later from the checkpoint file (`<output>.job.json`).
8. **Result** — the translated `.srt` path, a quality-check summary, and
   **Open result in editor** to review/fix lines in the normal Subtitle Edit editor
   (and, from there, burn the subtitles into the video as before).

## What the beginner never has to do

- Choose models, URLs or API keys (the engine entries use the same credentials the
  expert Auto-translate window manages; the guided path never *asks* for them).
- Understand chunking, prompt templates or RTL Unicode details — Arabic punctuation,
  quotes and bidi cleanup are applied automatically, and the quality check reports
  anything worth a human look.
- Trust unverifiable claims: time estimates appear only when computable; engine
  privacy labels are static facts about where the engine sends data.

## Known limits (honest)

- The guided window does not start transcription itself yet — step 2 hands you to the
  existing Speech-to-text window when there is no transcript.
- Resume-after-crash works via the checkpoint file at the API level; the window does
  not yet offer a "resume" button (a failed/cancelled run can simply be re-run;
  completed lines are reused when translation memory is on).
- Styles and dialect instructions are prompts: a small local model may answer in Modern
  Standard Arabic anyway. That is a model capability limit, shown here as-is rather
  than papered over.
