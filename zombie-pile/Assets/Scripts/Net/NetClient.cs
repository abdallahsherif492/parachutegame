using UnityEngine;
using ZombiePile.NetCore;

namespace ZombiePile
{
    /// The guest side of a co-op room: sends this player's aim and buttons to the host and draws what the
    /// host says: zombies are puppets moved by snapshots, everything else arrives as events.
    public class NetClient : MonoBehaviour
    {
        float inputT;
        readonly System.Collections.Generic.HashSet<int> seen = new System.Collections.Generic.HashSet<int>();
        readonly System.Collections.Generic.List<int> gone = new System.Collections.Generic.List<int>();
        float lastWall = -1f;

        void Start() { SendStats(); }

        /// Our upgrades, so the host's copy of our shooter shoots like ours (sent at the start and every level).
        public void SendStats()
        {
            var d = Save.Data;
            var m = new StatsMsg
            {
                slot = Net.LocalSlot, weapon = d.weapon,
                gunDmg = Econ.Lvl(Up.GunDamage), gunRate = Econ.Lvl(Up.GunRate), gunBullets = Econ.Lvl(Up.GunBullets),
                snDmg = Econ.Lvl(Up.SniperDamage), snRate = Econ.Lvl(Up.SniperRate), snPierce = Econ.Lvl(Up.SniperPierce),
                barrelPow = Econ.Lvl(Up.BarrelPower), barrelReload = Econ.Lvl(Up.BarrelReload), airLevel = Econ.Lvl(Up.Airstrike)
            };
            Net.Send(m.Encode());
        }

        public void PressBarrel() { Press('B'); }
        public void PressAirstrike() { Press('A'); }

        void Press(char kind)
        {
            var a = Shooter.Slots[Net.LocalSlot].AimPoint;
            Net.Send("I" + kind + "|" + W.I(Net.LocalSlot) + "|" + W.N(a.x) + "|" + W.N(a.y) + "|" + W.N(a.z));
        }

        void Update()
        {
            if (Game.I == null || !Game.I.Playing) return;
            inputT -= Time.unscaledDeltaTime;
            if (inputT > 0f) return;
            inputT = 1f / 15f;
            var sh = Shooter.Slots[Net.LocalSlot];
            var r = sh.LastRay;
            var msg = new InputMsg
            {
                slot = Net.LocalSlot, ox = r.origin.x, oy = r.origin.y, oz = r.origin.z, dx = r.direction.x, dy = r.direction.y, dz = r.direction.z,
                ax = sh.AimPoint.x, ay = sh.AimPoint.y, az = sh.AimPoint.z, fire = sh.Firing
            };
            Net.Send(msg.Encode());
        }

        // ------------------------------------------------------------------ from the host
        public void OnMessage(string m)
        {
            if (m[0] == 'S') { ApplySnapshot(m); return; }
            if (m[0] == 'E' && m.Length > 2) Event(m[1], m.Split('|'));
        }

        void ApplySnapshot(string m)
        {
            var s = Snapshot.Decode(m);
            if (s == null) return;
            var g = Game.I;
            if (g == null) return;
            g.NetWall(s.wallHp, s.wallMax, s.kills, s.coins);
            if (lastWall >= 0f && s.wallHp < lastWall - 0.4f && s.wallHp > 0f) Hud.I.FlashDamage();
            lastWall = s.wallHp;
            Hud.I.SetProgress(s.resolved, s.total);
            Shooter.Wall.SetNetCooldowns(s.barrel, s.air);
            for (int i = 0; i < 3; i++)
                if (i != Net.LocalSlot && Shooter.Slots[i] != null) Shooter.Slots[i].SetNetAim(new Vector3(s.shooters[i].ax, s.shooters[i].ay, s.shooters[i].az), s.shooters[i].fire);

            seen.Clear();
            foreach (var r in s.zombies)
            {
                seen.Add(r.id);
                Zombie z;
                var pos = new Vector3(r.x, r.y, r.z);
                if (!Zombie.ById.TryGetValue(r.id, out z))
                {
                    z = Zombie.SpawnPuppet(r.id, ZType.FromNet(r.type), pos);
                }
                z.PuppetSet(pos, (ZState)Mathf.Clamp(r.state, 0, 6), (r.hp % 200) / 100f, r.hp >= 200);
            }
            gone.Clear();
            foreach (var kv in Zombie.ById) if (!seen.Contains(kv.Key) && kv.Value != null && kv.Value.Puppet && kv.Value.IsAlive) gone.Add(kv.Key);
            foreach (var id in gone) { Zombie z; if (Zombie.ById.TryGetValue(id, out z) && z != null) Destroy(z.gameObject); Zombie.ById.Remove(id); }
        }

