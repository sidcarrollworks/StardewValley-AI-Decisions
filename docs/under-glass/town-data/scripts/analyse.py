"""Measure one town from Overture Maps extracts (buildings, places, land use, roads, water, addresses).
usage: analyse.py <datadir> <town> <centres.json> <out.json>
All distances in metres in a local equirectangular projection around the chosen centre."""
import sys, json, math, collections, re, os
import numpy as np, shapely, pyarrow.parquet as pq
from shapely.strtree import STRtree

D, town, centres, out = sys.argv[1:5]
lon0, lat0, centre_label = json.load(open(centres))[town]
R = 1500.0
RINGS = [(0, 200), (200, 500), (500, 1000), (1000, 1500)]
phi = math.radians(lat0)
KY = 111132.954 - 559.822 * math.cos(2 * phi) + 1.175 * math.cos(4 * phi)
KX = 111412.84 * math.cos(phi) - 93.5 * math.cos(3 * phi)
ADDRESSED = town in ("bloomfield", "monpazier")       # open address points available (US NAD/OpenAddresses, FR BAN)
AMAX = {"bloomfield": 1500, "monpazier": 600}.get(town, 350)  # largest untagged footprint taken as one home
AMIN = 35                                              # smallest untagged footprint taken as a home (UK tagged houses p5 = 28-39 m2)
if town in ("ogimachi", "ine"):
    AMAX, AMIN = 400, 60                               # JP tagged houses p5 = 63-70 m2; smaller ones are storehouses and sheds


def P(g):
    return shapely.transform(g, lambda c: (c - np.array([lon0, lat0])) * np.array([KX, KY]))


def load(layer, cols=None):
    f = f"{D}/{town}/{layer}.parquet"
    if not os.path.exists(f):
        return []
    rows = pq.read_table(f, columns=cols).to_pylist()
    for r in rows:
        r["g"] = P(shapely.from_wkb(r["geometry"]))
        del r["geometry"]
    return rows


circle = shapely.Point(0, 0).buffer(R, 128)

# ---------- water and land area per ring ----------
WATER_SUB = {"ocean", "lake", "river", "reservoir", "pond", "water", "canal"}
water = [r["g"] for r in load("water", ["subtype", "class", "geometry"])
         if r["subtype"] in WATER_SUB and r["g"].geom_type in ("Polygon", "MultiPolygon")]
water_u = shapely.union_all([w.intersection(circle) for w in water]) if water else shapely.Polygon()
ring_geom, ring_land_ha = [], []
for a, b in RINGS:
    rg = shapely.Point(0, 0).buffer(b, 128).difference(shapely.Point(0, 0).buffer(a, 128)) if a else shapely.Point(0, 0).buffer(b, 128)
    ring_geom.append(rg)
    ring_land_ha.append((rg.area - rg.intersection(water_u).area) / 1e4)

# ---------- land use ----------
lu = load("land_use", ["subtype", "class", "names", "geometry"])
FARM = {"farmyard", "farmland", "meadow", "greenhouse_horticulture", "orchard", "vineyard", "animal_keeping"}
NONRES_LU = {"industrial", "retail", "commercial", "quarry", "railway", "military", "construction", "landfill", "works", "brownfield"}
CIVIC_LU = {"school", "university", "college", "hospital", "clinic", "religious", "kindergarten"}
GATHER_LU = {"park", "playground", "village_green", "pitch", "recreation_ground", "plaza", "pedestrian", "sports_centre", "common"}
polys = lambda keys: [r["g"] for r in lu if r["class"] in keys and r["g"].geom_type in ("Polygon", "MultiPolygon")]
farm_t, nonres_t, civic_t, resid_t = (STRtree(polys(k)) for k in (FARM, NONRES_LU, CIVIC_LU, {"residential"}))
gather_polys = [r["g"] for r in lu if r["class"] in GATHER_LU and r["g"].geom_type in ("Polygon", "MultiPolygon") and r["g"].intersects(circle)]

