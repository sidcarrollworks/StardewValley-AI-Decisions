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
  const ids = [["reflection-choice","1:opp:13510:Penny:0"],["reflection-choice","1:opp:13511:Pam:0"],["lives-choice","dec:14010:Pam:0"],["lives-choice","dec:14028:Penny:0"],["lives-choice","dec:14055:Pam:0"],["lives-choice","dec:14060:Pam:1"],["haunt","Pam","14010"]];
  for (const p of ids) console.log(p.join(" | "), unit(1, ...p).toFixed(4));
}
