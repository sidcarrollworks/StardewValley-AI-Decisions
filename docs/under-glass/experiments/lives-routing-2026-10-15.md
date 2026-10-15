# Lives routing: first connected history comparison

Measured 2026-10-15. This is a wiring experiment for the
[routing increment](../specs/lives-routing-spec.md), not an interestingness score or a completed
Penny, Pam and Gus pilot. No model was called and no Stardew history was approved by this trial.

## Reproduce and inspect

```powershell
dotnet run -c Release --project sim/UnderGlass.LivesTrial -- --out out/lives-routing-review-2026-10-15 --days 7 --seed 7
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --lives-routing --out out/lives-routing-review-2026-10-15/town-learned.json
```

Choose a fresh trial directory; the harness refuses to overwrite existing output. Open its
`index.html` directly or through the preview server. It starts at the first changed choice,
shows recorded probabilities and destinations, and keeps full receipts behind a disclosure.
The separate whole recordings can be opened in Run Viewer when served from the repo's preview.
The existing Life chains page does not yet consume `livesRouting` records.

## Controlled history intervention

The deliberately small synthetic town has Ann and Bo, two destinations, no acts and no models.
Ann has an account of spending time at the Hall and a reviewed warm reading: “I enjoy being at
the Hall.” Its source is explicitly fictional; the harness uses the canon-stated admission path
to test that mechanism, not to make a claim about Stardew. Bo receives none of Ann's history.

Three actual recordings are produced:

- `with-history.json`: routing enabled, with the account and warm reading. Each Ann choice also
  records an isolated intervention removing that reading and its seed.
- `without-reading.json`: a separate run with the account retained and the warm reading absent.
- `routing-off.json`: the existing routine draw, with Lives routing disabled.

Only the embedded comparisons hold the choice fixed. They share the candidate identities/order,
scene, state, retained lived experience, settings and random unit. Removing history does not
rerun the person's past or predict a different future. Whole runs may diverge after a different
choice. The disabled control also uses the legacy draw's key, so differences against that control
must not be attributed solely to the history's numerical weight.

| Recorded measure | Result |
|---|---:|
| All destination decisions | 164 |
| Lives decisions / existing routine decisions | 84 / 80 |
| Comparable isolated interventions | 84 |
| Changed reasons and probabilities | 84 |
| Different sampled destination | 7 |
| Model calls | 0 |

At minute 1515 (Day 2, 01:15 in the review), Ann chooses the Hall. Its probability is
56.217650% with the warm reading and 50% without it. The Yard is first in the same ordered menu;
the shared unit `0.44852964951690455` therefore selects the Hall with history and the Yard without.
The simulation's actual selected destination is the Hall; the movement integration tests check
that the selected destination is reached in this unobstructed toy town. Seven changes is a
consequence of this seed and setting, not a target to tune toward.

## Existing town, no starting history

The second command runs the normal 26-person town for seven days at seed 7. It loads no history
pack. People can build associations through the existing feeling/perception paths:

| Recorded measure | Result |
|---|---:|
| Destination decisions recorded | 1,269 |
| Decisions using recalled memory | 589 |
| Decisions retaining the existing routine draw | 680 |
| Oversized menus left on existing routing | 25 |
| First acquisitions / corrections | 236 / 44 |

For example, George stumbles at home at minute 580. His next destination decision at minute 600
includes that lived feeling toward home. Square has 38.103743% and home 61.896257%, compared with
routine base weights of 0.5 and 1. He selects Square. This shows a perceived event reaching a later
route; it does not establish that every stumble should have this effect or that his resulting
week is more interesting. Judging that needs the meaningful-action pilot.

The CLI reported 1.1 seconds to record this week on Sid's machine, with other checks running.
This is one local observation, not a controlled performance benchmark. No model latency was
involved. The recording is 6,543,863 bytes. The synthetic with-history recording is 2,217,761 bytes
in readable JSON, including verbose before/after comparisons. These are not measurements of the
proposed compact full-Lives format; recording size and population scaling need separate work.

## Verification and limitations

Integration tests cover actual movement, private knowledge, same-choice interventions, default
compatibility, observer independence, hearsay without hidden place information, corrected
attribution, day-boundary decay and zero-weight/oversized menus. Review found and fixed two issues:
corroboration could restore faded feeling, and an asynchronously mutated caller list could change
the settings written into the recording. A provenance-only correction now preserves the retained
amount; a completely forgotten feeling is not revived. Original acquisition dilution remains.

The live adapter writes associations, not expectation outcomes. It chooses among existing
destinations, without seeking a person, arranging an evening, generating an idea or asking Laya
to settle competing options. No names are special-cased. The next work is reviewed pilot history,
opportunities with meaningful activities and responses, and their perceived outcomes, then the
full observer export and model comparison.

Recorded SHA-256 values:

| File | SHA-256 |
|---|---|
| `history.json` | `2ad88588484f804f5462d699ede5bb0305dfe567bcbf101a25c5b2f531a6b52e` |
| `with-history.json` | `81501800dadd9f52a65a4bfa1df160c89b695d57ccb9e9ec3954f82696c12b8d` |
| `without-reading.json` | `30f624c0ed51a4fd74fd64673bc4a197705a2bcb26d28e7d81039892e69b2134` |
| `routing-off.json` | `f939759c538016f18a6d712e37444685c338a173dafe9a7a551cb4cd6ee430ea` |
| `town-learned.json` | `0034d5ddaf16a9ba6d0c38ce463c64f169bd778a351fb12cbca97973a326bc58` |
