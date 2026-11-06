# Project Instructions

Always read and follow [src/.github/copilot-instructions.md](src/.github/copilot-instructions.md) — it contains the project's coding conventions, architecture notes, and domain rules (multi-tenancy, guest system, recurring bookings, badges, etc.). Treat it as binding for all work in this repo.

## Working on GitHub issues

This applies when you run in GitHub Actions (the `Claude agent` workflow), triggered by a new issue or an `@claude` comment. The only person who can trigger you is the owner, `sleepwalkerffs`.

**Plan first, build after approval.**
- On a new issue, or when asked for a plan: read the issue and the relevant code, then post one comment with the plan. It covers what you understood, the approach, the files you expect to touch, open questions and how you will test it. Change no code in that run.
- Implement only after the owner approves in a comment (for example "@claude go", "looks good, build it"), or when a comment clearly asks for a change right away (for example feedback on your pull request). If the request is unclear or a domain decision is open, ask in the comment instead of guessing.
- Write comments in the language of the issue (usually German or English).

**When implementing:**
- One issue, one branch, one pull request. If the issue already has an open pull request (`gh pr list --search "<issue number>"`), check out its branch with `gh pr checkout <number>`, commit there and push it with the action's push script (`origin <branch name>`, plain `git push` is not allowed). The action may have created a fresh `claude/...` branch for this run: leave it empty, don't spread one issue over several branches.
- Otherwise work on your own `claude/...` branch and target `master` with the pull request. Never push to `master`, `stage` or `production`. Pushing to `stage` or `production` deploys.
- When you are done implementing, open the pull request yourself with `gh pr create --base master --body-file <file>` (body from `.github/PULL_REQUEST_TEMPLATE.md`, `Closes #<issue>`). Keep its description up to date with `gh pr edit` when you change the pull request later. Never merge pull requests.
- Build and test from `src/`: `dotnet build`, then `dotnet test --no-build`. A PostgreSQL service runs at `127.0.0.1:5432`, and the tests create their own databases in it. Do not finish with a red build or failing tests. If you can't fix a failure, say so in the comment.
- Follow `src/.github/copilot-instructions.md` and the pull request template: tests for new or changed handlers, new texts in English and German, mobile and desktop, light and dark mode.
- Check UI changes in the browser before you finish (see "Checking the UI" below). In the pull request, say what you looked at and what the owner should still click through.
- Payments, credits, prices and purchases stay cookie-only and must never be reachable with club API keys.
- Never add secrets, keys, real member data or database dumps. Never change `.github/workflows/` unless the issue asks for it.
- Keep the change to what the issue asks. Mention other problems you notice in the comment instead of fixing them on the side.
- Use the subagents in `.claude/agents/` where they fit (for example Database Optimizer for migrations and queries, Internationalization Engineer for texts). Before opening the pull request, have the Code Reviewer agent review your diff and fix what it finds.
- Finish with a comment that covers what changed, how it was tested, what is left, and the link to the pull request.

### Checking the UI

The workflow gives you a headless Chrome through the Playwright MCP server (`mcp__playwright__*` tools) and a script that runs the app with invented test data:

- `ui-check-app` (a command on the PATH that runs `scripts/ui-check/start-app.sh`) builds the current code, starts the app at `http://localhost:5080` (own database `clubnetz_ui`, log in `/tmp/clubnetz-ui.log`) and runs the seed (`ui-check-seed`). Run it again after changing code, and `ui-check-app stop` to stop it. It takes a few minutes the first time, so give the Bash call a timeout of 10 minutes.
- Bash permissions match the command text. Run `ui-check-app` as its own command, from any directory: no `cd ... &&`, no `bash ...`, no pipe or redirect after it. Read the logs with the Read tool. If a command is still denied, quote the exact command in your comment.
- Test accounts in club 1 ("TC Vorderland", 7 courts, play modes Einzel and Doppel), all active members of the current season, password `UiCheck-2026!`: `anna.admin@example.test` (club admin), `ben.member@example.test`, `clara.member@example.test`, `david.member@example.test`.
- Add the data your feature needs with `psql -h 127.0.0.1 -U postgres -d clubnetz_ui`, or better through the UI or API as one of the test users. If the seed is missing something every feature needs, extend `seed.sh` in your pull request.
- Log in at `/account/login`. Check every new or changed page and dialog at 390x844 (phone) and 1280x800 (desktop, `browser_resize`) and in light and dark mode (set `localStorage['bookennis-theme']` to `light` or `dark`, then reload). Click through the main flow, including errors and empty states, and look at the browser console for errors.
- Save the screenshots you base your judgement on with `browser_take_screenshot` (a short file name per view, e.g. `play-requests-mobile-dark.png`). They are uploaded as the artifact `ui-check-<run id>` of the workflow run; link the run in your comment.
- Fix what you find before you finish. Don't use real member data or any other server, only this local app (the browser blocks every other site).
