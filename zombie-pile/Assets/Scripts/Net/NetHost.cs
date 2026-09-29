using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZombiePile.NetCore;

namespace ZombiePile
{
    /// The host side of a co-op room: runs the game as usual, streams a snapshot 20 times a second, tells the
    /// others about everything that happens (kills, explosions, shots, coins...), and turns their input into
    /// their shooters' aim and fire.
    public class NetHost : MonoBehaviour
    {
        readonly StringBuilder sb = new StringBuilder(4096);
        readonly Snapshot snap = new Snapshot();
        readonly Dictionary<int, PlayerStats> stats = new Dictionary<int, PlayerStats>();
        float snapT, idleT;
        int tick;

        void OnEnable()
        {
            Zombie.KilledHook = OnKill;
            // every human in the room controls their own slot; the AI covers the empty ones
            foreach (var p in Net.Players)
            {
                if (p.slot == Net.LocalSlot || p.slot < 0 || p.slot > 2) continue;
                var s = Shooter.Slots[p.slot];
                s.Ctl = Control.Remote;
                s.Remote = new PlayerStats();
                stats[p.slot] = s.Remote;
            }
            Shooter.SetLocal(Net.LocalSlot);
        }

        void OnDisable() { if (Zombie.KilledHook == (System.Action<Zombie, Vector3, Vector3, bool, Vector3, float>)OnKill) Zombie.KilledHook = null; }

        public void PlayerLeft(int slot)
        {
            if (slot < 0 || slot > 2) return;
            var s = Shooter.Slots[slot];
            s.Ctl = Control.AI; s.Remote = null;
            stats.Remove(slot);
            Game.Say("A PLAYER LEFT", "The AI takes over " + Shooter.Names[slot], 2.5f);
        }

        // ------------------------------------------------------------------ snapshots
        void Update()
        {
            if (Game.I == null) return;
            // keep streaming a few seconds after the level ends (the overrun / results are still playing out)
            if (Game.I.Playing) idleT = 4f; else idleT -= Time.unscaledDeltaTime;
            if (idleT <= 0f) return;
            snapT -= Time.unscaledDeltaTime;
            if (snapT > 0f) return;
            snapT = 0.05f;
            snap.tick = ++tick;
            snap.wallHp = Game.I.WallHp; snap.wallMax = Game.I.WallMax;
            snap.resolved = Level.I.Resolved; snap.total = Level.I.Total;
            snap.kills = Game.I.RunKills; snap.coins = Game.I.RunCoins;
            snap.barrel = Shooter.Wall.BarrelReady; snap.air = Shooter.Wall.AirReady;
            for (int i = 0; i < 3; i++)
            {
                var sh = Shooter.Slots[i];
                snap.shooters[i] = new ShooterRec { ax = sh.AimPoint.x, ay = sh.AimPoint.y, az = sh.AimPoint.z, fire = sh.Firing };
            }
            snap.zombies.Clear();
            foreach (var z in Zombie.All)
            {
                if (z == null || z.Puppet || !z.IsAlive) continue;   // a corpse waiting to be destroyed would come back as a ghost
                var p = z.transform.position;
                int hp = Mathf.Clamp(Mathf.RoundToInt(100f * z.Hp / Mathf.Max(1f, z.MaxHp)), 0, 100) + (z.Armor > 0f ? 200 : 0);
                snap.zombies.Add(new ZRec { id = z.Id, type = ZType.NetCode(z.Type), x = p.x, y = p.y, z = p.z, state = (int)z.State, hp = hp });
            }
            Net.Send(snap.Encode(sb));
        }

