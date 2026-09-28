using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    public enum ZState { Run, Climb, Pile, Leap, Knock, Breach }

    /// A zombie, driven by script while alive (runs up the street, climbs the pile, clings to it, goes over
    /// the wall) and by physics once dead (ragdoll). Living zombies are kinematic: no physics pile-ups,
    /// nothing gets launched into the sky, and the heap always builds against the wall.
    public class Zombie : MonoBehaviour
    {
        public const int Layer = 8;
        public const float BaseSpeed = 3.8f;
        public static readonly List<Zombie> Alive = new List<Zombie>();

        public ZType Type { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public float Armor { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsBoss { get { return Type.ability == Ability.Boss; } }
        public bool IsBrute { get { return Type.ability == Ability.Smash || IsBoss; } }
        public ZState State { get; private set; }
        public Collider HeadCollider { get { return headCol; } }
        public Transform Head { get { return head; } }
        public float Step { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 Center { get { return transform.position + Vector3.up * bodyH * 0.55f; } }
        public bool Airborne { get { return State == ZState.Leap || State == ZState.Knock; } }

        GameObject holder;
        Transform model, head, headBone, cone;
        Kit.Anim kit;
        Renderer[] skins;
        MaterialPropertyBlock mpb;
        CapsuleCollider bodyCol;
        SphereCollider headCol;
        float s, bodyH, speed, laneX, flash, punch, groanT, smashT, stateT, pileY, pileYShown;
        int col = -1;
        Vector3 from, spot, knockV;
        float dur;
        bool leapt, pileBottom;

        public static Zombie Spawn(ZType type, Vector3 pos, float hpScale, float speedScale)
        {
            var go = new GameObject(type.name);
            go.layer = Layer;
            go.transform.position = pos;
            var z = go.AddComponent<Zombie>();
            z.Init(type, hpScale, speedScale);
            return z;
        }

        void Init(ZType t, float hpScale, float speedScale)
        {
            Type = t;
            s = t.scale * (IsBrute ? 1f : Random.Range(0.94f, 1.08f));
            MaxHp = Hp = t.hp * hpScale;
            if (t.ability == Ability.Cone) Armor = t.hp * 1.6f * hpScale;
            speed = BaseSpeed * t.speed * speedScale * (IsBrute ? 1f : Random.Range(0.86f, 1.14f));
            laneX = Random.Range(-Arena.HalfWidth + 0.6f, Arena.HalfWidth - 0.6f);
            Step = t.step * s / t.scale;
            groanT = Random.Range(1f, 8f);
            bodyH = t.neck * s;

            // kinematic body: bullets and ragdolls hit it, nothing pushes it around
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            bodyCol = gameObject.AddComponent<CapsuleCollider>();
            bodyCol.radius = (IsBrute ? 0.42f : 0.3f) * s;
            bodyCol.height = Mathf.Max(bodyH + 0.15f * s, bodyCol.radius * 2.1f);
            bodyCol.center = new Vector3(0f, bodyCol.height * 0.5f, 0f);

            holder = Kit.Place(t.model, transform.position, 180f, s, transform, true);
            if (holder == null) holder = Kit.Place("Zombie_Basic", transform.position, 180f, s, transform, true);   // model missing: stand-in
            model = holder.transform;
            kit = Kit.Anim.From(holder);
            // clinging to the pile loops these (the importer marks them play-once)
            if (kit.anim != null && kit.punch != null) kit.anim[kit.punch].wrapMode = WrapMode.Loop;
            if (kit.anim != null && kit.hit != null) kit.anim[kit.hit].wrapMode = WrapMode.Once;
            kit.Start(kit.run);
            foreach (var tr in holder.GetComponentsInChildren<Transform>()) if (tr.name == "Head") { headBone = tr; break; }
            skins = holder.GetComponentsInChildren<Renderer>();
            mpb = new MaterialPropertyBlock();
            ApplyTint(0f);

            // head sphere (the model faces -Z here, so "forward" in model space is -z); a bit generous
            var hgo = new GameObject("HeadHit");
            hgo.layer = Layer;
            hgo.transform.SetParent(transform, false);
            hgo.transform.localPosition = new Vector3(0f, t.headY * s, -t.headZ * s);
            head = hgo.transform;
            if (t.hasHead)
            {
                headCol = hgo.AddComponent<SphereCollider>();
                headCol.radius = t.headR * s * 1.15f;
            }
            if (t.ability == Ability.Cone && headBone != null) AttachCone();

            IsAlive = true;
            State = ZState.Run;
            Alive.Add(this);
        }

        void AttachCone()
        {
            var c = Kit.Place("TrafficCone_1", headBone.position, 0f, 0.9f * s, null, true);
            if (c == null) return;
            cone = c.transform;
            cone.SetParent(headBone, true);
            cone.position = headBone.position + Vector3.up * 0.42f * s;
        }

        void ApplyTint(float f)
        {
            var c = Color.Lerp(Type.tint, new Color(2.6f, 2.3f, 2.3f), f);
            if (Type.ability == Ability.Explode) c *= 1f + 0.25f * Mathf.Sin(Time.time * 9f);
            foreach (var r in skins)
            {
                if (r == null || (cone != null && r.transform.IsChildOf(cone))) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", c);
                r.SetPropertyBlock(mpb);
            }
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Time.deltaTime;
            stateT += dt;
            var before = transform.position;
            switch (State)
            {
                case ZState.Run: Run(dt); break;
                case ZState.Climb: Climb(dt); break;
                case ZState.Pile: Cling(dt); break;
                case ZState.Leap: Leap(dt); break;
                case ZState.Knock: Knocked(dt); break;
                case ZState.Breach: Breach(dt); break;
            }
            if (this == null || !gameObject.activeSelf) return;
            if (dt > 0f) Velocity = (transform.position - before) / dt;

            if (flash > 0f || Type.ability == Ability.Explode)
            {
                flash = Mathf.Max(0f, flash - dt * 9f);
                ApplyTint(flash);
            }
            if (punch > 0f)
            {
                punch = Mathf.Max(0f, punch - dt * 7f);
                model.localScale = Vector3.one * s * (1f + punch * 0.07f);
            }
            groanT -= dt;
            if (groanT < 0f) { groanT = Random.Range(5f, 14f); if (Random.value < 0.3f) SoundBank.I.Groan(); }
        }

        void Run(float dt)
        {
            var p = transform.position;
            // spread out: keep a little room from the zombies around (mostly sideways, like a crowd)
            float push = 0f;
            for (int i = 0; i < Alive.Count; i++)
            {
                var o = Alive[i];
                if (o == this || o.State != ZState.Run) continue;
                var q = o.transform.position;
                float dz = q.z - p.z;
                if (dz > 0.7f || dz < -0.7f) continue;
                float dx = p.x - q.x;
                float r = 0.32f * (s + o.s);
                if (dx * dx + dz * dz < r * r) push += dx >= 0f ? 1f : -1f;
            }
            laneX = Mathf.Clamp(laneX + push * dt * 0.8f, -Arena.HalfWidth + 0.5f, Arena.HalfWidth - 0.5f);
            p.x = Mathf.MoveTowards(p.x, laneX, dt * 1.6f);
            p.z -= speed * dt;
            p.y = Mathf.MoveTowards(p.y, 0f, dt * 6f);
            transform.position = p;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.identity, dt * 6f);
            kit.Play(kit.run, 0.2f, Mathf.Clamp(speed / BaseSpeed, 0.8f, 1.7f));

            int c = Pile.ColAt(p.x);
            // leapers jump onto the heap from a distance once there is something to land on
            if (Type.ability == Ability.Leap && !leapt && Pile.MaxHeight > 0.9f && p.z < Pile.FrontZ(c) + 7f) { StartLeap(); return; }
            // the boss parks against the middle of the wall and becomes a ramp
            if (IsBoss) { if (p.z <= Arena.WallFront + 1.6f) SettleBoss(); return; }
            if (p.z <= Pile.FrontZ(c) + 0.6f) StartClimb(Pile.Choose(c));
        }

        Vector3 SpotFor(int c, float y)
        {
            float depth = Mathf.Max(0f, 1.3f - y) * 0.3f;
            return new Vector3(Pile.X(c) + Random.Range(-0.12f, 0.12f), y, Arena.WallFront + 0.28f * s + depth + Random.Range(0f, 0.15f));
        }

        void StartClimb(int c)
        {
            col = c;
            from = transform.position;
            spot = SpotFor(c, Pile.Height(c));
            dur = 0.3f + 0.2f * spot.y + Random.Range(0f, 0.15f);
            stateT = 0f;
            State = ZState.Climb;
            kit.Play(kit.climb ?? kit.run, 0.1f, 1.3f);
        }

        void Climb(float dt)
        {
            float k = Mathf.Clamp01(stateT / dur);
            // the target moves with the heap (others cling on or get shot off while this one climbs)
            spot.y = Mathf.MoveTowards(spot.y, Pile.Height(col), dt * 4f);
            var p = Vector3.Lerp(from, spot, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.35f;
            transform.position = p;
            if (k >= 1f) Settle();
        }

        void Settle()
        {
            pileY = Pile.Add(this, col);
            pileYShown = transform.position.y;
            spot.y = pileY;
            State = ZState.Pile;
            stateT = 0f;
            smashT = Random.Range(0.5f, 1.5f);
            transform.rotation = Quaternion.Euler(Random.Range(-12f, 4f), Random.Range(-25f, 25f), Random.Range(-10f, 10f));
            // at the top of the wall: over it goes
            if (pileY >= Arena.WallHeight - 1.1f) { StartBreach(); return; }
            PileAnim(pileY < 0.3f);
        }

        void PileAnim(bool bottom)
        {
            pileBottom = bottom;
            // the ones at the bottom pound on the wall, the rest scramble on top of each other
            if (bottom && kit.punch != null) kit.Play(kit.punch, 0.2f, Random.Range(0.8f, 1.1f));
            else kit.Play(kit.climb ?? kit.run, 0.2f, Random.Range(0.5f, 0.8f));
        }

        /// Called by the pile when zombies below this one were shot away.
        public void SetPileY(float y, bool bottom)
        {
            pileY = y;
            if (State != ZState.Pile) return;
            if (bottom != pileBottom) PileAnim(bottom);
        }

        void Cling(float dt)
        {
            pileYShown = Mathf.MoveTowards(pileYShown, pileY, dt * (pileYShown > pileY ? 7f : 3f));
            transform.position = new Vector3(spot.x, pileYShown, spot.z);
            if (IsBrute && pileY < 0.5f)
            {
                smashT -= dt;
                if (smashT <= 0f)
                {
                    smashT = IsBoss ? 2.5f : 1.6f;
                    if (Game.I != null) Game.I.WallHit(IsBoss ? 12f : 4f, transform.position + Vector3.up * bodyH, IsBoss);
                }
            }
        }

        void SettleBoss()
        {
            col = -1;
            spot = new Vector3(transform.position.x, 0f, Arena.WallFront + 1.1f);
            pileY = 0f; pileYShown = 0f;
            State = ZState.Pile;
            smashT = 1f;
            kit.Play(kit.punch ?? kit.run, 0.2f, 0.7f);
            // the horde climbs up its back: a ramp under the middle columns
            int c = Pile.ColAt(transform.position.x);
            Pile.SetBase(c - 2, c + 2, Step);
        }

        void StartLeap()
        {
            leapt = true;
            var top = Pile.Highest();
            col = top != null ? Pile.ColAt(top.transform.position.x) : Pile.ColAt(transform.position.x);
            from = transform.position;
            spot = SpotFor(col, Pile.Height(col));
            dur = 0.9f;
            stateT = 0f;
            State = ZState.Leap;
            kit.Play(kit.jump ?? kit.climb, 0.08f, 1f);
        }

        void Leap(float dt)
        {
            float k = Mathf.Clamp01(stateT / dur);
            spot.y = Pile.Height(col);
            float y0 = Mathf.Lerp(from.y, spot.y, k);
            float apex = Mathf.Max(from.y, spot.y) + 2.4f;
            var p = Vector3.Lerp(from, spot, k);
            p.y = y0 + (apex - y0) * 4f * k * (1f - k);
            transform.position = p;
            if (k >= 1f) Settle();
        }

        void Knocked(float dt)
        {
            knockV.y -= 22f * dt;
            var p = transform.position + knockV * dt;
            p.z = Mathf.Max(p.z, Arena.WallFront + 0.4f);
            p.x = Mathf.Clamp(p.x, -Arena.HalfWidth + 0.4f, Arena.HalfWidth - 0.4f);
            if (p.y <= 0f && knockV.y < 0f)
            {
                p.y = 0f;
                knockV = Vector3.zero;
                if (stateT > 0.3f)
                {
                    State = ZState.Run;
                    stateT = 0f;
                    kit.Play(kit.hit ?? kit.run, 0.05f);
                }
            }
            transform.position = p;
        }

        void Dislodge(Vector3 v)
        {
            if (State == ZState.Pile && col >= 0) Pile.Remove(this, col);
            col = -1;
            knockV = v;
            stateT = 0f;
            State = ZState.Knock;
            kit.Play(kit.climb ?? kit.run, 0.05f, 1.5f);
        }

        void StartBreach()
        {
            if (col >= 0) Pile.Remove(this, col);
            col = -1;
            IsAlive = false;
            Alive.Remove(this);
            if (bodyCol != null) bodyCol.enabled = false;
            if (headCol != null) headCol.enabled = false;
            from = transform.position;
            stateT = 0f;
            State = ZState.Breach;
            kit.Play(kit.climb ?? kit.run, 0.05f, 1.6f);
        }

        void Breach(float dt)
        {
            // pull up onto the parapet, vault over it and vanish behind it
            var top = new Vector3(from.x, Arena.WallHeight + 0.3f, Arena.WallFront + 0.05f);
            var over = new Vector3(from.x, Arena.WallHeight + 0.6f, Arena.WallFront - 0.7f);
            if (stateT < 0.45f) transform.position = Vector3.Lerp(from, top, Mathf.SmoothStep(0f, 1f, stateT / 0.45f));
            else
            {
                float k = Mathf.Clamp01((stateT - 0.45f) / 0.25f);
                transform.position = Vector3.Lerp(top, over, k);
                transform.localScale = Vector3.one * Mathf.Max(0.01f, 1f - k);
                if (k >= 1f)
                {
                    if (Game.I != null) Game.I.OnBreach(this);
                    Fx.I.Dust(over, 1f);
                    Destroy(gameObject);
                }
            }
        }

        // ------------------------------------------------------------------ damage
        /// A bullet hit. 'dmg' already includes any headshot bonus. Returns true if it killed.
        public bool Hit(float dmg, Vector3 dir, Vector3 point, bool headshot, float kick)
        {
            if (!IsAlive) return false;
            flash = 1f;
            punch = 1f;
            if (Armor > 0f)
            {
                Armor -= dmg;
                Fx.I.Sparks(point);
                if (Armor <= 0f) PopCone(dir);
                return false;
            }
            Hp -= dmg;
            Fx.I.Blood(point, -dir, headshot ? 10 : 5);
            if (State == ZState.Run && kick > 0f) transform.position += new Vector3(dir.x, 0f, Mathf.Max(0f, dir.z)) * kick;
            if (Hp > 0f) return false;
            Die(dir * (headshot ? 3f : 2f) * (1f + kick * 4f), point, headshot, Vector3.zero, 0f);
            return true;
        }

        /// Explosions: damage with falloff; survivors are thrown off the pile or knocked back.
        public bool Blast(Vector3 center, float radius, float dmg, float force)
        {
            if (!IsAlive) return false;
            var d = Center - center;
            float k = 1f - Mathf.Clamp01(d.magnitude / radius);
            if (Armor > 0f) { Armor = 0f; PopCone(d.normalized + Vector3.up); }
            Hp -= dmg * (0.35f + 0.65f * k);
            flash = 1f;
            if (Hp <= 0f)
            {
                Die(Vector3.zero, center, false, center, force * (0.6f + 0.6f * k));
                return true;
            }
            if (IsBoss || k < 0.2f) return false;
            var away = new Vector3(d.x, 0f, Mathf.Abs(d.z) + 0.5f).normalized;
            float m = IsBrute ? 0.3f : 1f;
            Dislodge(away * (3f + 5f * k) * m + Vector3.up * (4f + 4f * k) * m);
            return false;
        }

        void PopCone(Vector3 dir)
        {
            if (cone == null) return;
            cone.SetParent(null, true);
            var rb = cone.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            float sc = Mathf.Max(0.001f, cone.lossyScale.x);
            var bc = cone.gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.5f, 0.6f, 0.5f) / sc;
            bc.center = new Vector3(0f, 0.3f, 0f) / sc;
            cone.gameObject.layer = Ragdoll.Layer;
            rb.AddForce((dir.normalized + Vector3.up * 1.4f) * 5f, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
            Destroy(cone.gameObject, 4f);
            cone = null;
            SoundBank.I.Play(SoundBank.I.clang, 0.6f, Random.Range(0.9f, 1.1f));
        }

        void Die(Vector3 impulse, Vector3 point, bool headshot, Vector3 blastAt, float blastForce)
        {
            if (!IsAlive) return;
            IsAlive = false;
            Alive.Remove(this);
            if (col >= 0) Pile.Remove(this, col);
            col = -1;
            if (IsBoss) Pile.SetBase(0, Pile.Cols - 1, 0f);
            if (Game.I != null) Game.I.OnKill(this, point, headshot);
            if (cone != null) PopCone(impulse + Vector3.up);
            if (headshot && headBone != null)
            {
                headBone.localScale = Vector3.one * 0.01f;
                Fx.I.Blood(head.position, Vector3.up, 22);
            }
            if (Type.ability == Ability.Explode && Game.I != null)
            {
                var at = Center;
                Game.I.Delay(0.08f, () => Boom.Explode(at, 3.4f, 80f, 11f, false));
            }
            // the model becomes a ragdoll on its own; this object (colliders, script) goes away
            flash = 0f;
            ApplyTint(0f);
            model.localScale = Vector3.one * s;
            model.SetParent(null, true);
            Ragdoll.Make(holder, kit, s, Type.headR, headshot || !Type.hasHead, Velocity * 0.6f, impulse, point, blastAt, blastForce);
            Destroy(gameObject);
        }

        /// Clears everything (new level).
        public static void KillAll()
        {
            foreach (var z in FindObjectsByType<Zombie>(FindObjectsSortMode.None)) Destroy(z.gameObject);
            Alive.Clear();
            Pile.Clear();
            Ragdoll.ClearAll();
        }

        void OnDestroy() { Alive.Remove(this); }
    }
}
