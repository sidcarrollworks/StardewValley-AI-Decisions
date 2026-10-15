// Test vectors for the canonical JSON and the hashes of contract v0.2, section 9.6, so a C# writer can be tested
// against the JS checker byte for byte. Canonical JSON is RFC 8785 (JCS): members sorted by their names' UTF-16 code
// units, arrays in order, no whitespace, numbers as ECMAScript Number::toString, strings with only ", \ and
// U+0000-U+001F escaped (\b \t \n \f \r, other controls \u00xx in lowercase hex). requestHash, alternativeSetHash and
// identityHash are "fnv1a64:" + FNV-1a 64 over the canonical text's UTF-16 code units with one 0x1F terminator
// (Rng.Hash with one part, sim/UnderGlass.Sim/Rng.cs:11-24); snapshotHash is SHA-256 over its UTF-8 bytes.
// The canonical function is the structure checker's own (check-lives-observer-structure.cjs), copied; the fault suite
// asserts that the checker accepts the vectors' hashes and refuses the C#-default ones.
// Usage: node canon-vectors.cjs [fixture.json]   (default: ../lives-observer-example-v0.2.json)
"use strict";
const fs = require("fs"), path = require("path"), crypto = require("crypto");
const { hash } = require("./rng.cjs");
const { csharpJson } = require("./make-real-like.cjs");

function canon(v) {
  if (Array.isArray(v)) return "[" + v.map(canon).join(",") + "]";
  if (v && typeof v === "object") return "{" + Object.keys(v).sort().map((k) => JSON.stringify(k) + ":" + canon(v[k])).join(",") + "}";
  if (typeof v === "number" && !Number.isFinite(v)) throw new Error("a non-finite number has no canonical form");
  return JSON.stringify(v);
}
const fnv = (s) => "fnv1a64:" + hash([s]).toString(16).padStart(16, "0");
const sha = (s) => crypto.createHash("sha256").update(s, "utf8").digest("hex");

// Each vector: a value, and (for contrast) the text System.Text.Json's defaults would write for it, which is not canonical.
const VECTORS = [
  ["apostrophe and member order", { b: 1, a: "I'll grab it tomorrow" }],
  ["member names in UTF-16 code-unit order (not code-point order)", { "\u00e9": 1, z: 2, Z: 3, a: 4, "\ud83d\ude00": 5, "\ufb01": 6 }],
  ["string escapes: only \", \\ and controls", { s: "quote\" backslash\\ tab\t newline\n bell\u0007 del\u007f e\u0301 \u00e9 <tag> & + ' \u2028 \ud83d\ude00" }],
  ["numbers as ECMAScript writes them", { n: [0, -0, 1, -1.5, 0.05, 0.1, 0.1 + 0.2, 26540, 1e21, 1e-7, 123456789012345680000, 5e-324, 1.7976931348623157e308, 0.000001] }],
  ["null, booleans, empty containers", { x: null, y: [true, false, {}], z: [] }],
];

function main() {
  const src = process.argv[2] || path.join(__dirname, "..", "lives-observer-example-v0.2.json");
  const lines = [];
  for (const [name, v] of VECTORS) {
    const c = canon(v), cs = csharpJson(v);
    lines.push({ name, canonical: c, fnv1a64: fnv(c), sha256: sha(c), csharpDefault: cs, csharpDefaultFnv: fnv(cs), same: c === cs });
  }
  const f = JSON.parse(fs.readFileSync(src, "utf8"));
  const t = (f.reflections || [])[0];
  if (t) lines.push({ name: `fixture request ${t.request.id} (requestHash)`, canonicalBytes: Buffer.byteLength(canon(t.request)), fnv1a64: fnv(canon(t.request)),
    csharpDefaultFnv: fnv(csharpJson(t.request)) });
  if (f.characterInspiration?.snapshot) lines.push({ name: "fixture archetype snapshot (snapshotHash)", canonicalBytes: Buffer.byteLength(canon(f.characterInspiration.snapshot)),
    sha256: sha(canon(f.characterInspiration.snapshot)), recorded: f.characterInspiration.snapshotHash });
  for (const l of lines) console.log(JSON.stringify(l));
}
module.exports = { VECTORS, canon, fnv, sha };
if (require.main === module) main();
