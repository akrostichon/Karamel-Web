# Issue tracker: GitHub

Issues and specs for this repo live as GitHub issues. Use the `gh` CLI for all operations.

## Conventions

- **Create an issue**: `gh issue create --title "..." --body "..."`. Use a here-string or file for multi-line bodies.
- **Read an issue**: `gh issue view <number> --json number,title,body,labels,comments`.
- **List issues**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'`, with appropriate label and state filters.
- **Make an issue a sub-issue of a parent**: use GitHub sub-issues when available; otherwise add `Part of #<parent>` at the top of the child body.
- **Blocking**: use GitHub issue dependencies when available. Otherwise add `Blocked by: #<n>, #<n>` at the top of the child body. A ticket is unblocked when every blocker is closed.
- **Comment on an issue**: `gh issue comment <number> --body "..."`.
- **Apply / remove labels**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`.
- **Close**: comment with the explanation, then `gh issue close <number>`.

Infer the repo from `git remote -v`; `gh` does this automatically when run inside this clone. Before the first issue creation in a session, verify the target with `gh repo view --json nameWithOwner`. File issues only in the user's own repo, never in the skills-source repo.

## Pull requests as a triage surface

**PRs as a request surface: no.** Set to `yes` only if this repo decides to triage external PRs as feature requests.

## When a skill says "publish to the issue tracker"

Create a GitHub issue.

## When a skill says "fetch the relevant ticket"

Read it with `gh issue view <number>`.
