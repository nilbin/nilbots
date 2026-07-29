# Codex bot-class configuration handoff

Branch: `codex/bot-class-config`

Implementation commit:
`f355e41` (`Persist bot class identity through web and CLI`)

This is the web/backend preparation requested after the original two-branch
handover. It is separate from the match-contract/profile-3 work on
`codex/class-first-class`.

## Scope

- nullable persisted `Bot.ClassId` and EF migration for legacy compatibility;
- Engine-owned class catalog validation;
- class identity on create/read/list/mine/meta API contracts;
- owner-only, immutable, atomic, idempotent first assignment for legacy bots;
- regenerated web/mobile schema mirrors and CLI API contracts;
- Garage creation and legacy assignment UI on the web;
- CLI register/submit propagation and explicit-declaration mismatch checks.

The mobile product UI is intentionally unchanged. Its generated schema contains
the additive API field only because contract generation refreshes both mirrors.

A manifest that omits `class` remains deliberately class-agnostic; the
persisted bot identity is authoritative. An explicitly declared manifest class
must match it.

## Verification

- live PostgreSQL class tests: 15/15;
- PostgreSQL schema/migration tests: 3/3;
- CLI tests: 54/54;
- web tests: 280/280;
- DocDrift: 13/13;
- web and mobile TypeScript checks;
- API contract regeneration and EF pending-model check;
- `git diff --check`.

The wider App suite reached 253 passes and three expected skips. One unrelated
anonymous-auth test is blocked locally by a rejected cached development
OpenIddict certificate; it reproduces without the class changes.

