# Quest Journal
## Overview
- Quest Journal is a console app for managing work tasks in the form of an Obsidian-style markdown file. There are three main days:
    - Today - Things that are actively being worked
    - Tomorrow - Things on the back burner, or things planned but not being worked
    - Yesterday - Things that have been completed
- In addition to days, there are subcategories of task:
    - Mainquests - Things that are high priority and should be the first thing worked, like the main quest in a video game.
    - Sidequests - Things that are lower priority, or smaller tasks, and can be completed whenever.

## Example
```md
# TODAY
## MAINQUESTS
- [>] Implement that feature
    - Do we need to worry about the side effects?
    - [ ] Worry about side effects
    - [~] Talk to Dave
        - Dave is busy
## SIDEQUESTS
- [ ] Vibe code a personal app instead of working

# TOMORROW
## MAINQUESTS
- [ ] Handle that issue for QA
- [ ] Something else to do in the future
## SIDEQUESTS
...

# YESTERDAY
## MAINQUESTS
- [x] A task I completed
    - [x] Subtask 1
## SIDEQUESTS
...
```

## Tasks
- Tasks can have multiple types of checked, which changes how they're handled:
    - `- [ ]` - not being worked
    - `- [>]` - being worked
    - `- [~]` - cancelled
    - `- [!]` - warning
    - `- [x]` - completed
    - `- `    - simple bullet, often just a comment related to the parent
    - Each of these possible task statuses can be nested within other tasks

## Functionality
- For now, I want this to primarily be a dotnet8 cli and tui project with a parser based on the format used in `/media/sf_OneDrive_KnowledgeLake/Obsidian/Weekly/Tasks.md`. I've been using this format for awhile and it feels nice on the ADHD brain.
### Status - e.g. `quest status <day>`
- Prints the top-level bullets/checkboxes from the provided day, displaying all quests and sidequests. Defaults to Today, but has an -a flag to show all days.

