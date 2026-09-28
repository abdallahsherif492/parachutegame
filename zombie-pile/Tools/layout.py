import json
L = []
def add(m, x, y, z, yaw=0, s=1): L.append({"m": m, "p": [round(x,3), round(y,3), round(z,3)], "r": yaw, "s": s})
H = 5.2              # the wall: two containers high
HW = 4.0             # lane half width (inner faces of the canyon)
CW, CL, CH = 2.56, 5.71, 2.6
R, G = "Container_Red", "Container_Green"
# ---- the wall across the street (front face at z = 0), wide enough to fill the screen
for row in range(2):
    for i, x in enumerate((-8.565, -2.855, 2.855, 8.565)):
        add(R if (i + row) % 2 == 0 else G, x, row * CH, -1.28)
# parapet on the wall top: low concrete barriers, a gap in the middle for the gunner
for x in (-5.4, -3.8, -2.2, 2.2, 3.8, 5.4):
    add("TrafficBarrier_2", x, H, -0.42, 0)
# ---- the road
for i in range(10): add("Street_Straight_Crack1" if i in (1, 4, 7) else "Street_Straight", 0, 0, 4 + i * 8, 0)
# ---- the canyon: two neat container walls, two high
for side in (-1, 1):
    x = side * (HW + CW / 2)
    for k in range(12):
        z = 1.2 + CL / 2 + k * (CL + 0.12)
        add((R, G)[(k + (side > 0)) % 2], x, 0, z, 90)
        if k not in (2, 6, 9): add((G, R)[(k + (side > 0)) % 2], x, CH, z, 90)
    for k in range(5):
        add("StreetLights", side * (HW - 0.25), 0, 12 + k * 12, 90 if side < 0 else -90)
# explosive barrels along the lane edges (shootable, not solid)
for (x, z) in [(-3.3, 7.5), (3.35, 12.5), (-3.4, 19), (3.3, 26)]: add("Barrel", x, 0, z, 0)
for (x, z, m, r) in [(-1.3, 3.0, "Blood_1", 20), (1.8, 10.5, "Blood_2", 140), (-0.9, 18, "Blood_3", 75), (1.2, 27, "Blood_2", 300)]: add(m, x, 0.02, z, r)
# ---- outside the canyon (seen at the screen edges): parked wrecks, water towers, more containers
for side in (-1, 1):
    x0 = side * (HW + CW + 1.7)
    add("Vehicle_Pickup", x0, 0, 5.5, 180 + side * 12)
    add("Vehicle_Sports", x0 + side * 0.4, 0, 16, side * 8)
    add("Barrel", x0 - side * 0.2, 0, 10.5, 0); add("Barrel", x0 + side * 0.6, 0, 11.3, 0)
    add("Wheels_Stack", x0, 0, 21.5, 0)
    for k in range(8):
        add((G, R)[k % 2], side * (HW + CW + 5.0), 0, 26 + k * (CL + 0.2), 90)
        add((R, G)[k % 2], side * (HW + CW + 5.0), CH, 26 + k * (CL + 0.2), 90)
add("WaterTower", -13.5, 0, 12, 0); add("WaterTower", 14, 0, 22, 0)
# ---- the flank tower (second shooter): three containers stacked beside the canyon, in front of the wall
TZ = 14
for row in range(3):
    add((G, R, G)[row], HW + CW + 0.35 + CL / 2, row * CH, TZ, 0)
add("TrafficBarrier_2", HW + CW + 0.75, 3 * CH, TZ + 0.6, 90); add("TrafficBarrier_2", HW + CW + 0.75, 3 * CH, TZ - 0.9, 90)
add("Barrel", HW + CW + 4.2, 3 * CH, TZ + 0.5, 0); add("Wheels_Stack", HW + CW + 4.8, 3 * CH, TZ - 0.6, 0)
# ---- our side of the wall
add("TrashBag_2", -4.6, H, -1.9, 30); add("Barrel", 5.0, H, -2.0, 0); add("Wheels_Stack", 4.1, H, -2.1, 0); add("Barrel", -5.4, H, -1.7, 0)
RESULT = {"wallHeight": H, "wallFront": 0.0, "halfWidth": HW, "props": L}
