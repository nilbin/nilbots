# Shared agent instructions

This repository is developed with both Claude Code and Codex.

1. Read and follow `CLAUDE.md` completely before making repository changes.
   It is the canonical shared architecture, invariant, command, and safety
   guide despite its historical filename.
2. Treat `.claude/skills/*/SKILL.md` as shared repository workflow skills.
   When a task matches a skill description, read that skill completely and
   follow it whether the active agent is Claude Code or Codex.
3. Keep shared project guidance in `CLAUDE.md` or the matching repository
   skill—not in tool-specific private instructions—so both agents receive the
   same rules.
4. System, developer, and explicit user instructions still take precedence
   over repository guidance.
