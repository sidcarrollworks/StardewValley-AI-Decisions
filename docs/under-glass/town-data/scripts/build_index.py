"""Build a row-group bbox index for one Overture type (all files) by reading Parquet footers.
usage: build_index.py <release> <theme> <type> <out.json>"""
import sys, json, re, urllib.request
from concurrent.futures import ThreadPoolExecutor
sys.path.insert(0, sys.argv[0].rsplit("/", 1)[0])
import pyarrow.parquet as pq
from rangefile import RangeFile, _OPENER, STATS

BASE = "https://overturemaps-us-west-2.s3.us-west-2.amazonaws.com"
release, theme, typ, out = sys.argv[1:5]


def list_keys(prefix):
    keys, sizes, token = [], [], None
    while True:
        url = f"{BASE}/?list-type=2&prefix={prefix}"
        if token:
            url += "&continuation-token=" + urllib.parse.quote(token)
        with _OPENER.open(url, timeout=60) as r:
            t = r.read().decode()
        keys += re.findall(r"<Key>([^<]*)</Key>", t)
        sizes += [int(s) for s in re.findall(r"<Size>(\d+)</Size>", t)]
        m = re.search(r"<NextContinuationToken>([^<]*)</NextContinuationToken>", t)
        if not m:
            return list(zip(keys, sizes))
        token = m.group(1)


import urllib.parse
files = list_keys(f"release/{release}/theme={theme}/type={typ}/")


def col_index(md, name):
    rg = md.row_group(0)
    for i in range(rg.num_columns):
        if rg.column(i).path_in_schema == name:
            return i
    raise KeyError(name)


def one(item):
    key, size = item
    url = f"{BASE}/{key}"
    f = RangeFile(url, size=size, block=1 << 20)
    md = pq.ParquetFile(f).metadata
    ix = {n: col_index(md, n) for n in ("bbox.xmin", "bbox.xmax", "bbox.ymin", "bbox.ymax")}
    rgs = []
    for g in range(md.num_row_groups):
        rg = md.row_group(g)
        s = {n: rg.column(i).statistics for n, i in ix.items()}
        rgs.append([g, rg.num_rows, s["bbox.xmin"].min, s["bbox.xmax"].max, s["bbox.ymin"].min, s["bbox.ymax"].max])
    return {"url": url, "size": size, "rgs": rgs}


with ThreadPoolExecutor(16) as ex:
    res = list(ex.map(one, files))
json.dump(res, open(out, "w"))
print(len(res), "files", sum(len(r["rgs"]) for r in res), "row groups", STATS)
