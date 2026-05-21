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

## XP
- Most actions involving tasks will give you XP, especially completing tasks. XP is just for fun, nothing to do but to collect it. XP is tracked and diff calculated whenever you run a `quest` command.
