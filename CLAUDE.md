# Project Instructions

Always read and follow [src/.github/copilot-instructions.md](src/.github/copilot-instructions.md) — it contains the project's coding conventions, architecture notes, and domain rules (multi-tenancy, guest system, recurring bookings, badges, etc.). Treat it as binding for all work in this repo.

## Working on GitHub issues

This applies when you run in GitHub Actions (the `Claude agent` workflow), triggered by a new issue or an `@claude` comment. The only person who can trigger you is the owner, `sleepwalkerffs`.

**Plan first, build after approval.**
- On a new issue, or when asked for a plan: read the issue and the relevant code, then post one comment with the plan. It covers what you understood, the approach, the files you expect to touch, open questions and how you will test it. Change no code in that run.
- Implement only after the owner approves in a comment (for example "@claude go", "looks good, build it"), or when a comment clearly asks for a change right away (for example feedback on your pull request). If the request is unclear or a domain decision is open, ask in the comment instead of guessing.
- Write comments in the language of the issue (usually German or English).

**When implementing:**
- Work on your own `claude/...` branch and target `master` with the pull request. Never push to `master`, `stage` or `production`. Pushing to `stage` or `production` deploys.
- Build and test from `src/`: `dotnet build`, then `dotnet test --no-build`. A PostgreSQL service runs at `127.0.0.1:5432`, and the tests create their own databases in it. Do not finish with a red build or failing tests. If you can't fix a failure, say so in the comment.
- Follow `src/.github/copilot-instructions.md` and the pull request template: tests for new or changed handlers, new texts in English and German, mobile and desktop, light and dark mode.
- You can't check the UI in a browser here. Say so in the pull request and describe what the owner should click through.
- Payments, credits, prices and purchases stay cookie-only and must never be reachable with club API keys.
- Never add secrets, keys, real member data or database dumps. Never change `.github/workflows/` unless the issue asks for it.
- Keep the change to what the issue asks. Mention other problems you notice in the comment instead of fixing them on the side.
- Use the subagents in `.claude/agents/` where they fit (for example Database Optimizer for migrations and queries, Internationalization Engineer for texts). Before opening the pull request, have the Code Reviewer agent review your diff and fix what it finds.
- Finish with a comment that covers what changed, how it was tested, what is left, and the link to the pull request.
