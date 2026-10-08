"""Download a column subset of Overture divisions (localities) once, then search by name.
usage: divisions.py <index/division.json> <cache.parquet> name1 name2 ..."""
import sys, json, os
sys.path.insert(0, sys.argv[0].rsplit("/", 1)[0])
import pyarrow.parquet as pq, pyarrow.compute as pc, shapely
from rangefile import RangeFile

ix, cache = sys.argv[1], sys.argv[2]
if not os.path.exists(cache):
    f = json.load(open(ix))[0]
    pf = pq.ParquetFile(RangeFile(f["url"], size=f["size"]))
    names = [n.name for n in pf.schema_arrow]
    want = [c for c in ("id", "names", "subtype", "class", "country", "region", "population", "wikidata", "geometry", "bbox", "local_type") if c in names]
    t = pf.read(columns=want)
    pq.write_table(t, cache)
t = pq.read_table(cache)
print(t.schema.names)
prim = pc.struct_field(t.column("names"), "primary")
for q in sys.argv[3:]:
    m = pc.equal(prim, q)
    sub = t.filter(m)
    for r in sub.to_pylist():
        g = shapely.from_wkb(r["geometry"])
        print(q, "|", r["subtype"], r.get("class"), r["country"], r.get("region"), "pop", r.get("population"), "wd", r.get("wikidata"), "pt", (round(g.x, 5), round(g.y, 5)) if g.geom_type == "Point" else g.geom_type)