        static Vector3 V(string[] f, int i) { return new Vector3(W.ToF(f[i]), W.ToF(f[i + 1]), W.ToF(f[i + 2])); }
        static Color Col(string[] f, int i) { return new Color(W.ToInt(f[i]) / 255f, W.ToInt(f[i + 1]) / 255f, W.ToInt(f[i + 2]) / 255f); }

        void Event(char code, string[] f)
        {
            var g = Game.I;
            switch (code)
            {
                case 'K':   // id, impulse, point, head, blastAt, force
                {
                    Zombie z;
                    if (f.Length < 13 || !Zombie.ById.TryGetValue(W.ToInt(f[1]), out z) || z == null) return;
                    z.PuppetDie(V(f, 2), V(f, 5), f[8] == "1", V(f, 9), W.ToInt(f[12]));
                    break;
                }
                case 'H': { Zombie z; if (f.Length > 1 && Zombie.ById.TryGetValue(W.ToInt(f[1]), out z) && z != null) z.PuppetHit(); break; }
                case 'X': if (f.Length >= 6) Boom.Fx_(V(f, 1), W.ToF(f[4]), f[5] == "1"); break;
                case 'B': if (f.Length >= 7) { var from = V(f, 1); ThrownBarrel.Throw(from, V(f, 4), 0f, 0f, false); SoundBank.I.Play(SoundBank.I.throwS, 0.5f); } break;
                case 'A': Airstrike.CallFxOnly(); break;
                case 'Q': if (f.Length >= 8) Toys.Launch((Toy)Mathf.Clamp(W.ToInt(f[1]), 0, 7), V(f, 2), V(f, 5), false); break;
                case 'M':   // another player's shot: muzzle flash, tracer, sound (ours we already showed)
                {
                    if (f.Length < 9) return;
                    int slot = W.ToInt(f[1]);
                    if (slot == Net.LocalSlot) return;
                    bool tower = f[8] == "1";
                    Fx.I.MuzzleFlash(V(f, 2), tower ? 0.7f : 0.45f);
                    Fx.I.Tracer(V(f, 2), V(f, 5), tower);
                    SoundBank.I.Play(tower ? SoundBank.I.sniper : SoundBank.I.shot, tower ? 0.3f : 0.16f, Random.Range(0.9f, 1.1f));
                    break;
                }
                case 'C': if (f.Length >= 5) g.AddCoinsClient(W.ToInt(f[4]), V(f, 1)); break;
                case 'D': if (f.Length >= 6) Hud.I.Damage(V(f, 1), W.ToInt(f[4]), f[5] == "1"); break;
                case 'P': if (f.Length >= 10) Hud.I.Popup(V(f, 2), f[1], Col(f, 5), W.ToInt(f[8]) / 100f); break;
                case 'N': if (f.Length >= 4) Hud.I.Banner(f[1], f[2], W.ToInt(f[3]) / 10f); break;
                case 'R': if (f.Length >= 4) Crate.Drop(V(f, 1), true); break;
                case 'O': foreach (var c in FindObjectsByType<Crate>(FindObjectsSortMode.None)) c.OpenVisual(); break;
                case 'Y': if (f.Length >= 2) Survivors.I.Apply(W.ToInt(f[1])); break;
                case 'T':
                    if (f.Length >= 5)
                    {
                        var at = V(f, 1);
                        Fx.I.Dust(new Vector3(at.x, Mathf.Min(at.y, Arena.WallHeight - 1f), Arena.WallFront + 0.3f), 0.7f);
                        float d = W.ToInt(f[4]) / 10f;
                        SoundBank.I.Play(SoundBank.I.smash, Mathf.Clamp(0.12f + d * 0.08f, 0.1f, 0.4f), Random.Range(0.8f, 1.1f));
                        CameraRig.I.Shake(Mathf.Clamp(d * 0.04f, 0.02f, 0.25f));
                        Hud.I.ChipFlash(Mathf.Clamp01(d / 3f));
                    }
                    break;
                case 'L': if (f.Length >= 3) { g.ClientBegin(W.ToInt(f[1]), f[2] == "1"); SendStats(); } break;
                case 'W': if (f.Length >= 2) g.ClientWave(W.ToInt(f[1])); break;
                case 'V': if (f.Length >= 5) g.ClientLevelClear(W.ToInt(f[1]), W.ToInt(f[2]), W.ToInt(f[3]), W.ToInt(f[4])); break;
                case 'F': if (f.Length >= 6) g.ClientLose(W.ToInt(f[1]), W.ToInt(f[2]), f[3] == "1", W.ToInt(f[4]), f[5] == "1"); break;
            }
        }
    }
}
