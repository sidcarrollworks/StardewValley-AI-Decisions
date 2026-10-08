# Real towns, measured

The numbers behind `docs/under-glass/town-layout-research.md`: eight small towns measured from
Overture Maps open data (release 2026-09-23.1; the layers built from OpenStreetMap are under the
ODbL, © OpenStreetMap contributors). The raw extracts (about 570 MB) are not kept here; the scripts
rebuild them.

- `scripts/towns.json` and `scripts/centres.json`: each town's centre (longitude, latitude), the
  square, high street or town hall the rings are measured from.
- `results/<town>.json`: what `analyse.py` measured within 1.5 km of the centre (homes and
  establishments by ring, density, the commercial core, streets, land use).
- `results-extra/<town>.json`: what `analyse_extra.py` added (spacing between homes, and the share
  of homes within a walk of a shop or an everyday place).

To rebuild, with Python 3, `pyarrow`, `shapely` and `numpy` (set `SSL_CERT_FILE` if a proxy needs
its own certificate):

```bash
python3 scripts/build_index.py 2026-09-23.1 buildings building index/building.json
python3 scripts/build_index.py 2026-09-23.1 places place index/place.json
python3 scripts/build_index.py 2026-09-23.1 base land_use index/land_use.json
python3 scripts/build_index.py 2026-09-23.1 transportation segment index/segment.json
python3 scripts/fetch.py index/building.json <minlon> <minlat> <maxlon> <maxlat> towndata/<town>/building.parquet   # likewise place, land_use, segment
python3 scripts/analyse.py towndata <town> scripts/centres.json results/<town>.json
python3 scripts/analyse_extra.py towndata <town> scripts/centres.json results-extra/<town>.json
python3 scripts/summarise.py results
```

The bounding box for each town is about 1.8 km either side of its centre. These scripts read
remote Parquet files by range requests (`rangefile.py`), and are not part of the simulator.
