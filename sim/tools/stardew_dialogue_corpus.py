"""Build an auditable review corpus from export-stardew-dialogue.ps1's local assets.

This is a conservative source index, not a game-condition evaluator or biography generator.
Every raw entry survives. Unsupported syntax and uncertain speakers remain labelled.
"""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re


PORTRAIT = re.compile(r"\$(h|s|u|l|a|neutral|\d+)(?![\w])")
LOCALIZED = re.compile(r"\[LocalizedText\s+([^:\s]+):([^\]\s]+)(?:\s[^\]]*)?\]")
DIRECT_REFERENCE = re.compile(r"^(Strings[\\/][^:]+):(.+)$")


def split_quoted(value: str, separator: str) -> list[str]:
    """Event '/' separators ignore double-quoted dialogue, preserving escapes verbatim."""
    result, start, quoted, escaped = [], 0, False, False
    for i, char in enumerate(value):
        if char == '"' and not escaped:
            quoted = not quoted
        if char == separator and not quoted:
            result.append(value[start:i])
            start = i + 1
        escaped = char == "\\" and not escaped
    result.append(value[start:])
    return result


def parse_dialogue(raw: str) -> dict:
    """Keep both gender/random/world-state variants and distinguish player responses.

    Branch commands are retained as conditions, never evaluated. Dynamic substitutions
    remain in text; the parser does not choose a player, romance route, or world history.
    """
    nodes, warnings, conditions = [], [], []
    answer = None

    def add_text(text, role="npc", extra=None):
        for random_index, random_variant in enumerate(text.split("||")):
            for branch_index, branch in enumerate(random_variant.split("|")):
                for gender_index, gender in enumerate(branch.split("^")):
                    portraits = PORTRAIT.findall(gender)
                    clean = PORTRAIT.sub("", gender).replace("$b", "").replace("$e", "").strip()
                    if not clean:
                        continue
                    nodes.append({"kind": "text", "role": role, "raw": gender, "text": clean,
                        "portraits": portraits, "conditions": list(conditions),
                        "variant": {"random": random_index, "branch": branch_index, "gender": gender_index},
                        **(extra or {})})

    for index, segment in enumerate(raw.split("#")):
        value = segment.strip()
        if answer is not None:
            add_text(segment, "player", {"answer": answer})
            answer = None
            continue
        if not value:
            continue
        if value in ("$b", "$e"):
            nodes.append({"kind": "page-break", "raw": segment, "segment": index})
            continue
        match = re.match(r"^\$r\s+(\S+)\s+(-?\d+)\s+(\S+)\s*$", value)
        if match:
            answer = {"id": match[1], "friendship": int(match[2]), "replyKey": match[3]}
            nodes.append({"kind": "player-answer-command", "raw": segment, **answer})
            continue
        if value.startswith("$y "):
            match = re.fullmatch(r"\$y\s+'(.*)'", value, re.DOTALL)
            fields = match[1].split("_") if match else []
            if len(fields) >= 3 and len(fields) % 2 == 1:
                add_text(fields[0], extra={"inlineQuestion": True})
                for i in range(1, len(fields), 2):
                    add_text(fields[i], "player", {"inlineAnswer": (i - 1) // 2})
                    add_text(fields[i + 1], extra={"replyToInlineAnswer": (i - 1) // 2})
            else:
                nodes.append({"kind": "unparsed-command", "raw": segment})
                warnings.append("Inline question syntax retained without guessing its answers.")
            continue
        if re.match(r"^\$(?:q|p|d|c|k|t|v|action|query|r)\b", value) or re.match(r"^\$\d+\s+\S", value):
            conditions.append(segment)
            nodes.append({"kind": "conditional-command", "raw": segment})
            continue
        if re.match(r"^\$[A-Za-z]+\b", value) and not re.match(r"^\$(?:h|s|u|l|a|neutral|b|e)\b", value):
            nodes.append({"kind": "unparsed-command", "raw": segment})
            warnings.append("Unknown leading dialogue command retained.")
            continue
        add_text(segment)
    if answer is not None:
        warnings.append("Player answer command has no following segment.")
    return {"nodes": nodes, "warnings": sorted(set(warnings))}


def references(raw: str) -> list[dict]:
    matches = [(m[1], m[2]) for m in LOCALIZED.finditer(raw)]
    direct = DIRECT_REFERENCE.fullmatch(raw.strip())
    if direct:
        matches.append((direct[1], direct[2]))
    return [{"asset": a.replace("\\", "/"), "key": k} for a, k in dict.fromkeys(matches)]


def key_candidates(key: str, names: list[str]) -> list[str]:
    return [name for name in names if re.search(r"(?<![A-Za-z])" + re.escape(name) + r"(?![A-Za-z])", key)]


def build_corpus(manifest: dict) -> tuple[dict, dict, dict]:
    if manifest.get("schemaVersion") != 1 or manifest.get("language") != "en":
        raise ValueError("Expected schemaVersion 1 English raw asset export.")
    assets = {asset["asset"]: asset for asset in manifest["assets"]}
    metadata = assets.get("Data/Characters", {}).get("entries", {})
    names = sorted(set(manifest["characterNames"]) | {"LeoMainland"})
    # NPC.tryToGetMarriageSpecificDialogue can fall back to the shared table for
    # roommates too; include every identity with its own MarriageDialogue asset.
    spouses = sorted({name for name, value in metadata.items() if value.get("CanBeRomanced")} |
        {asset.rsplit("/", 1)[-1].removeprefix("MarriageDialogue") for asset in assets
         if asset.startswith("Characters/Dialogue/MarriageDialogue") and asset != "Characters/Dialogue/MarriageDialogue"})
    records, event_commands = [], Counter()

    def add(asset, key, raw, category, speaker=None, attribution="unresolved", candidates=None,
            suffix="", conditions=None, parse=True, parent=None):
        source = assets[asset]
        record = {"id": f"{asset}:{key}{suffix}", "source": {"asset": asset, "key": key,
            "sha256": source["sha256"], "gameVersion": manifest["gameVersion"]},
            "category": category, "speaker": speaker, "attribution": attribution,
            "speakerCandidates": candidates or [], "conditions": conditions or {"rawKey": key},
            "raw": raw, "references": references(raw), "parentId": parent,
            "parsed": parse_dialogue(raw) if parse else {"nodes": [], "warnings": []}}
        records.append(record)
        return record

    def event_lines(asset, key, script, category, conditions=None, suffix=""):
        root = add(asset, key, script, category + "-script", parse=False, suffix=suffix,
                   conditions=conditions)
        for i, command in enumerate(split_quoted(script, "/")):
            command = command.strip()
            if not command:
                continue
            verb = command.split()[0]
            event_commands[verb] += 1
            match = re.fullmatch(r'(speak|textAboveHead)\s+(\S+)\s+"((?:\\.|[^"\\])*)"(?:\s+.*)?', command, re.DOTALL)
            if match:
                who = match[2]
                add(asset, key, match[3], category + "-speech", who if who in names else None,
                    "event-speaker" if who in names else "unresolved-event-speaker", [who],
                    suffix=f"{suffix}/command/{i}", conditions=conditions, parent=root["id"])
            elif verb in ("speak", "textAboveHead", "message", "question", "quickQuestion", "fork"):
                row = add(asset, key, command, category + "-control", parse=False,
                          suffix=f"{suffix}/command/{i}", conditions=conditions, parent=root["id"])
                row["parsed"]["warnings"].append("Event control/narration/branch retained; not attributed as NPC speech.")

    for asset, source in sorted(assets.items()):
        entries = source["entries"]
        if source["format"] == "character-metadata":
            continue
        if source["format"] == "phone-call-metadata":
            for key, call in entries.items():
                who = call.get("FromNpc")
                add(asset, key, call.get("Dialogue") or "", "phone", who,
                    "phone-from-npc" if who in names else "unresolved",
                    conditions={"TriggerCondition": call.get("TriggerCondition"), "RingCondition": call.get("RingCondition"), "MaxCalls": call.get("MaxCalls")})
            continue
        if source["format"] == "movie-reaction-metadata":
            for person in entries:
                for reaction in person.get("Reactions", []):
                    for phase, response in (reaction.get("SpecialResponses") or {}).items():
                        if not response:
                            continue
                        key = person["Id"] + "/" + reaction["Id"] + "/" + phase
                        conditions = {"movieTag": reaction.get("Tag"), "requiredPeople": reaction.get("Whitelist"),
                                      "responsePoint": response.get("ResponsePoint")}
                        if response.get("Text"):
                            add(asset, key, response["Text"], "movie", person.get("NPCName"), "movie-npc-name", conditions=conditions)
                        if response.get("Script"):
                            event_lines(asset, key, response["Script"], "movie", conditions, suffix="/script")
            continue
        if source["format"] != "string-dictionary":
            continue
        for key, raw in sorted(entries.items()):
            if not isinstance(raw, str):
                raise ValueError(f"Non-string entry in string dictionary {asset}:{key}")
            base = asset.rsplit("/", 1)[-1]
            conditions = {"rawKey": key}
            if asset.startswith("Data/Events/"):
                conditions["eventPreconditions"] = key.split("/")[1:]
                event_lines(asset, key, raw, "event", conditions)
            elif asset.startswith("Data/Festivals/"):
                conditions["festival"] = base
                conditions["festivalConditions"] = entries.get("conditions")
                if re.search(r'(?:^|/)(?:speak|textAboveHead)\s', raw):
                    event_lines(asset, key, raw, "festival", conditions)
                else:
                    candidate = key_candidates(key, names)
                    who = candidate[0] if len(candidate) == 1 and re.match(re.escape(candidate[0]) + r"(?:_|$)", key) else None
                    add(asset, key, raw, "festival", who, "festival-key" if who else "unresolved", candidate, conditions=conditions)
            elif asset.startswith("Characters/Dialogue/"):
                if base == "MarriageDialogue":
                    candidate = key_candidates(key, names)
                    add(asset, key, raw, "marriage", candidate[0] if len(candidate) == 1 else None,
                        "shared-marriage-key" if len(candidate) == 1 else "shared-spouse",
                        candidate or spouses, conditions={"rawKey": key, "requires": "applicable spouse/marriage state"})
                elif base.startswith("MarriageDialogue"):
                    who = base.removeprefix("MarriageDialogue")
                    add(asset, key, raw, "marriage", who if who in names else None, "dialogue-asset",
                        conditions={"rawKey": key, "requires": "applicable spouse/roommate state"})
                elif base == "rainy":
                    add(asset, key, raw, "rainy", key if key in names else None, "rainy-key", conditions={"rawKey": key, "weather": "rain"})
                else:
                    add(asset, key, raw, "dialogue", base if base in names else None, "dialogue-asset", conditions=conditions)
            elif asset.startswith("Strings/schedules/"):
                add(asset, key, raw, "schedule-dialogue", base if base in names else None, "schedule-string-asset")
            elif asset.startswith("Characters/schedules/"):
                add(asset, key, raw, "schedule-script", base if base in names else None, "schedule-asset", parse=False)
            elif asset == "Data/NPCGiftTastes":
                add(asset, key, raw, "gift-taste-source", parse=False)
                if key in names:
                    fields = raw.split("/")
                    for i, taste in enumerate(("love", "like", "dislike", "hate", "neutral")):
                        if 2 * i < len(fields) and fields[2 * i]:
                            add(asset, key, fields[2 * i], "gift-reaction", key, "gift-npc-key",
                                suffix=f"/reaction/{taste}", conditions={"giftTaste": taste})
            elif asset == "Data/EngagementDialogue":
                candidate = [name for name in names if re.fullmatch(re.escape(name) + r"\d+", key)]
                add(asset, key, raw, "engagement", candidate[0] if len(candidate) == 1 else None,
                    "engagement-key", conditions={"rawKey": key, "requires": "engagement"})
            else:
                candidate = key_candidates(key, names)
                add(asset, key, raw, "shared-string" if asset.startswith("Strings/") else "other-data",
                    candidates=candidate, attribution="key-hint" if candidate else "unresolved",
                    parse=asset.startswith("Strings/") or asset == "Data/ExtraDialogue")

    # Resolve a referenced string with the caller's established speaker, without globally
    # assigning generic string-table entries to every character whose name appears in a key.
    for row in list(records):
        if not row["speaker"]:
            continue
        for i, ref in enumerate(row["references"]):
            source = assets.get(ref["asset"])
            value = source.get("entries", {}).get(ref["key"]) if source and isinstance(source.get("entries"), dict) else None
            if isinstance(value, str):
                resolved = add(ref["asset"], ref["key"], value, "resolved-reference", row["speaker"],
                    "speaker-inherited-from-reference", suffix="/via/" + hashlib.sha256(row["id"].encode()).hexdigest()[:16] + f"/{i}",
                    conditions=row["conditions"], parent=row["id"])
                resolved["referenceCaller"] = row["source"]
            else:
                row["parsed"]["warnings"].append(f"Unresolved localized reference: {ref['asset']}:{ref['key']}")

    records.sort(key=lambda row: row["id"])
    by_character = {name: {"name": name, "gameVersion": manifest["gameVersion"],
        "metadata": metadata.get(name), "records": [row for row in records if row["speaker"] == name or name in row["speakerCandidates"]]}
        for name in names}
    by_id = {row["id"]: row for row in records}
    for character in by_character.values():
        parents = {row["parentId"] for row in character["records"] if row["parentId"]}
        character["sourceContexts"] = {key: by_id[key] for key in sorted(parents) if key in by_id}
    coverage = {"schemaVersion": 1, "gameVersion": manifest["gameVersion"], "language": "en",
        "loadedAssets": len(assets), "rawStringEntries": sum(len(x["entries"]) for x in assets.values() if x["format"] == "string-dictionary"),
        "records": len(records), "byCategory": dict(sorted(Counter(x["category"] for x in records).items())),
        "byAttribution": dict(sorted(Counter(x["attribution"] for x in records).items())),
        "recordsWithWarnings": sum(bool(x["parsed"]["warnings"]) for x in records),
        "eventCommandCounts": dict(sorted(event_commands.items())), "unsupportedAssets": manifest.get("unsupported", []),
        "perCharacter": {name: {"records": len(value["records"]), "attributed": sum(x["speaker"] == name for x in value["records"]),
            "candidateOrShared": sum(x["speaker"] != name for x in value["records"])} for name, value in by_character.items()},
        "limitations": ["Not a claim to contain every line reachable in the game.",
            "Base English content only; no Content Patcher, mod dialogue, save state, or localization variants applied.",
            "Shared Strings/ExtraDialogue keys are hints, not verified speakers; code/map callers still require attribution.",
            "Event fork/question/control syntax and commands other than literal speak/textAboveHead remain raw; branches are not executed.",
            "Dialogue conditions, alternate genders and possible player answers are preserved without choosing a canonical history.",
            "Maps and other structured Data assets (including SpecialOrders) have not been exhaustively traversed.",
            "Counts are source entries/derived records, not unique spoken lines; referenced text can occur in multiple contexts.",
            "No source has been converted into a simulated personal memory or inferred biography."]}
    corpus = {"schemaVersion": 1, "gameVersion": manifest["gameVersion"], "gameAssemblySha256": manifest.get("gameAssemblySha256"),
              "rawManifest": "raw-assets.json", "records": records}
    return corpus, by_character, coverage


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    manifest = json.loads(args.input.read_text(encoding="utf-8-sig"))
    corpus, characters, coverage = build_corpus(manifest)
    args.out.mkdir(parents=True, exist_ok=True)
    (args.out / "characters").mkdir(exist_ok=True)
    def write(path, data):
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    write(args.out / "corpus.json", corpus)
    write(args.out / "coverage.json", coverage)
    write(args.out / "unattributed.json", [r for r in corpus["records"] if not r["speaker"]])
    for name, rows in characters.items():
        filename = re.sub(r'[^A-Za-z0-9 _-]', '_', name)
        write(args.out / "characters" / (filename + ".json"), rows)
    print(f"Indexed {coverage['records']} records for {len(characters)} character identities; review coverage.json before treating entries as voice evidence.")


if __name__ == "__main__":
    main()
