Migration status - Bootstrap utilities -> Semantic tokens

Summary
- Completed: Toast utilities migration on branch feature/tokens-toast-migration (commit b3a488e). Added semantic helpers (.toast-success, .toast-error) to Karamel.Web/wwwroot/css/tokens.css and updated components and tests.
- Tests & build: `dotnet build` succeeded; `dotnet test` passed (0 failures, 3 skipped expected).

What remains
- Repo-wide sweep to replace remaining Bootstrap color utilities (bg-*, text-*, border-*, text-muted). Most occurrences are vendor (wwwroot/lib/bootstrap) and must NOT be edited.
- Migrate component files in small batches (3–5 files per batch), update tests, run build/tests, commit on feature branches.

Exact next operations for next agent
1) Create branch: feature/tokens-sweep-2
2) Run a filtered search excluding vendor paths to find non-vendor files using Bootstrap color utilities. Scope: Karamel.Web/Pages, Karamel.Web/Components, Karamel.Web/Layout, Karamel.Web/wwwroot/css, Karamel.Web/wwwroot/js. Example search patterns: "bg-", "text-", "border-", "text-muted". Exclude: wwwroot/lib/bootstrap/**
3) Pick 3–5 files (prioritize Pages and Components) with usages and record them.
4) For each chosen file: replace utility classes with semantic helpers; if needed, add mapping helpers to Karamel.Web/wwwroot/css/tokens.css.
5) Run `dotnet build` and `dotnet test`. Fix failures. Update tests that assert on exact class names.
6) Commit changes to a new feature branch and push (do NOT merge to main). Create small PR per batch.
7) Perform light/dark visual QA and adjust tokens.css if colors need tuning.

Notes
- Search results currently show the majority of matches inside wwwroot/lib/bootstrap (vendor). Do NOT edit vendor files; add semantic helpers in wwwroot/css/tokens.css instead.
- If search tools ignore some code paths, re-run searches with includeIgnoredFiles=true or explicitly list folders to scan.

Contacts
- Repo: Karamel-Web (local)
- Dev commands: `dotnet build`, `dotnet test`, `dotnet run --project Karamel.Web`