# ---------- places (POIs) ----------
EXCL_CAT = {"home_service", "event_or_party_service", "design_service", "technical_service", "b2b_service",
            "b2b_office_and_professional_service", "b2b_science_and_technology_service", "manufacturer", "wholesaler",
            "supplier_or_distributor", "building_or_construction_service", "corporate_or_business_office", "media_service",
            "telecommunications_service", "environmental_or_ecological_service", "farm", "agricultural_service",
            "storage_facility", "rental_service", "housing_or_property_service", "senior_living_facility", "family_service",
            "private_lodging", "bed_and_breakfast", "campground", "lodging", "historic_site", "monument", "castle", "lighthouse",
            "parking", "public_transit_facility_or_service", "taxi_or_ride_share_service", "atm", "printing_service",
            "professional_service", "electric_utility_provider", "public_fountain", "sport_league", "sport_team",
            "civic_organization", "social_or_community_service", "recreational_equipment_rental", "food_truck_stand",
            "travel_and_transportation", "arts_and_entertainment", "community_and_government", "sports_and_recreation",
            "food_service", "national_park", "golf_course", "mountain", "beach", "bridge", "river", "lake", "marina"}
EXCL_TOP = {"geographic_entities", "?"}
SHOP_TOP = {"shopping", "food_and_drink"}
NOT_SHOP = {"auto_dealer", "vehicle_dealer", "vehicle_parts_store", "distillery"}
GATHER_CAT = {"community_center", "library", "christian_place_of_worship", "buddhist_place_of_worship", "religious_organization",
              "elementary_school", "middle_school", "high_school", "preschool", "park", "sport_or_fitness_facility", "sport_field",
              "swimming_pool", "gym", "stadium_arena", "museum", "cultural_center", "music_venue", "movie_theater", "theatre_venue",
              "event_venue", "festival_venue", "fairgrounds", "government_office", "courthouse", "bank_or_credit_union",
              "shipping_or_delivery_service", "pharmacy_and_drug_store", "art_gallery", "sport_or_recreation_club"}

raw = load("place", ["names", "confidence", "basic_category", "taxonomy", "operating_status", "geometry"])
pois = []
for r in raw:
    top = ((r["taxonomy"] or {}).get("hierarchy") or ["?"])[0]
    cat = r["basic_category"]
    if (r["confidence"] or 0) < 0.5 or (r["operating_status"] not in (None, "open")):
        continue
    if cat is None or cat in EXCL_CAT or top in EXCL_TOP:
        continue
    if r["g"].distance(shapely.Point(0, 0)) > R + 50:
        continue
    name = re.sub(r"[^0-9a-z぀-鿿]", "", ((r["names"] or {}).get("primary") or "").lower())
    everyday = (top in SHOP_TOP and cat not in NOT_SHOP) or cat in GATHER_CAT
    shop = top in SHOP_TOP and cat not in NOT_SHOP
    pois.append({"name": name, "cat": cat, "top": top, "g": r["g"], "everyday": everyday, "shop": shop})
# dedupe: same or contained normalised name within 60 m
keep = []
for p in pois:
    dup = False
    for q in keep:
        if p["name"] and q["name"] and (p["name"] in q["name"] or q["name"] in p["name"]) and p["g"].distance(q["g"]) < 60:
            dup = True
            break
    if not dup:
        keep.append(p)
pois = [p for p in keep if p["g"].distance(shapely.Point(0, 0)) <= R]

# ---------- addresses ----------
addr = [r["g"] for r in load("address", ["geometry"])] if ADDRESSED else []

# ---------- buildings ----------
RES = {"house", "detached", "semidetached_house", "terrace", "residential", "apartments", "bungalow", "cabin", "farm",
       "static_caravan", "dormitory", "houseboat"}
OUT = {"garage", "garages", "shed", "greenhouse", "barn", "roof", "carport", "farm_auxiliary", "outbuilding", "service", "toilets",
       "shelter", "bunker", "stable", "cowshed", "silo", "storage_tank", "hut", "transformer_tower", "water_tower", "hangar",
       "warehouse", "industrial", "manufacture", "parking", "construction", "ruins", "boathouse", "allotment_house", "sty",
       "glasshouse", "tank", "digester", "slurry_tank"}
