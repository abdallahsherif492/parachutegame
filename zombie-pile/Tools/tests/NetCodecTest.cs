using System;
using System.Text;
using ZombiePile.NetCore;

class NetCodecTest
{
    static int fails;
    static void Check(bool ok, string what) { if (!ok) { fails++; Console.WriteLine("FAIL: " + what); } }
    static bool Near(float a, float b, float eps = 0.051f) { return Math.Abs(a - b) <= eps; }

    static int Main()
    {
        var rnd = new Random(5);
        var s = new Snapshot { tick = 1234, wallHp = 73.4f, wallMax = 120f, resolved = 31, total = 80, kills = 55, coins = 402, barrel = 0.35f, air = 1f };
        s.shooters[0] = new ShooterRec { ax = -3.3f, ay = 1.2f, az = 14.55f, fire = true };
        s.shooters[2] = new ShooterRec { ax = 8.05f, ay = 0f, az = -0.1f, fire = false };
        for (int i = 0; i < 100; i++) s.zombies.Add(new ZRec { id = 1000 + i, type = i % 7, x = (float)(rnd.NextDouble() * 8 - 4), y = (float)(rnd.NextDouble() * 5), z = (float)(rnd.NextDouble() * 55), state = i % 7, hp = rnd.Next(0, 101) });
        var enc = s.Encode(new StringBuilder());
        Console.WriteLine("snapshot of 100 zombies: " + enc.Length + " chars");
        var d = Snapshot.Decode(enc);
        Check(d != null, "decode returns a snapshot");
        Check(d.tick == 1234 && d.resolved == 31 && d.total == 80 && d.kills == 55 && d.coins == 402, "counters");
        Check(Near(d.wallHp, 73.4f, 0.06f) && Near(d.wallMax, 120f), "wall");
        Check(Near(d.barrel, 0.35f, 0.006f) && Near(d.air, 1f, 0.006f), "cooldowns");
        Check(d.shooters[0].fire && !d.shooters[1].fire && Near(d.shooters[0].ax, -3.3f) && Near(d.shooters[2].ax, 8.05f), "shooters");
        Check(d.zombies.Count == 100, "100 zombies");
        for (int i = 0; i < 100; i++)
        {
            var a = s.zombies[i]; var b = d.zombies[i];
            Check(a.id == b.id && a.type == b.type && a.state == b.state && a.hp == b.hp && Near(a.x, b.x) && Near(a.y, b.y) && Near(a.z, b.z), "zombie " + i);
        }
        s.zombies.Clear();
        var empty = Snapshot.Decode(s.Encode(new StringBuilder()));
        Check(empty != null && empty.zombies.Count == 0, "empty zombie list");
        Check(Snapshot.Decode("garbage") == null && Snapshot.Decode("S|1|2") == null, "garbage is rejected");

        var inp = new InputMsg { slot = 2, ox = 7.5f, oy = 10.8f, oz = 19.6f, dx = -0.5f, dy = -0.61f, dz = -0.6f, ax = 1.5f, ay = 0.4f, az = 3.25f, fire = true };
        InputMsg r;
        Check(InputMsg.Decode(inp.Encode(), out r), "input decodes");
        Check(r.slot == 2 && r.fire && Near(r.ox, 7.5f) && Near(r.oy, 10.8f) && Near(r.oz, 19.6f) && Near(r.ax, 1.5f) && Near(r.az, 3.25f), "input values");
        Check(Near(r.dx, -0.5f, 0.001f) && Near(r.dy, -0.61f, 0.001f) && Near(r.dz, -0.6f, 0.001f), "input direction");
        Check(!InputMsg.Decode("I|1|2", out r), "short input rejected");

        var st = new StatsMsg { slot = 1, weapon = 2, gunDmg = 7, gunRate = 3, gunBullets = 1, snDmg = 4, snRate = 2, snPierce = 1, barrelPow = 5, barrelReload = 6, airLevel = 2 };
        StatsMsg t;
        Check(StatsMsg.Decode(st.Encode(), out t), "stats decode");
        Check(t.slot == 1 && t.weapon == 2 && t.gunDmg == 7 && t.snPierce == 1 && t.barrelReload == 6 && t.airLevel == 2, "stats values");

        Check(Ev.Make('N', W.Clean("HORDE | INCOMING;"), "sub", "2") == "EN|HORDE / INCOMING,|sub|2", "event text is cleaned");
        Check(W.Q(1.02f) == 20 && W.F(21) == 1.05f, "quantise");
        Check(W.ToInt("abc") == 0 && W.ToInt("-12") == -12, "parse");

        Console.WriteLine(fails == 0 ? "codec tests passed" : fails + " codec tests FAILED");
        return fails == 0 ? 0 : 1;
    }
}
