# Meepliton — Git & GitHub Workflow Skill
# .claude/skills/GIT-WORKFLOW.md
#
# Attach this to any Claude conversation when you need help with git,
# GitHub, or getting your changes into the repository.
# This skill is written for contributors who are NOT professional developers.

---

## The Mental Model

Think of git like a save system for a whole folder of files, where every save has a label and can be undone.

```
Your computer           GitHub (the shared copy)
─────────────           ────────────────────────
Working files    →  Stage  →  Commit  →  Push  →  Pull Request  →  main
(editing now)       (pick      (save       (send     (ask to merge
                    what to    locally)    up)        into main)
                    save)
```

The golden rule: **never push directly to `main`**. Always work on a branch, then open a Pull Request. This way, CI checks run and someone else can review before your changes affect everyone.

---

## Before You Start: Checking Your State

Always run this first — it tells you where you are and what's changed:

```bash
git status          # what files have changed
git branch          # what branch you're on (current branch has *)
git log --oneline -10   # last 10 commits (q to quit)
gh pr status        # any open PRs involving you
```

If `git status` shows you're on `main`, stop and create a branch before making any changes (see "Starting New Work" below).

---

## One-Time Setup (do this once per machine)

```bash
# Tell git who you are (used in commit messages)
git config --global user.name "Your Name"
git config --global user.email "your@email.com"

# Set default branch name to main
git config --global init.defaultBranch main

# Make git store your GitHub credentials so you don't retype them
gh auth login
# → follow the prompts, choose GitHub.com, choose HTTPS, paste your token

# Clone the repo (only needed once)
gh repo clone [username]/meepliton
cd meepliton
```

---

## Starting New Work

Every piece of work gets its own branch. Branch names should be short and descriptive.

```bash
# Make sure you're starting from a fresh main
git checkout main
git pull origin main

# Create and switch to a new branch
git checkout -b add-sushigo-game
# or: git checkout -b fix-skyline-tile-bug
# or: git checkout -b update-lobby-style

# You're now on your branch. Start making changes.
```

**Branch naming conventions for Meepliton:**
- `add-{gameid}-game` — for new game modules
- `fix-{description}` — for bug fixes  
- `update-{description}` — for improvements to existing things
- `docs-{description}` — for documentation only

---

## The Daily Loop: Stage → Commit → Push

After making changes to files:

```bash
# 1. See what changed
git status

# 2. Stage the files you want to include in this commit
git add src/games/Meepliton.Games.Sushigo/    # add a whole folder
git add apps/frontend/src/games/sushigo/   # add another folder
git add -p                                  # interactive: pick individual chunks

# 3. Check what you've staged looks right
git diff --staged

# 4. Commit with a clear message
git commit -m "Add initial Sushi Go game module

- Add SushigoModule.cs with CreateInitialState and Handle
- Add SushigoState and SushigoAction records  
- Add React frontend component with card hand display"

# 5. Push to GitHub
git push origin add-sushigo-game
# First push on a new branch? Use:
git push -u origin add-sushigo-game
# (-u sets up tracking so future pushes just need: git push)
```

**Good commit messages:**
- Start with a short summary (under 72 chars) that completes "This commit will..."
- Add a blank line then bullet points for details if needed
- ✓ `"Add Sushi Go initial state and player hand logic"`
- ✓ `"Fix tile placement crash when board is full"`  
- ✗ `"stuff"` / `"changes"` / `"wip"` (not helpful to others)

---

## Let Claude Write Your Commit Message

After staging your changes (`git add`), run:

```bash
git diff --staged
```

Copy the output, paste it into a Claude conversation, and say:
> "Write a commit message for these changes following the Meepliton conventions."

Or use Claude Code directly:
```bash
# Stage changes yourself, then let Claude write the commit
git add -p
claude -p "Look at my staged changes and write a good commit message" \
  --allowedTools "Bash(git diff *),Bash(git status *),Bash(git commit *)"
```

---

## Opening a Pull Request

When your work is ready for review:

```bash
# Make sure everything is pushed
git push

# Open a PR using the gh CLI
gh pr create \
  --title "Add Sushi Go game module" \
  --body "Adds a complete Sushi Go implementation.

## What this adds
- Full game rules engine (SushigoModule.cs)
- Card drafting mechanic with pass-left/pass-right
- React frontend with card hand and played cards display
- Mobile-friendly layout

## Testing
Tested locally with 3 players. All scoring rules verified against rulebook.

## Screenshots
[paste a screenshot here if you have one]" \
  --base main

# Or just open an interactive PR form in the browser
gh pr create --web
```

**What happens next:**
1. GitHub runs the CI checks automatically (build + test)
2. Someone reviews your PR
3. If CI passes and the reviewer approves, they merge it
4. Your changes are now live in `main`

---

## Watching Your PR's CI Status

```bash
gh pr status          # your open PRs and their CI status
gh pr checks          # detailed check results for your current branch's PR
gh run list           # list recent GitHub Actions runs
gh run view           # see the output of a run
```

If CI fails:
```bash
gh run view --log-failed   # show only the failed parts
# Fix the issue, commit, push — CI reruns automatically
```

---

