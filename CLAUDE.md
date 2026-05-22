# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`quest` is a .NET 8 CLI that reads an Obsidian-style markdown task journal and renders a day-oriented status view. The journal format groups tasks under day headers (`# TODAY`, `# TOMORROW`, `# YESTERDAY`) and category subheaders (`## MAINQUESTS`, `## SIDEQUESTS`); tasks use checkbox marks where each mark maps to a status (see README for the full set: `[ ]` Open, `[>]` Active, `[~]` Cancelled, `[!]` Warning, `[x]` Completed, plain bullets become Comments).

The user runs this against a real questlog whose path lives in `~/.config/quest-journal/config.json`. Treat that config as the source of truth for which file `quest` operates on.

## Build, test, run

```
dotnet build
dotnet test
dotnet test tests/QuestJournal.Core.Tests/QuestJournal.Core.Tests.csproj --filter "FullyQualifiedName~ChangeDetectorTests"
dotnet run --project src/QuestJournal.Cli -- status
```

The CLI's assembly name is `quest` — the built binary lives at `src/QuestJournal.Cli/bin/Debug/net8.0/quest`. Invoke it directly when smoke-testing to avoid the `dotnet run` first-time banner polluting stdout.

Tests are xUnit. Fixtures (e.g. `tests/QuestJournal.Core.Tests/Fixtures/sample-tasks.md`) are copied to the test output via `<None Update>` + `CopyToOutputDirectory=PreserveNewest` and located at runtime through `AppContext.BaseDirectory`. The CLI's bundled `Assets/nvim.lua` exrc uses the same pattern.

## Architecture

Two projects:

- **`QuestJournal.Core`** — parser, model, change tracking. No CLI/Spectre dependencies; safe to add tests against.
- **`QuestJournal.Cli`** — `Program.cs` dispatches subcommands by string (`status`, `edit`); `Commands/*Command.cs` each own one verb. Spectre.Console for rendering.

### Parsing

`JournalParser.Parse(string)` / `.ParseFile(path)` returns a `JournalDocument { Days, FrontmatterLines }`. The hierarchy is `JournalDocument → DaySection → CategorySection → Quest`. `Quest` is an immutable record carrying `Status`, `Text`, `Children`, `IndentDepth`, and `LineNumber` (1-based). Indentation is normalized (tabs → depth, 4-space groups). Numbered list items (`1.`) parse as Comment-status quests rather than erroring.

### Change tracking & XP

`Core/ChangeTracking/` implements a snapshot-based diff that runs on every `quest` invocation. Conceptual model:

- **Identity**: `TaskKey(Day, Category, Ancestors[], Text)` — path-based, not line-based, used for *within-snapshot* uniqueness so two real siblings with identical text under different parents stay distinct. Editing a task's text shows up as `Removed + Added` (documented v1 trade-off).
- **Snapshot**: `~/.local/share/quest-journal/state.json` (respects `XDG_DATA_HOME`). Holds the flattened task list, the configured journal path, and a monotonic `totalXp`. Atomic write via temp-file-then-rename. Corrupt file → treated as missing.
- **Detector**: `ChangeDetector.Detect(prior, current) → ChangeSet` of `Added`/`Removed`/`StatusChanged`. Has a post-pass — `CollapseMoves` — that pairs `Removed + Added` events by `Text` alone (FIFO when there are duplicates). **Move tracking is location-agnostic**: re-indenting, re-parenting, changing day (TODAY ↔ TOMORROW ↔ YESTERDAY), and changing category (MAINQUESTS ↔ SIDEQUESTS) all produce no diff event and no XP when the status is unchanged. When a move coincides with a status change, the pair collapses to a single `StatusChanged` on the destination key.
- **XP**: `XpCalculator.Award` per change. Headline rules: `Completed`=+10, `Cancelled`=+1, other status changes=+2, added=+1, added-`Comment`=0, removed=0. A task moved to YESTERDAY and marked Completed in one edit still earns +10 — the collapse routes it through `StatusChanged → Completed`, which already pays 10. A genuinely-new completed-on-YESTERDAY task also pays 10 via the `Added + Completed + YESTERDAY` rule.
- **Pipeline**: `Cli/ChangeTracking/ChangeTrackingPipeline.RunAfter(doc, journalPath, writeSnapshot)` is the single entry point used by both commands. Returns a `PipelineResult { XpAwarded, TotalXp, HasChanges }` so the caller can place the XP footer separately (the diff tree always prints inline; the footer is positioned by the command).

**Day-model rules should be source-day-agnostic.** When designing new behavior tied to TODAY/TOMORROW/YESTERDAY, default to "what's the destination + final state" rather than hardcoding a source. More broadly, when designing rules about cross-snapshot task identity, treat `Text` as the identity and treat day/category/parent purely as location attributes.

### Rendering

- `Rendering/GlyphTheme.cs` — two themes (`Ascii`, `NerdFont`) chosen by the `nerdFontGlyphs` config flag. Adding a new glyph means a positional field on the record plus a value in both static instances. Avoid rewriting whole nerd-font glyph lines via `Write` or `Edit` — the private-use-area code points can get mangled in transit; prefer targeted edits on the surrounding ASCII context, or use `\uXXXX` escapes.
- `Rendering/QuestStyles.cs` — shared `StyleGlyph`/`StyleText` helpers used by both `StatusRenderer` and `DiffRenderer`. Keep styling consistent between status and diff views by routing through here.
- `Rendering/DiffRenderer.cs` — splits the work: `RenderDiffTree(ChangeSet)` draws the Spectre.Console `Tree` (only when there are changes), `RenderXpFooter(xpAwarded, totalXp)` prints the sparkle line. Callers decide ordering.

### Output ordering

- `quest status`: diff tree (if any) → blank → day status output → blank → XP footer (always).
- `quest edit`: launches `$EDITOR` with `WorkingDirectory = questlogDir` (so nvim's `:set exrc` picks up the adjacent `.nvim.lua`), then re-parses the file and runs the pipeline. Renders diff tree + XP footer **only if there were changes** (silent edit = silent output).

### `quest edit` exrc bootstrap

When `Path.GetFileName($EDITOR) == "nvim"`, the command copies the bundled `src/QuestJournal.Cli/Assets/nvim.lua` to `<questlog-dir>/.nvim.lua` if one isn't already there. Failure to copy (IOException) prints a yellow warning and continues; it never blocks the edit. Other editors get launched as-is with no setup.
