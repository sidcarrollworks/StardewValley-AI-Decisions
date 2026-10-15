# Lives observer recording contracts

Versioned contracts between the Lives simulation work (Claude) and Codex's read-only chain viewer. Nothing in them is
implemented: no Lives code exists yet. Fixtures are generated and labelled illustrative; their probabilities say
nothing about runtime model quality. A reader accepts exact 0.x versions only.

## Versions

Implementation follow-up, 2026-10-15: PRs #96–98 are merged. The
[pilot plan](../specs/lives-pilot-spec.md) records the remaining runtime and viewer work,
recommended implementation choices, and the individual-reading admission gate. Contract 0.2
remains proposed; this follow-up does not edit either version or claim a runtime writer exists.

| Version | Status | Files |
|---|---|---|
| **0.1** | **Frozen** (2026-10-14). Never edited. | `lives-observer-recording-v0.1.md`, `lives-observer-example-v0.1.json` (sha256 `57bb7248...cc4b4`), `check-lives-observer.cjs`, `lives-observer-v0.1-tools/` (`build-example.cjs`, `mutate-example.cjs`, `rng.cjs`) |
| **0.2** | **Proposed** (2026-10-14), for Sid's fifth direction and his bedtime decision. Breaking changes await Sid's and Codex's agreement. Its fixture runs over Wednesday and Thursday, summer 3-4 (days 30-31): the agreed evening is a summer Thursday, since in a dry year Penny's sourced routine has her at home and awake through the evening on summer Mondays, Thursdays and Sundays (and winter 4). | `lives-observer-recording-v0.2.md` (the contract), `lives-observer-example-v0.2.json` (fixture), `check-lives-observer-structure.cjs`, `check-lives-observer-rules.cjs`, `lives-observer-changes-v0.2.md` (change list and implementation sequence), `lives-observer-v0.2-tools/` |

**What Codex built against 0.1.** The chain viewer, PR #95 on `codex/lives-chain-viewer`, read for 0.2 at aef9d7f
(2026-10-14 18:26, the branch head, a clean worktree): `sim/viewer/chains-model.js` (the adapter: exact version
`"0.1"`, `lives` never rebuilt, a missing gate never arrives, an allowlisted projection per lens), `chains.js` and
`chains.html` (the UI), `sim/tools/test-chains.cjs` (38 tests, one asserting that `"0.2"` is refused), and
`docs/under-glass/specs/lives-chain-viewer-spec.md`. It reads a byte-identical copy of the 0.1 fixture (the same
SHA-256). Its dashboard and speech tools refuse an illustrative recording that carries `lives` (`sim/viewer/tools.js:49-50`).
0.2 is a separate file set: Codex's 0.1 adapter keeps working on 0.1 until it is replaced by agreement. The change list
gives, for each change, the adapter line it touches at aef9d7f and what a 0.2 adapter must do; the branch may move on.

## Running the checks (node 20, no dependencies)

From this folder:

```bash
node check-lives-observer-structure.cjs lives-observer-example-v0.2.json   # structure: must also pass on real output
node check-lives-observer-rules.cjs lives-observer-example-v0.2.json       # experimental pilot rules; readers never require it
node lives-observer-v0.2-tools/build-example.cjs lives-observer-example-v0.2.json   # regenerate the fixture (deterministic)
node lives-observer-v0.2-tools/build-example.cjs --find-seed               # the smallest seed whose keyed draws give the story
node lives-observer-v0.2-tools/mutate-structure.cjs    # planted faults, each caught by the structure checker; the real-like variant and every legitimate run shape pass
node lives-observer-v0.2-tools/mutate-rules.cjs        # planted rule faults, each caught by the rules checker; the legitimate shapes pass rule 6
node lives-observer-v0.2-tools/make-real-like.cjs lives-observer-example-v0.2.json <out.json>   # the stripped variant
node lives-observer-v0.2-tools/reader-lens.cjs lives-observer-example-v0.2.json   # a reference character-lens reader (contract 4.3)
node lives-observer-v0.2-tools/canon-vectors.cjs           # canonical JSON and hash vectors for the C# writer (contract 9.6)
node lives-observer-v0.2-tools/rng.cjs <seed> <part>...                    # Rng.Unit, checked against Rng.cs
node check-lives-observer.cjs lives-observer-example-v0.1.json             # 0.1, unchanged
```

Each checker exits 0 on pass, 1 on failure, 2 on usage or parse errors, and prints which checks ran. The structure
checker passes a recording with no `lives` block after checking its base rows ("no Lives data").

**Legitimate run shapes.** `lives-observer-v0.2-tools/variants.cjs` builds, from the real-like variant, every kind of
run the pilot must be able to record: an authored control, Laya failing on a torn question and mid-way through a torn
reflection, Haiku + Laya as the mind runs today and with the proposed thought-only path, a hybrid whose generator never
answers, quiet reflections by a person
with no archetype, Canonical and Raw evaluation, a replay, Lives off, a chunk ending with work pending, and an actual
sleep onset recorded in the moves stream (sleep is recorded, never inferred from a habitual bedtime: contract 6.5). The
structure suite requires each to pass; the rules suite requires each to pass rule 6. All hashes use one canonical JSON
(RFC 8785, contract 9.6); `canon-vectors.cjs` prints its test vectors.

**What the checkers cannot see.** The structure checker holds character-safe content to a closed vocabulary of keys and
codes, but it cannot read meaning in text: a `characterLabel` that paraphrases a secret, or a tape's free text (memory,
context, lines) that mentions something its holder never perceived, passes it. Labels and tape text need review; the
pilot adds a writer test that builds request text only from the holder's own records (contract 4.1, 5.4, 15). The 0.2 checkers
refuse a 0.1 file at the version check (exit 1). The frozen 0.1 checker does not check the version first: on a 0.2 file
it throws a `TypeError` (`check-lives-observer.cjs:549`) before printing anything, exit 1; use the 0.2 checkers for 0.2.

**Migrating from 0.1.** The structure fault suite plants, in a 0.2 file, each 0.1 path that Codex's adapter reads (and
0.1's block metadata); the checker refuses every one, so a writer or a fixture ported carelessly from 0.1 cannot pass.

## Rules for changing these files

- Never edit a frozen version. A change is a new version with its own files and a change list.
- Change a fixture at its generator, regenerate, and run both checkers and both fault suites.
- Mark anything recalled rather than confirmed in source `VERIFY`; cite code as path:line.