CIVCOM = {"retail", "commercial", "office", "supermarket", "kiosk", "church", "chapel", "cathedral", "temple", "shrine", "mosque",
          "synagogue", "religious", "public", "civic", "government", "school", "kindergarten", "college", "university", "hospital",
          "hotel", "fire_station", "train_station", "library", "pavilion", "sports_hall", "sports_centre", "museum", "townhall",
          "community_centre", "post_office", "bank", "pub", "restaurant"}
CIVCOM_SUB = {"commercial", "civic", "religious", "education", "medical", "entertainment"}
OTHER_SUB = {"outbuilding", "agricultural", "industrial", "service", "military", "transportation"}

bl = [r for r in load("building", ["subtype", "class", "sources", "names", "geometry"]) if r["g"].geom_type in ("Polygon", "MultiPolygon")]
bl = [r for r in bl if r["g"].centroid.distance(shapely.Point(0, 0)) <= R]
btree = STRtree([r["g"] for r in bl])
for r in bl:
    r["area"] = r["g"].area
    r["c"] = r["g"].centroid
    r["d"] = r["c"].distance(shapely.Point(0, 0))
    r["poi"] = 0
    r["addr"] = 0
for p in pois:
    hits = btree.query(p["g"], predicate="dwithin", distance=3.0)
    if len(hits):
        best = min(hits, key=lambda i: bl[i]["g"].distance(p["g"]))
        bl[best]["poi"] += 1
for a in addr:
    hits = btree.query(a, predicate="intersects")
    if not len(hits):
        hits = btree.query(a, predicate="dwithin", distance=25.0)
    if len(hits):
        best = min(hits, key=lambda i: (bl[i]["g"].distance(a), -bl[i]["area"]))
        bl[best]["addr"] += 1


def within(tree, g):
    return len(tree.query(g, predicate="intersects")) > 0


tagged_res = 0
for r in bl:
    cls, sub = r["class"], r["subtype"]
    if cls in RES or (sub == "residential" and cls not in OUT):
        r["kind"] = "home"; tagged_res += 1
    elif cls in CIVCOM or (sub in CIVCOM_SUB and cls not in OUT):
        r["kind"] = "civcom"
    elif cls in OUT or sub in OTHER_SUB:
        r["kind"] = "other"
    elif r["poi"]:
        r["kind"] = "civcom"
    elif r["area"] < AMIN:
        r["kind"] = "other"
    elif town == "ine" and r["g"].distance(water_u) < 8:
        r["kind"] = "other"                            # funaya boathouses stand on the waterline
    elif within(civic_t, r["c"]):
        r["kind"] = "civcom"
    elif within(farm_t, r["c"]) or within(nonres_t, r["c"]):
        r["kind"] = "other"
    elif ADDRESSED:
        r["kind"] = "home" if (r["addr"] and r["area"] <= AMAX) else "other"
    elif r["area"] <= AMAX or (r["area"] <= 1500 and within(resid_t, r["c"])):
        r["kind"] = "home"
    else:
        r["kind"] = "other"

homes = [r for r in bl if r["kind"] == "home"]
civ = [r for r in bl if r["kind"] == "civcom"]
hd = np.array([r["d"] for r in homes]) if homes else np.array([0.0])
ed = np.array([p["g"].distance(shapely.Point(0, 0)) for p in pois]) if pois else np.array([np.nan])
cd = np.array([r["d"] for r in civ]) if civ else np.array([np.nan])
q = lambda a, x: float(np.nanpercentile(a, x)) if len(a) else None

rings = []
for (a, b), rg, ha in zip(RINGS, ring_geom, ring_land_ha):
    hs = [r for r in homes if a <= r["d"] < b or (b == 1500 and r["d"] == 1500)]
    fp = sum(r["g"].intersection(rg).area for r in hs)
    es = sum(1 for d in ed if a <= d < b) if pois else 0
    rings.append({"ring": f"{a}-{b}", "land_ha": round(ha, 1), "homes": len(hs), "homes_per_ha": round(len(hs) / ha, 2) if ha else None,
                  "res_footprint_pct": round(100 * fp / (ha * 1e4), 1) if ha else None, "establishments": es,
                  "addresses": sum(1 for g in addr if a <= g.distance(shapely.Point(0, 0)) < b) if ADDRESSED else None})