## Keeping Your Branch Up to Date

If `main` has changed while you've been working (common when multiple people contribute):

```bash
# Bring in the latest changes from main
git checkout main
git pull origin main
git checkout add-sushigo-game
git rebase main
# or: git merge main  (rebase is cleaner; merge is safer if you're not sure)

# If there are conflicts, git will pause and show you what to fix.
# See "Resolving Conflicts" below.

# After rebase, you need to force-push (because history changed)
git push --force-with-lease
```

---

## Resolving Conflicts

A conflict means two people edited the same part of the same file. Git marks the conflict in the file like this:

```
<<<<<<< HEAD (your changes)
const playerCount = 4;
=======
const playerCount = 6;
>>>>>>> main (changes from main)
```

To resolve it:
1. Open the file — look for `<<<<<<` markers
2. Edit the file to keep what's correct (delete the markers and one version)
3. `git add` the resolved file
4. `git rebase --continue` (or `git merge --continue`)

For complex conflicts, Claude can help:
```bash
git diff     # see all the conflicts
# Copy the conflict output, paste into Claude, ask it to resolve
```

---

## Recovering From Common Mistakes

### "I accidentally committed to main"
```bash
# Move your commit to a new branch instead
git checkout -b my-new-branch
git checkout main
git reset --hard origin/main  # ⚠️ this removes your commit from main locally
# your commit is now only on my-new-branch
```

### "I want to undo my last commit but keep the changes"
```bash
git reset --soft HEAD~1
# Your changes are still there, just unstaged
```

### "I want to completely undo my last commit and discard the changes"
```bash
git reset --hard HEAD~1
# ⚠️ This permanently deletes the changes in that commit
```

### "I made a mess of my branch and want to start fresh from main"
```bash
git checkout main
git pull
git branch -D my-messy-branch       # delete the messy branch locally
git checkout -b my-clean-branch     # start fresh
```

### "I pushed something I shouldn't have"
```bash
# If the branch hasn't been merged yet:
git reset --hard HEAD~1
git push --force-with-lease
# If it HAS been merged, tell Matt — don't force-push main
```

### "I'm in 'detached HEAD' state"
```bash
# You checked out a specific commit instead of a branch
# Just create a branch from where you are:
git checkout -b recovery-branch
```

### "My branch is showing 'diverged' from origin"
```bash
# This means your local and remote versions disagree
# If you're sure your local version is correct:
git push --force-with-lease
# If you're not sure, ask before force-pushing
```

---

## Useful Inspection Commands

```bash
# See the full history with graph
git log --oneline --graph --all

# See what changed in a specific commit
git show abc1234

# See who changed what line in a file
git blame path/to/file.cs

# Search through all commit messages
git log --oneline --grep="skyline"

# See what's different between your branch and main
git diff main...HEAD

# See all branches (local and remote)
git branch -a

# Find a commit by text in the changes
git log -S "SkylineModule" --oneline
```

---

## The `gh` CLI Cheatsheet

```bash
# Repos
gh repo clone [owner]/meepliton     # clone a repo
gh repo view                      # open repo in browser

# PRs
gh pr create                      # create a PR (interactive)
gh pr create --web                # create a PR in browser
gh pr list                        # list all open PRs
gh pr status                      # PRs involving you
gh pr view                        # view current branch's PR
gh pr checkout 42                 # switch to PR #42's branch
gh pr merge                       # merge current PR (if you have permission)
gh pr close                       # close without merging

# CI / Actions
gh run list                       # recent workflow runs
gh run view                       # view a specific run
gh run view --log-failed          # show only failed log lines
gh workflow list                  # list all workflows
gh workflow run deploy.yml        # manually trigger a workflow

# Issues
gh issue create                   # create an issue
gh issue list                     # list open issues
gh issue view 7                   # view issue #7

# General
gh auth status                    # check login status
gh auth login                     # log in / refresh credentials
```

---

## Meepliton-Specific Workflow Checklist

When adding a new game:

```bash
# 1. Start on a fresh branch
git checkout main && git pull origin main
git checkout -b add-{gameid}-game

# 2. Run the scaffold
./scripts/new-game.ps1 -GameId {gameid} -GameName "{Name}"

# 3. Implement the game (using Claude with NEW-GAME.md skill)

# 4. Build and test locally
dotnet run --project src/Meepliton.AppHost

# 5. Commit in logical chunks
git add src/games/Meepliton.Games.{Name}/
git commit -m "Add {Name} C# game module"

git add apps/frontend/src/games/{gameid}/
git commit -m "Add {Name} React frontend"

# 6. If the game needs migrations, add the CI step
# Edit .github/workflows/deploy.yml (see requirements.md §15.5)
git add .github/workflows/deploy.yml
git commit -m "Add {gameid} migration step to CI pipeline"

# 7. Open a PR
gh pr create --title "Add {Name} game module" --base main

# 8. Watch CI
gh pr checks --watch
```

---

## Getting Help

If you're stuck on something git-related, the best approach is to paste the output of `git status` and `git log --oneline -5` into a Claude conversation along with a description of what you're trying to do. That gives Claude enough context to give concrete, specific advice rather than generic instructions.
