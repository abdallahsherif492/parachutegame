using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Pure C# (no Unity types) so the wire format can be tested outside Unity: Tools/tests/run.sh
namespace ZombiePile.NetCore
{
    /// Numbers travel as integers: positions in 5 cm steps, everything else scaled. Text frames, '|' between
    /// fields, ';' between list items, ',' between values of one item.
    public static class W
    {
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        public static int Q(float v) { return (int)Math.Round(v * 20.0); }
        public static float F(int q) { return q / 20f; }
        public static string N(float v) { return Q(v).ToString(C); }
        public static string I(int v) { return v.ToString(C); }
        public static int ToInt(string s) { int v; return int.TryParse(s, NumberStyles.Integer, C, out v) ? v : 0; }
        public static float ToF(string s) { return F(ToInt(s)); }
        public static string Clean(string s) { return (s ?? "").Replace('|', '/').Replace(';', ',').Replace('\n', ' '); }
    }

    public struct ZRec { public int id, type, state, hp; public float x, y, z; }
    public struct ShooterRec { public float ax, ay, az; public bool fire; }

    /// What the host sends about the running game about 20 times a second.
    public class Snapshot
    {
        public int tick, resolved, total, kills, coins;
        public float wallHp, wallMax, barrel = 1f, air = 1f;
        public ShooterRec[] shooters = new ShooterRec[3];
        public readonly List<ZRec> zombies = new List<ZRec>();

        public string Encode(StringBuilder sb)
        {
            sb.Length = 0;
            sb.Append("S|").Append(W.I(tick)).Append('|').Append(W.I((int)Math.Round(wallHp * 10f))).Append('|').Append(W.I((int)Math.Round(wallMax * 10f)))
              .Append('|').Append(W.I(resolved)).Append('|').Append(W.I(total)).Append('|').Append(W.I(kills)).Append('|').Append(W.I(coins))
              .Append('|').Append(W.I((int)Math.Round(barrel * 100f))).Append('|').Append(W.I((int)Math.Round(air * 100f))).Append('|');
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) sb.Append(';');
                var s = shooters[i];
                sb.Append(W.N(s.ax)).Append(',').Append(W.N(s.ay)).Append(',').Append(W.N(s.az)).Append(',').Append(s.fire ? '1' : '0');
            }
            sb.Append('|');
            for (int i = 0; i < zombies.Count; i++)
            {
                if (i > 0) sb.Append(';');
                var z = zombies[i];
                sb.Append(W.I(z.id)).Append(',').Append(W.I(z.type)).Append(',').Append(W.N(z.x)).Append(',').Append(W.N(z.y)).Append(',').Append(W.N(z.z))
                  .Append(',').Append(W.I(z.state)).Append(',').Append(W.I(z.hp));
            }
            return sb.ToString();
        }

        public static Snapshot Decode(string msg)
        {
            var f = msg.Split('|');
            if (f.Length < 12 || f[0] != "S") return null;
            var s = new Snapshot
            {
                tick = W.ToInt(f[1]), wallHp = W.ToInt(f[2]) / 10f, wallMax = W.ToInt(f[3]) / 10f,
                resolved = W.ToInt(f[4]), total = W.ToInt(f[5]), kills = W.ToInt(f[6]), coins = W.ToInt(f[7]),
                barrel = W.ToInt(f[8]) / 100f, air = W.ToInt(f[9]) / 100f
            };
            var sh = f[10].Split(';');
            for (int i = 0; i < 3 && i < sh.Length; i++)
            {
                var v = sh[i].Split(',');
                if (v.Length < 4) continue;
                s.shooters[i] = new ShooterRec { ax = W.ToF(v[0]), ay = W.ToF(v[1]), az = W.ToF(v[2]), fire = v[3] == "1" };
            }
            if (f[11].Length > 0)
                foreach (var item in f[11].Split(';'))
                {
                    var v = item.Split(',');
                    if (v.Length < 7) continue;
                    s.zombies.Add(new ZRec { id = W.ToInt(v[0]), type = W.ToInt(v[1]), x = W.ToF(v[2]), y = W.ToF(v[3]), z = W.ToF(v[4]), state = W.ToInt(v[5]), hp = W.ToInt(v[6]) });
                }
            return s;
        }
    }

    /// What a guest tells the host: where the pointer is and whether it is held (about 15 times a second).
    public struct InputMsg
    {
        public int slot; public float ox, oy, oz, dx, dy, dz, ax, ay, az; public bool fire;

        public string Encode()
        {
            return "I|" + W.I(slot) + "|" + W.N(ox) + "|" + W.N(oy) + "|" + W.N(oz) + "|" + W.I((int)Math.Round(dx * 1000f)) + "|" + W.I((int)Math.Round(dy * 1000f)) + "|" + W.I((int)Math.Round(dz * 1000f))
                 + "|" + W.N(ax) + "|" + W.N(ay) + "|" + W.N(az) + "|" + (fire ? "1" : "0");
        }

        public static bool Decode(string msg, out InputMsg m)
        {
            m = default(InputMsg);
            var f = msg.Split('|');
            if (f.Length < 12 || f[0] != "I") return false;
            m.slot = W.ToInt(f[1]); m.ox = W.ToF(f[2]); m.oy = W.ToF(f[3]); m.oz = W.ToF(f[4]);
            m.dx = W.ToInt(f[5]) / 1000f; m.dy = W.ToInt(f[6]) / 1000f; m.dz = W.ToInt(f[7]);
            m.dz /= 1000f;
            m.ax = W.ToF(f[8]); m.ay = W.ToF(f[9]); m.az = W.ToF(f[10]); m.fire = f[11] == "1";
            return true;
        }
    }

    /// A guest's upgrades, sent when they join and at the start of every level, so their shooter on the
    /// host shoots with their own stats.
    public struct StatsMsg
    {
        public int slot, weapon, gunDmg, gunRate, gunBullets, snDmg, snRate, snPierce, barrelPow, barrelReload, airLevel;

        public string Encode()
        {
            return "J|" + W.I(slot) + "|" + W.I(weapon) + "|" + W.I(gunDmg) + "|" + W.I(gunRate) + "|" + W.I(gunBullets) + "|" + W.I(snDmg) + "|" + W.I(snRate) + "|" + W.I(snPierce)
                 + "|" + W.I(barrelPow) + "|" + W.I(barrelReload) + "|" + W.I(airLevel);
        }

        public static bool Decode(string msg, out StatsMsg m)
        {
            m = default(StatsMsg);
            var f = msg.Split('|');
            if (f.Length < 12 || f[0] != "J") return false;
            m.slot = W.ToInt(f[1]); m.weapon = W.ToInt(f[2]); m.gunDmg = W.ToInt(f[3]); m.gunRate = W.ToInt(f[4]); m.gunBullets = W.ToInt(f[5]);
            m.snDmg = W.ToInt(f[6]); m.snRate = W.ToInt(f[7]); m.snPierce = W.ToInt(f[8]); m.barrelPow = W.ToInt(f[9]); m.barrelReload = W.ToInt(f[10]); m.airLevel = W.ToInt(f[11]);
            return true;
        }
    }

    /// One-off things the host tells everybody: "E" + code, then fields.
    public static class Ev
    {
        public static string Make(char code, params string[] fields)
        {
            var sb = new StringBuilder("E").Append(code);
            foreach (var f in fields) sb.Append('|').Append(f);
            return sb.ToString();
        }
    }
}