        // ------------------------------------------------------------------ what the guests send
        public void OnMessage(string m)
        {
            if (m.Length < 2) return;
            switch (m[0])
            {
                case 'I':
                    if (m[1] == '|')
                    {
                        InputMsg im;
                        if (!InputMsg.Decode(m, out im) || im.slot < 0 || im.slot > 2) return;
                        var sh = Shooter.Slots[im.slot];
                        if (sh.Ctl != Control.Remote) return;
                        sh.SetRemoteInput(new Vector3(im.ox, im.oy, im.oz), new Vector3(im.dx, im.dy, im.dz), new Vector3(im.ax, im.ay, im.az), im.fire);
                    }
                    else if (m[1] == 'B' || m[1] == 'A')
                    {
                        var f = m.Split('|');
                        if (f.Length < 5) return;
                        int slot = W.ToInt(f[1]);
                        var aim = new Vector3(W.ToF(f[2]), W.ToF(f[3]), W.ToF(f[4]));
                        PlayerStats st; stats.TryGetValue(slot, out st);
                        if (m[1] == 'B') Shooter.Wall.ThrowBarrelAt(aim, st); else Shooter.Wall.CallAirstrikeAt(aim, st);
                    }
                    break;
                case 'J':
                    StatsMsg sm;
                    if (!StatsMsg.Decode(m, out sm) || sm.slot < 0 || sm.slot > 2) return;
                    PlayerStats ps;
                    if (!stats.TryGetValue(sm.slot, out ps)) { ps = new PlayerStats(); stats[sm.slot] = ps; }
                    ps.weapon = sm.weapon; ps.gunDmg = sm.gunDmg; ps.gunRate = sm.gunRate; ps.gunBullets = sm.gunBullets;
                    ps.snDmg = sm.snDmg; ps.snRate = sm.snRate; ps.snPierce = sm.snPierce;
                    ps.barrelPow = sm.barrelPow; ps.barrelReload = sm.barrelReload; ps.airLevel = sm.airLevel;
                    var s = Shooter.Slots[sm.slot];
                    if (s.Ctl == Control.Remote) { s.Remote = ps; s.RefreshWeapon(); }
                    break;
            }
        }

        // ------------------------------------------------------------------ events out
        static string N(float v) { return W.N(v); }
        static string V(Vector3 v) { return W.N(v.x) + "|" + W.N(v.y) + "|" + W.N(v.z); }
        static string Col(Color c) { return W.I(Mathf.RoundToInt(c.r * 255f)) + "|" + W.I(Mathf.RoundToInt(c.g * 255f)) + "|" + W.I(Mathf.RoundToInt(c.b * 255f)); }
        void E(char code, string body) { Net.Send("E" + code + "|" + body); }

        void OnKill(Zombie z, Vector3 impulse, Vector3 point, bool head, Vector3 blastAt, float force)
        {
            E('K', W.I(z.Id) + "|" + V(impulse) + "|" + V(point) + "|" + (head ? "1" : "0") + "|" + V(blastAt) + "|" + W.I(Mathf.RoundToInt(force)));
        }

        public void Explosion(Vector3 at, float radius, bool big) { E('X', V(at) + "|" + N(radius) + "|" + (big ? "1" : "0")); }
        public void BarrelThrown(Vector3 from, Vector3 to) { E('B', V(from) + "|" + V(to)); }
        public void Airstrike(Vector3 aim) { E('A', V(aim)); }
        public void ShotFx(int slot, Vector3 from, Vector3 to, bool tower) { E('M', W.I(slot) + "|" + V(from) + "|" + V(to) + "|" + (tower ? "1" : "0")); }
        public void Coins(Vector3 at, int amount) { E('C', V(at) + "|" + W.I(amount)); }
        public void Damage(Vector3 at, int amount, bool crit) { E('D', V(at) + "|" + W.I(amount) + "|" + (crit ? "1" : "0")); }
        public void Hit(int id) { E('H', W.I(id)); }
        public void Popup(Vector3 at, string text, Color c, float size) { E('P', W.Clean(text) + "|" + V(at) + "|" + Col(c) + "|" + W.I(Mathf.RoundToInt(size * 100f))); }
        public void Banner(string title, string sub, float dur) { E('N', W.Clean(title) + "|" + W.Clean(sub) + "|" + W.I(Mathf.RoundToInt(dur * 10f))); }
        public void CrateDrop(Vector3 at) { E('R', V(at)); }
        public void CrateOpen(Vector3 at) { E('O', V(at)); }
        public void Mood(int mood) { E('Y', W.I(mood)); }
        public void Chip(Vector3 at, float d) { E('T', V(at) + "|" + W.I(Mathf.RoundToInt(d * 10f))); }
        public void LevelBegin(int level, bool endless) { E('L', W.I(level) + "|" + (endless ? "1" : "0")); }
        public void WaveBegin(int level) { E('W', W.I(level)); }
        public void LevelClear(int stars, int kills, int bonus, int runCoins) { E('V', W.I(stars) + "|" + W.I(kills) + "|" + W.I(bonus) + "|" + W.I(runCoins)); }
        public void Fail(int level, int coins, bool endless, int best, bool newBest) { E('F', W.I(level) + "|" + W.I(coins) + "|" + (endless ? "1" : "0") + "|" + W.I(best) + "|" + (newBest ? "1" : "0")); }
    }
}
