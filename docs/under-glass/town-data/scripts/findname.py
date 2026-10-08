import sys, json, math, re
import pyarrow.parquet as pq, shapely
D, town, towns, pat = sys.argv[1:5]
lon0, lat0 = json.load(open(towns))[town]
kx = 111320 * math.cos(math.radians(lat0)); ky = 110540
rx = re.compile(pat, re.I)
for layer in ("building", "place", "land_use", "segment"):
    for r in pq.read_table(f"{D}/{town}/{layer}.parquet", columns=["names", "geometry"] + (["class"] if layer != "place" else ["basic_category"])).to_pylist():
        n = (r["names"] or {}).get("primary") or ""
        if rx.search(n):
            c = shapely.from_wkb(r["geometry"]).centroid
            dx, dy = (c.x - lon0) * kx, (c.y - lat0) * ky
            print(layer, "|", n, "|", r.get("class") or r.get("basic_category"), "|", f"{c.x:.6f},{c.y:.6f}", f"d={math.hypot(dx, dy):.0f}m")
