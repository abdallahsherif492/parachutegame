"""Generates Assets/Resources/ZombieKit/layout.json: the street, the wall, the two shooter towers, the safe-zone
camp behind the wall, the ruined city around the street and the skyline at its far end.

Coordinates are glTF/right-handed (z toward the zombies, wall front face at z = 0); the game mirrors X for Unity.
Usage:  python3 Tools/layout.py            (from the zombie-pile folder)
"""
import json, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Assets", "Resources", "ZombieKit", "layout.json")
old = json.load(open(OUT))
models = {m["name"]: m for m in old["models"]}
for m in json.load(open(os.path.join(HERE, "models_extra.json"))): models[m["name"]] = m

rnd = random.Random(11)
P = []      # props: base scene, same in every level (the gameplay depends on it)
def add(m, x, y, z, yaw=0, s=1): P.append({"m": m, "p": [round(x, 3), round(y, 3), round(z, 3)], "r": yaw, "s": s})

H = 5.2                 # the wall: two containers high
HW = 4.0                # lane half width (inner faces of the canyon)
CW, CL, CH = 2.56, 5.71, 2.6
R, G = "Container_Red", "Container_Green"
TZ = 14                 # z of the two shooter towers

# ---- the wall across the street (front face at z = 0)
for row in range(2):
    for i, x in enumerate((-8.565, -2.855, 2.855, 8.565)):
        add(R if (i + row) % 2 == 0 else G, x, row * CH, -1.28)
for x in (-5.4, -3.8, -2.2, 2.2, 3.8, 5.4):
    add("TrafficBarrier_2", x, H, -0.42, 0)

# ---- the road, all the way into the ruined city
for i in range(12): add("Street_Straight_Crack1" if i in (1, 4, 7, 10) else "Street_Straight", 0, 0, 4 + i * 8, 0)

# ---- the canyon: two container walls
for side in (-1, 1):
    x = side * (HW + CW / 2)
    for k in range(13):
        z = 1.2 + CL / 2 + k * (CL + 0.12)
        add((R, G)[(k + (side > 0)) % 2], x, 0, z, 90)
        if k not in (2, 6, 9, 11): add((G, R)[(k + (side > 0)) % 2], x, CH, z, 90)
    for k in range(5):
        add("StreetLights", side * (HW - 0.25), 0, 12 + k * 12, 90 if side < 0 else -90)
for (x, z) in [(-3.3, 7.5), (3.35, 12.5), (-3.4, 19), (3.3, 26)]: add("Barrel", x, 0, z, 0)
for (x, z, m, r) in [(-1.3, 3.0, "Blood_1", 20), (1.8, 10.5, "Blood_2", 140), (-0.9, 18, "Blood_3", 75), (1.2, 27, "Blood_2", 300)]: add(m, x, 0.02, z, r)

# ---- the two shooter towers: three containers each, beside the canyon
for side in (-1, 1):
    tx = side * (HW + CW + 0.35 + CL / 2)
    for row in range(3): add((G, R, G)[row], tx, row * CH, TZ, 0)
    add("TrafficBarrier_2", side * (HW + CW + 0.75), 3 * CH, TZ + 0.6, 90)
    add("TrafficBarrier_2", side * (HW + CW + 0.75), 3 * CH, TZ - 0.9, 90)
    add("Barrel", side * (HW + CW + 4.2), 3 * CH, TZ + 0.5, 0)
    add("Wheels_Stack", side * (HW + CW + 4.8), 3 * CH, TZ - 0.6, 0)
    # cars parked beside the towers
    add("Vehicle_Pickup", side * (HW + CW + 1.9), 0, 5.2, 180 + side * 12)
    add("Vehicle_Sports", side * (HW + CW + 2.1), 0, 26, side * 8)

# ---- the safe zone behind the wall: a camp with a fire, survivors and shelters
FIRE = (0.0, -10.5)
add("Pallet_Broken", FIRE[0] - 0.9, 0, FIRE[1] + 0.3, 40); add("Pallet_Broken", FIRE[0] + 0.9, 0, FIRE[1] - 0.5, -20)
for k in range(9):
    a = k / 9 * math.tau
    add("CinderBlock", FIRE[0] + math.cos(a) * 1.05, 0, FIRE[1] + math.sin(a) * 1.05, -math.degrees(a) + 90, 1.1)
add("Couch", -3.6, 0, -11.5, 100); add("Couch", 3.5, 0, -9.5, -85)
add("Vehicle_Truck_Armored", -9.2, 0, -9.0, 74); add("Vehicle_Pickup_Armored", 9.0, 0, -8.2, -102)
add("Vehicle_Truck", -8.5, 0, -18.5, 96)
for (x, z, r) in [(2.4, -6.3, 20), (3.2, -6.9, -15), (2.8, -6.6, 5)]: add("Chest", x, 0, z, r)
add("Chest_Special", -2.4, 0, -6.6, 12)
for (x, z, r) in [(-5.6, -6.2, 30), (5.9, -13.2, 80)]: add("Barrel", x, 0, z, r)
for (x, z) in [(-1.8, -14.2), (1.6, -13.8), (5.2, -5.9)]: add("TrashBag_1", x, 0, z, rnd.randint(0, 360))
add("Pipes", 6.6, 0, -15.6, 90); add("Pipes", -6.4, 0, -14.6, 70)
for k in range(7): add("TrafficCone_2", -6.6 + k * 2.2, 0, -21.5, rnd.randint(0, 360))
add(G, -13.4, 0, -6.5, 90); add(R, -13.4, CH, -6.5, 90); add(R, 13.4, 0, -11.5, 90); add(G, 13.4, CH, -11.5, 90)
add("TrafficBarrier_1", -10.2, 0, -21.6, 0); add("TrafficBarrier_1", 10.2, 0, -21.6, 0)
add("Wheels_Stack", -4.9, 0, -16.6, 0); add("Wheel", 6.1, 0, -6.4, 0)

