# References

Links to keep, and which spec they matter for. **Read** means someone on the project read the page
and the spec already uses what it says (date in brackets); the others are listed for the feature
that will need them. Recalled facts about these pages are still marked "verify" in the specs; facts
read from them are marked **wiki**.

## SMAPI API reference (stardewvalleywiki.com/Modding:Modder_Guide/APIs, index read 2026-09-30)

| Page | For | Status |
|---|---|---|
| [Get Started](https://stardewvalleywiki.com/Modding:Modder_Guide/Get_Started) | project setup, decompiling, conventions: [README.md](README.md) | read (2026-09-30) |
| [Manifest](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Manifest) | release packaging (`MinimumApiVersion`, dependencies): [roadmap.md](roadmap.md), "Later" | - |
| [Update checks](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Update_checks) | release packaging: `UpdateKeys` in `manifest.json`, e.g. `"GitHub:sidcarrollworks/<repo>"` (needs a release tagged with a semantic version) or `"Nexus:<id>"`; several keys allowed | read (2026-09-30) |
| [Mod structure](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Mod_structure) | release packaging (what goes in the mod folder: `i18n/`, `data/`) | - |
| [Events](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Events) | every hook: `GameLoop`, `Display.MenuChanged`, `Content.AssetRequested`/`LocaleChanged`, `Multiplayer` ([diary.md](diary.md), [intents.md](intents.md), [ladder.md](ladder.md), [invitations.md](invitations.md)) | - |
| [Configuration](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Config) | [config.md](config.md) (does SMAPI add new keys to an existing `config.json`?) | - |
| [Content](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Content) | editing `Data/mail` and `Data/Quests`, loading schedules: [invitations.md](invitations.md), [ladder.md](ladder.md), [newcomer-week.md](newcomer-week.md), [routines.md](routines.md) | - |
| [Data](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Data) | save data: [persistence.md](persistence.md) | - (behavior checked in the SMAPI decompile) |
| [Input](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Input) | nothing yet; a debug hotkey at most | - |
| [Logging](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Logging) | `[shadow]`/`[live]` lines, log levels: [rollout.md](rollout.md) | - |
| [Reflection](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Reflection) | avoid; only if a needed field is private. Prefer the decompile to find a public path first | - |
| [Multiplayer](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Multiplayer) | [multiplayer-compat.md](multiplayer-compat.md) | read (2026-09-30) |
| [Translation](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Translation) | `i18n/` lines: [text.md](text.md) | read (2026-09-30) |
| [Utilities](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Utilities) | `SDate` date math and `PerScreen<T>`: [multiplayer-compat.md](multiplayer-compat.md); maybe `GameClock` checks | - |
| [Content packs](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Content_Packs) | pairing with other mods ([multiplayer-compat.md](multiplayer-compat.md), "Later") | read (2026-09-30): see note below |
| [Console commands](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Console) | `npcmod_newcomer` ([newcomer-week.md](newcomer-week.md)), `npcmod_live off` ([rollout.md](rollout.md)) | - |
| [Mod integrations](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Integrations) | Content Patcher tokens, Generic Mod Config Menu ([multiplayer-compat.md](multiplayer-compat.md), [config.md](config.md)) | read (2026-09-30): `helper.ModRegistry.IsLoaded(id)`, `GetApi<TInterface>(id)` (null-check; not before `GameLaunched`) |
| [Harmony](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Harmony) | gift and quest hooks: [diary.md](diary.md), "Harmony" | read (2026-09-30) |

**Content packs note.** The wiki recommends that instead of a custom content-pack format, a mod
offer "a custom game asset" that other mods, Content Patcher packs included, can edit through the
content pipeline, which gives them tokens and conditions for free. That fits this mod well: expose
the voice notes, the region table and the newcomer gift table as assets (for example
`Mods/squid.StardewNpcMod/Voices`) so a pack for Stardew Valley Expanded can add its characters
without code. Lines stay in `i18n/` ([text.md](text.md)). Decide when pairing work starts.

## Other modding wiki pages

| Page | For |
|---|---|
| [Modding:Dialogue](https://stardewvalleywiki.com/Modding:Dialogue) | dialogue commands the sanitizer must strip: [text.md](text.md) |
| [Modding:Mail data](https://stardewvalleywiki.com/Modding:Mail_data) | letter format: [invitations.md](invitations.md) (the parser was also checked in the decompile) |
| [Modding:NPC data](https://stardewvalleywiki.com/Modding:NPC_data) | `Data/Characters` fields: [text.md](text.md), [ladder.md](ladder.md), [routines.md](routines.md) |
| [Modding:Schedule data](https://stardewvalleywiki.com/Modding:Schedule_data) | schedules: [routines.md](routines.md), [invitations.md](invitations.md), [find.md](find.md) |
| [Modding:Shops](https://stardewvalleywiki.com/Modding:Shops) | shop owners: [find.md](find.md) |
| [Modding:Trigger actions](https://stardewvalleywiki.com/Modding:Trigger_actions) | what a stray `%action` in a letter could do (why the mail sanitizer strips `%`): [text.md](text.md) |
| [Modding:Quest data](https://stardewvalleywiki.com/Modding:Quest_data) | requests as quests: [invitations.md](invitations.md), "Later" |

The pages in this second table weren't read for the spec; the URLs follow the wiki's naming and
should be checked when first used.

## Other sources

- Local decompile of 1.6.15 and SMAPI 4.5.2: [README.md](README.md), "Verifying game facts";
  findings in `stardew-source-notes.md`.
- Laya: github.com/NandhaKishorM/laya and huggingface.co/convaiinnovations/laya (the only trusted
  sources, `AGENTS.md` rule 8): [laya.md](laya.md), `sidecar/README.md`.
- Content Patcher (for exposing tokens, later): its author's docs on GitHub
  (github.com/Pathoschild/StardewMods, Content Patcher folder): [multiplayer-compat.md](multiplayer-compat.md).
