import json, sys
R = sys.argv[1]
HH = {"mevagissey": 2.36, "hay": 1.87, "longmelford": 2.36, "laxton": 2.36, "bloomfield": 2.34, "monpazier": 2.2, "ogimachi": 2.61, "ine": 2.27}
order = ["mevagissey", "hay", "longmelford", "laxton", "bloomfield", "monpazier", "ogimachi", "ine"]
rows = []
for t in order:
    r = json.load(open(f"{R}/{t}.json"))
    est = r["homes"] * HH[t]
    sm = r["street_m"]
    pub = sm.get("public_settled", 0); allp = pub + sm.get("service_settled", 0) + sm.get("path_settled", 0) + sm.get("track_settled", 0)
    est_ring = [x["establishments"] for x in r["rings"]]
    tot = sum(est_ring) or 1
    rows.append((t, r, est, pub, allp, est_ring, tot))
    print(f"## {t}")
    print(f" homes={r['homes']} (tagged {r['homes_tagged']}), median footprint {r['home_footprint_median_m2']} m2, est residents {est:.0f} (x{HH[t]})")
    print(f" addresses in circle {r['addresses_in_circle']}, on homes {r['home_addresses']}")
    print(f" civcom bldgs {r['civcom_buildings']}, establishments {r['establishments']}, everyday {r['everyday_pois']} + {r['gathering_landuse']} green/park polys")
    print(f" est P50/P90 {r['est_d50']:.0f}/{r['est_d90']:.0f}; civ bldg P50/P90 {r['civ_d50']:.0f}/{r['civ_d90']:.0f}; everyday P50/P90 {r['everyday_d50']:.0f}/{r['everyday_d90']:.0f}; homes P50/P90 {r['home_d50']:.0f}/{r['home_d90']:.0f}")
    print(" rings (land ha, homes, homes/ha, res footprint %, est share %):",
          [(x["ring"], x["land_ha"], x["homes"], x["homes_per_ha"], x["res_footprint_pct"], round(100 * x["establishments"] / tot)) for x in r["rings"]])
    print(f" nearest everyday P50/P90 {r['nearest_everyday_p50_p90']}, nearest shop {r['nearest_shop_p50_p90']}")
    print(f" public streets in settled area {pub/1000:.1f} km = {pub/r['homes']:.1f} m/home = {pub/est:.1f} m/resident; all ways {allp/1000:.1f} km = {allp/est:.1f} m/resident; settled area {r['settled_area_ha']} ha; public streets in circle {sm.get('public_circle',0)/1000:.1f} km")
    print(f" shape {r['shape']}: elong {r['elongation']}, homes<=500m {r['homes_within_500m']}, phi {r['orientation_order']} (core {r['core_orientation_order']}), axes {r['axes_aligned_share']}/{r['axes_balance']} core {r['core_axes_aligned_share']}/{r['core_axes_balance']}, main street {r['main_street']} {r['main_street_home_share']}")
    print(f" homes per establishment {r['homes']/max(1,r['establishments']):.1f}; establishments per 100 residents {100*r['establishments']/est:.1f}; water ha {r['water_ha_in_circle']}; mix {r['establishment_mix']}")
    print(f" sources {r['building_sources']}")