# nearest everyday place (shop/food/drink, civic, worship, school, park/green/pitch) for each home
ev = [p["g"] for p in pois if p["everyday"]] + gather_polys
shops = [p["g"] for p in pois if p["shop"]]
def nearest(targets):
    if not targets or not homes:
        return None, None
    t = STRtree(targets)
    ds = np.array([targets[t.nearest(r["c"])].distance(r["c"]) for r in homes])
    return round(q(ds, 50)), round(q(ds, 90))
near_ev = nearest(ev)
near_shop = nearest(shops)

# streets
PUBLIC = {"motorway", "trunk", "primary", "secondary", "tertiary", "residential", "unclassified", "living_street", "pedestrian", "unknown"}
PATHS = {"footway", "path", "steps", "cycleway", "bridleway", "sidewalk", "crosswalk"}
segs = [r for r in load("segment", ["subtype", "class", "names", "geometry"]) if r["subtype"] == "road"]
settled = shapely.union_all([r["g"].buffer(50, 8) for r in homes + civ]).intersection(circle) if (homes or civ) else shapely.Polygon()
L = collections.Counter()
bear = []
core_bear = []
core = shapely.Point(0, 0).buffer(400, 64)
c500 = shapely.Point(0, 0).buffer(500, 64)
for r in segs:
    g = r["g"].intersection(circle)
    if g.is_empty:
        continue
    gs = g.intersection(settled)
    cls = r["class"]
    grp = "public" if cls in PUBLIC else "service" if cls == "service" else "track" if cls == "track" else "path" if cls in PATHS else "other"
    L[grp + "_circle"] += g.length
    L[grp + "_settled"] += gs.length
    L[grp + "_within500"] += g.intersection(c500).length
    if grp == "public" and not gs.is_empty:
        for part in getattr(gs, "geoms", [gs]):
            if part.geom_type != "LineString":
                continue
            c = np.asarray(part.coords)
            for (x1, y1), (x2, y2) in zip(c[:-1], c[1:]):
                ln = math.hypot(x2 - x1, y2 - y1)
                if ln > 0.5:
                    bb_ = math.degrees(math.atan2(x2 - x1, y2 - y1)) % 180.0
                    bear.append((bb_, ln))
                    if math.hypot((x1 + x2) / 2, (y1 + y2) / 2) <= 400:
                        core_bear.append((bb_, ln))
