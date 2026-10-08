"""Fetch Overture rows inside a bbox using a prebuilt row-group index.
usage: fetch.py <index.json> <minlon> <minlat> <maxlon> <maxlat> <out.parquet> [col,col,...]"""
import sys, json
sys.path.insert(0, sys.argv[0].rsplit("/", 1)[0])
import pyarrow as pa, pyarrow.parquet as pq, pyarrow.compute as pc
from concurrent.futures import ThreadPoolExecutor
from rangefile import RangeFile, STATS

ix = json.load(open(sys.argv[1]))
x0, y0, x1, y1 = map(float, sys.argv[2:6])
out = sys.argv[6]
cols = sys.argv[7].split(",") if len(sys.argv) > 7 else None

jobs = []
for f in ix:
    gs = [g for g, n, xmin, xmax, ymin, ymax in f["rgs"] if not (xmin > x1 or xmax < x0 or ymin > y1 or ymax < y0)]
    if gs:
        jobs.append((f["url"], f["size"], gs))


def one(job):
    url, size, gs = job
    pf = pq.ParquetFile(RangeFile(url, size=size, block=1 << 20))
    tabs = []
    for g in gs:
        t = pf.read_row_group(g, columns=cols)
        b = t.column("bbox")
        xmin = pc.struct_field(b, "xmin"); xmax = pc.struct_field(b, "xmax")
        ymin = pc.struct_field(b, "ymin"); ymax = pc.struct_field(b, "ymax")
        m = pc.and_(pc.and_(pc.less_equal(xmin, x1), pc.greater_equal(xmax, x0)),
                    pc.and_(pc.less_equal(ymin, y1), pc.greater_equal(ymax, y0)))
        tabs.append(t.filter(m))
    return tabs


with ThreadPoolExecutor(8) as ex:
    parts = [t for ts in ex.map(one, jobs) for t in ts]
if parts:
    t = pa.concat_tables(parts, promote_options="default")
    pq.write_table(t, out)
    print(out, t.num_rows, "rows from", sum(len(j[2]) for j in jobs), "row groups", STATS)
else:
    print(out, "no row groups")
