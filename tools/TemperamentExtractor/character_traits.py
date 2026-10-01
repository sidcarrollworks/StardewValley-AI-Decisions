"""Decode the personality fields from the game's Content/Data/Characters.xnb.

xnbcli can't unpack Data/Characters (a typed ReflectiveReader asset), but it can decompress it.
Get the decompressed bytes with xnbcli plus a one-line dump (see docs/spec/temperament.md,
"Reproducing the table"), then:

    python tools/TemperamentExtractor/character_traits.py Characters.bin fixtures/game/temperament/characters.json

Field order follows StardewValley.GameData.Characters.CharacterData in the 1.6.15 decompile:
DisplayName, BirthSeason (Season?), BirthDay, HomeRegion, Language, Gender, Age, Manner,
SocialAnxiety, Optimism. Value types are written raw, strings as a 7-bit reader index then a
7-bit length and UTF-8 bytes. Enum orders are from the same decompile. If a game update adds a
field before Optimism, this breaks loudly (the display-name check fails) rather than silently.
"""
import json
import re
import struct
import sys

AGE = ["Adult", "Teen", "Child"]
MANNER = ["Neutral", "Polite", "Rude"]
ANXIETY = ["Outgoing", "Shy", "Neutral"]
OPTIMISM = ["Positive", "Negative", "Neutral"]


def main(src, dst):
    d = open(src, "rb").read()

    def v7(p):
        r = s = 0
        while True:
            b = d[p]
            p += 1
            r |= (b & 0x7F) << s
            s += 7
            if b < 0x80:
                return r, p

    def rstr(p):
        idx, p = v7(p)
        if idx == 0:
            return None, p
        n, p = v7(p)
        return d[p:p + n].decode("utf8"), p + n

    chars = {}
    for m in re.finditer(rb"\[LocalizedText Strings.NPCNames:([A-Za-z]+)\]", d):
        name = m.group(1).decode()
        # the string is preceded by its reader index and a one-byte length
        display, p = rstr(m.start() - 2)
        if display != m.group().decode():
            sys.exit(f"layout changed near {name}; update this script")
        has_season = d[p]
        p += 1 + (4 if has_season else 0)
        birthday = struct.unpack_from("<i", d, p)[0]
        p += 4
        _home, p = rstr(p)
        _lang, _gender, age, manner, anxiety, optimism = struct.unpack_from("<6i", d, p)
        if birthday > 0:  # villagers only; placeholders like Bear or Gunther have no birthday
            chars[name] = {"Manner": MANNER[manner], "SocialAnxiety": ANXIETY[anxiety],
                           "Optimism": OPTIMISM[optimism], "Age": AGE[age]}

    out = {"note": "Personality fields from the game's Data/Characters (1.6.15), decoded from "
                   "Content/Data/Characters.xnb with tools/TemperamentExtractor/character_traits.py. "
                   "Characters with a birthday only (the villagers).",
           "characters": dict(sorted(chars.items()))}
    with open(dst, "w", newline="\n") as f:
        f.write(json.dumps(out, indent=2) + "\n")
    print(f"wrote {len(chars)} characters to {dst}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