# Boeing (2019) orientation order: 36 bins of 10 deg, bidirectional, length-weighted
def orient(bs):
    if not bs:
        return None
    h = np.zeros(36)
    for b, w in bs:
        for bb in (b, b + 180):
            h[int(((bb + 5) % 360) // 10)] += w
    pr = h / h.sum()
    H = -sum(x * math.log(x) for x in pr if x > 0)
    Hmax, Hg = math.log(36), 1.386
    return 1 - ((H - Hg) / (Hmax - Hg)) ** 2
order = orient(bear)
core_order = orient(core_bear)
# elongation of the home cloud (PCA)
if len(homes) > 3:
    xy = np.array([[r["c"].x, r["c"].y] for r in homes])
    ev_ = np.sort(np.linalg.eigvalsh(np.cov(xy.T)))
    elong = math.sqrt(ev_[1] / ev_[0])
else:
    elong = None
within500 = float(np.mean(hd <= 500)) if homes else None


# Perpendicular-axes test: fold bearings mod 90, find the axis pair holding most street length (+-7.5 deg),
# then how evenly that length splits between the two perpendicular axes. A grid needs both.
def axes(bs):
    if not bs:
        return None, None, 0.0
    b = np.array([x for x, _ in bs]); w = np.array([y for _, y in bs])
    best = (0, 0)
    for th in range(90):
        dd = np.abs(((b - th) + 45) % 90 - 45)
        sh = w[dd <= 7.5].sum()
        if sh > best[0]:
            best = (sh, th)
    sh, th = best
    da = np.abs(((b - th) + 90) % 180 - 90)          # distance to axis A (mod 180)
    inA = w[da <= 7.5].sum(); inB = w[np.abs(da - 90) <= 7.5].sum()
    return round(sh / w.sum(), 3), round(min(inA, inB) / max(inA, inB), 3) if max(inA, inB) else 0.0, w.sum()
aligned, balance, _ = axes(bear)
c_aligned, c_balance, c_len = axes(core_bear)
# main-street share: homes within 60 m of the single named public street that has the most homes along it
named = collections.defaultdict(list)
for r in segs:
    n = (r.get("names") or {}).get("primary")
    if n and r["class"] in PUBLIC:
        named[n].append(r["g"])
best_street, main_share = None, 0.0
if homes:
    hpts = shapely.MultiPoint([r["c"] for r in homes])
    for n, gs in named.items():
        u = shapely.union_all(gs).buffer(60, 8)
        k = len(shapely.intersection(hpts, u).geoms) if not shapely.intersection(hpts, u).is_empty and hasattr(shapely.intersection(hpts, u), "geoms") else (1 if not shapely.intersection(hpts, u).is_empty else 0)
        if k / len(homes) > main_share:
            best_street, main_share = n, k / len(homes)
is_grid = (aligned is not None and aligned >= 0.6 and balance >= 0.33) or (c_len >= 2000 and c_aligned >= 0.6 and c_balance >= 0.33)
if is_grid:
    shape = "grid"
elif (elong is not None and elong >= 2.0) or main_share >= 0.35:
    shape = "linear"
elif within500 is not None and within500 < 0.35:
    shape = "dispersed"
else:
    shape = "nucleated"

res = {
    "town": town, "centre": [lon0, lat0, centre_label],
    "buildings_in_circle": len(bl),
    "building_sources": dict(collections.Counter((r["sources"][0]["dataset"] if r["sources"] else None) for r in bl)),
    "homes": len(homes), "homes_tagged": sum(1 for r in homes if r["class"] in RES or r["subtype"] == "residential"),
    "home_footprint_median_m2": round(q(np.array([r["area"] for r in homes]), 50)) if homes else None,
    "civcom_buildings": len(civ), "establishments": len(pois),
    "establishment_mix": dict(collections.Counter(p["top"] for p in pois).most_common()),
    "addresses_in_circle": sum(1 for g in addr if g.distance(shapely.Point(0, 0)) <= R) if ADDRESSED else None,
    "home_addresses": sum(r["addr"] for r in homes) if ADDRESSED else None,
    "est_d50": q(ed, 50), "est_d90": q(ed, 90), "civ_d50": q(cd, 50), "civ_d90": q(cd, 90),
    "home_d50": q(hd, 50), "home_d90": q(hd, 90),
    "rings": rings,
    "nearest_everyday_p50_p90": near_ev, "nearest_shop_p50_p90": near_shop,
    "street_m": {k: round(v) for k, v in sorted(L.items())},
    "settled_area_ha": round(settled.area / 1e4, 1),
    "orientation_order": round(order, 3) if order is not None else None,
    "core_orientation_order": round(core_order, 3) if core_order is not None else None,
    "axes_aligned_share": aligned, "axes_balance": balance, "core_axes_aligned_share": c_aligned, "core_axes_balance": c_balance,
    "core_public_street_m": round(c_len), "main_street": best_street, "main_street_home_share": round(main_share, 3),
    "everyday_pois": sum(1 for p in pois if p["everyday"]), "gathering_landuse": len(gather_polys),
    "everyday_d50": q(np.array([p["g"].distance(shapely.Point(0, 0)) for p in pois if p["everyday"]]), 50),
    "everyday_d90": q(np.array([p["g"].distance(shapely.Point(0, 0)) for p in pois if p["everyday"]]), 90),
    "elongation": round(elong, 2) if elong else None, "homes_within_500m": round(within500, 3) if within500 is not None else None,
    "shape": shape, "homes_within_500m_n": int(np.sum(hd <= 500)) if homes else 0,
    "far_establishments": sorted([(round(p["g"].distance(shapely.Point(0, 0))), p["cat"], p["name"][:30]) for p in pois if p["g"].distance(shapely.Point(0, 0)) > 700])[:40], "water_ha_in_circle": round(water_u.area / 1e4, 1),
}
json.dump(res, open(out, "w"), indent=1, ensure_ascii=False)
print(json.dumps(res, ensure_ascii=False))
