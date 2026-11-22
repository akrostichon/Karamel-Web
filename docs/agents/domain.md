# Domain Docs

How engineering skills should consume this repo's domain documentation.

## Before exploring, read these

- **`GLOSSARY.md`** at the repo root, or **`GLOSSARY-MAP.md`** if it exists. For a map, read the glossary for each relevant context.
- **`docs/adr/`** for relevant decisions, if present. This repo currently stores ADRs as `docs/ADR-*.md`; read those that touch the area being explored as well. If ADRs are later moved into `docs/adr/`, read the relevant files there.

If these files or directories do not exist, proceed silently. Don't flag their absence or suggest creating them upfront; domain-modeling skills create them when terms or decisions are resolved.

## File structure

This is a single-context repo. The intended layout is one root `GLOSSARY.md` and system-wide ADRs under `docs/adr/`. Existing ADRs are currently in `docs/ADR-*.md`.

## Use the glossary's vocabulary

When output names a domain concept (in an issue title, refactor proposal, hypothesis, or test name), use the term defined in `GLOSSARY.md`. Don't drift to synonyms the glossary explicitly avoids.

If a needed concept isn't in the glossary, reconsider whether the project uses that term or note a genuine gap for domain-modeling.

## Flag ADR conflicts

If output contradicts an existing ADR, surface it explicitly rather than silently overriding it:

> _Contradicts ADR-0007 (decision summary), but worth reopening because…_
