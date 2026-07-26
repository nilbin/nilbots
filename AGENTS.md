# Shared agent instructions

This repository is developed with both Claude Code and Codex.

1. Read and follow `CLAUDE.md` completely before making repository changes.
   It is the canonical shared architecture, invariant, command, and safety
   guide despite its historical filename.
2. **Working inside `web/` or `mobile/` means reading that directory's own
   `CLAUDE.md` first**, in addition to the root one. They carry the rules that
   are specific to each front end — build outputs, folder boundaries enforced by
   tests, data-access conventions, platform footguns — and the root guide wins
   only where the two overlap.

   This step exists because the two agents discover them differently: Claude
   Code loads a directory's `CLAUDE.md` automatically when it opens a file
   there, and Codex does not. Anything that lands in a scoped guide is therefore
   invisible to Codex unless it opens it deliberately.
3. Treat `.claude/skills/*/SKILL.md` as shared repository workflow skills.
   When a task matches a skill description, read that skill completely and
   follow it whether the active agent is Claude Code or Codex. Skills all live
   at the repository root today; a subdirectory could gain its own, so match on
   description rather than assuming that one directory.
4. Keep shared project guidance in `CLAUDE.md`, the scoped `web/CLAUDE.md` or
   `mobile/CLAUDE.md`, or the matching repository skill—not in tool-specific
   private instructions—so both agents receive the same rules.
5. System, developer, and explicit user instructions still take precedence
   over repository guidance.
