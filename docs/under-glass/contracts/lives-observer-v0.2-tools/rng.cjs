// Rng.Hash and Rng.Unit as sim/UnderGlass.Sim/Rng.cs:11-37 computes them (FNV-1a 64 over UTF-16 code units
// with a 0x1F separator after each part; SplitMix64 mixing). Copied from the v0.1 tools. Checked 2026-10-14
// against a compiled copy of Rng.cs (a9eadaf) on five keys, including lives-choice, reflection-choice and
// errand-ask: identical to the last bit.
const M = (1n << 64n) - 1n;
function hash(parts) {
  let h = 14695981039346656037n;
  for (const part of parts) {
    for (let i = 0; i < part.length; i++) { h ^= BigInt(part.charCodeAt(i)); h = (h * 1099511628211n) & M; }
    h ^= 0x1Fn; h = (h * 1099511628211n) & M;
  }
  return h;
}
function mix(z) {
  z = (z + 0x9E3779B97F4A7C15n) & M;
  z = ((z ^ (z >> 30n)) * 0xBF58476D1CE4E5B9n) & M;
  z = ((z ^ (z >> 27n)) * 0x94D049BB133111EBn) & M;
  return z ^ (z >> 31n);
}
function unit(seed, ...parts) { return Number((mix(hash(parts) ^ mix(BigInt(seed) & M))) >> 11n) / 2 ** 53; }
module.exports = { hash, mix, unit };
if (require.main === module) {
  // node rng.cjs <seed> <part> [part...]  prints Rng.Unit(seed, parts)
  const [seed, ...parts] = process.argv.slice(2);
  if (!seed || !parts.length) { console.error("usage: node rng.cjs <seed> <part> [part...]"); process.exit(2); }
  console.log(unit(Number(seed), ...parts));
}