# survivors: (model, x, z, yaw, animation)  yaw 0 = looking toward the zombies (+z)
SURV = []
def surv(m, x, z, yaw, anim="Idle", y=0.0): SURV.append({"m": m, "p": [round(x, 2), y, round(z, 2)], "r": yaw, "a": anim})
ring = [(-1.9, -9.4, 60), (-1.7, -11.9, 120), (0.3, -12.7, 180), (2.0, -11.6, 235), (2.1, -9.2, 290), (0.2, -8.0, 0)]
for i, (x, z, yaw) in enumerate(ring): surv("Characters_Matt" if i % 2 == 0 else "Characters_Sam", x, z, yaw, "Idle" if i != 3 else "Wave")
# watching the wall from behind
for i, (x, z) in enumerate([(-5.2, -5.4), (-3.6, -5.0), (4.2, -5.2), (6.0, -5.6), (-7.2, -7.0)]):
    surv("Characters_Sam" if i % 2 else "Characters_Matt", x, z, 8 * (i - 2), "Idle")
# militia on top of the wall, pistols out
for i, x in enumerate((-7.4, -4.9, 4.9, 7.4)): surv("Characters_Matt" if i % 2 == 0 else "Characters_Sam", x, -1.6, 0, "Idle_Gun", H)

# ---- the ruined city: districts along the street, and the far skyline (x, z, w, d, h, tilt_deg, seed)
CITY = []
def bld(x, z, w, d, h, tilt=0.0): CITY.append({"x": round(x, 2), "z": round(z, 2), "w": round(w, 2), "d": round(d, 2), "h": round(h, 2), "t": round(tilt, 1), "s": rnd.randint(1, 9999)})
for side in (-1, 1):
    for r in range(8):
        z0 = 10 + r * 11.5
        for c in range(3):
            if rnd.random() < 0.22: continue
            w, d = rnd.uniform(7.5, 10.5), rnd.uniform(8, 10.5)
            x = side * (19 + c * 12.5 + rnd.uniform(-0.6, 0.6))
            # low near the wall so the street stays in view, taller further out and further up the street
            h = rnd.uniform(6.5, 10) + r * 1.4 + c * 3.5 + (rnd.uniform(4, 9) if rnd.random() < 0.3 else 0)
            tilt = rnd.uniform(4, 8) * (1 if rnd.random() < 0.5 else -1) if rnd.random() < 0.15 else 0.0
            bld(x, z0 + rnd.uniform(-1, 1), w, d, h, tilt)
# far end of the street: the burning downtown
for r in range(6):
    for c in range(-6, 7):
        x = c * 11.5 + rnd.uniform(-1.5, 1.5)
        if abs(x) < 8 and r < 2: continue
        z = 96 + r * 12 + rnd.uniform(-1.5, 1.5)
        h = rnd.uniform(18, 42) + r * 4
        bld(x, z, rnd.uniform(8, 12), rnd.uniform(8, 12), h, rnd.uniform(-5, 5) if rnd.random() < 0.15 else 0.0)
# rubble at the base of the district blocks
for side in (-1, 1):
    for k in range(10):
        add(rnd.choice(["CinderBlock", "Pallet", "Wheel", "TrashBag_2", "Pipes"]), side * rnd.uniform(11.5, 15), 0, rnd.uniform(28, 80), rnd.randint(0, 360))

# fires among the ruins: x, y, z, size
FIRES = [{"x": x, "y": y, "z": z, "s": sc} for x, y, z, sc in
         [(-16, 9, 44, 1.3), (21, 7, 58, 1.6), (-30, 12, 70, 2.0), (12, 16, 90, 2.4), (-14, 22, 104, 3.0), (26, 24, 112, 3.2), (0, 30, 121, 3.6), (-40, 18, 84, 2.4), (38, 14, 78, 2.0)]]

# lit props beside the camp: lamp posts
add("StreetLights", -5.2, 0, -3.6, 0); add("StreetLights", 5.2, 0, -3.6, 180)

props = P
used = {p["m"] for p in props} | {s["m"] for s in SURV} | {"Zombie_Basic", "Zombie_Arm", "Zombie_Chubby", "Zombie_Ribcage", "Characters_Shaun", "Characters_Lis", "TrafficCone_1", "Chest_Special", "Barrel"}
out = {
    "wallHeight": H, "wallFront": 0.0, "halfWidth": HW,
    "tower": [HW + CW + 0.35 + 0.5, 7.8, float(TZ)],     # inner end of the tower top (x is mirrored for the other side)
    "camp": [FIRE[0], 0.0, FIRE[1]],
    "props": props, "survivors": SURV, "city": CITY, "fires": FIRES,
    "models": [models[n] for n in sorted(models) if n in used or n in models],
}
json.dump(out, open(OUT, "w"), separators=(",", ":"))
print(len(props), "props,", len(SURV), "survivors,", len(CITY), "buildings ->", OUT)
