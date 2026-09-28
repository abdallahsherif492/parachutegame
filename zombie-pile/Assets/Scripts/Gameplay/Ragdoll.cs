using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// Turns a dead zombie's own skeleton into a physics ragdoll (rigidbodies, colliders and joints on the
    /// kit bones), so shot zombies slump and fall off the pile and blasts throw them around.
    /// Corpses live on their own layer: bullets ignore them, the invisible walls keep them in the street.
    public static class Ragdoll
    {
        public const int Layer = 9;
        const int MaxCorpses = 26;
        static readonly List<Corpse> corpses = new List<Corpse>();

        class Part { public string bone, end, parent; public float mass, radius; public bool box; }

        // bone, end bone (collider runs from bone to end), parent part, mass, radius (m, before zombie scale)
        static readonly Part[] Parts =
        {
            new Part { bone = "Hips", end = "Abdomen", parent = null, mass = 3f, radius = 0.17f, box = true },
            new Part { bone = "Torso", end = "Neck", parent = "Hips", mass = 3f, radius = 0.17f },
            new Part { bone = "Head", end = null, parent = "Torso", mass = 1.2f, radius = 0.26f },
            new Part { bone = "UpperArm.L", end = "LowerArm.L", parent = "Torso", mass = 0.8f, radius = 0.07f },
            new Part { bone = "LowerArm.L", end = "Middle1.L", parent = "UpperArm.L", mass = 0.6f, radius = 0.07f },
            new Part { bone = "UpperArm.R", end = "LowerArm.R", parent = "Torso", mass = 0.8f, radius = 0.07f },
            new Part { bone = "LowerArm.R", end = "Middle1.R", parent = "UpperArm.R", mass = 0.6f, radius = 0.07f },
            new Part { bone = "UpperLeg.L", end = "LowerLeg.L", parent = "Hips", mass = 1.4f, radius = 0.09f },
            new Part { bone = "LowerLeg.L", end = "Foot.L", parent = "UpperLeg.L", mass = 1f, radius = 0.08f },
            new Part { bone = "UpperLeg.R", end = "LowerLeg.R", parent = "Hips", mass = 1.4f, radius = 0.09f },
            new Part { bone = "LowerLeg.R", end = "Foot.R", parent = "UpperLeg.R", mass = 1f, radius = 0.08f },
        };

        public static void Setup()
        {
            Physics.IgnoreLayerCollision(Layer, Arena.Props, true);
            Physics.IgnoreLayerCollision(Layer, Zombie.Layer, false);
        }

        public static void ClearAll()
        {
            foreach (var c in corpses) if (c != null) Object.Destroy(c.gameObject);
            corpses.Clear();
        }

        /// holder: the kit model holder (detached from the zombie). s: zombie scale.
        public static void Make(GameObject holder, Kit.Anim anim, float s, float headR, bool headless,
                                Vector3 velocity, Vector3 impulse, Vector3 point, Vector3 blastAt, float blastForce)
        {
            if (anim != null && anim.anim != null) { anim.anim.Stop(); anim.anim.enabled = false; }

            var bones = new Dictionary<string, Transform>();
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;

            // the kit rigs keep the feet under the root (IK); hang them on the shins so they follow
            Reparent(bones, "Foot.L", "LowerLeg.L");
            Reparent(bones, "Foot.R", "LowerLeg.R");

            var bodies = new Dictionary<string, Rigidbody>();
            var all = new List<Rigidbody>();
            foreach (var p in Parts)
            {
                Transform b;
                if (!bones.TryGetValue(p.bone, out b)) continue;
                if (p.bone == "Head" && headless) continue;
                Rigidbody parent = null;
                if (p.parent != null && !bodies.TryGetValue(p.parent, out parent))
                {
                    // missing parent (legs-only zombies): hang on the hips if there are any
                    bodies.TryGetValue("Hips", out parent);
                }
                if (p.parent != null && parent == null && bodies.Count > 0) continue;

                float scale = Mathf.Max(0.0001f, Mathf.Abs(b.lossyScale.x));
                float r = p.radius * s;
                if (p.bone == "Head") r = headR * s * 0.9f;
                Vector3 end;
                Transform e;
                if (p.end != null && bones.TryGetValue(p.end, out e)) end = e.position;
                else if (p.bone.StartsWith("LowerArm")) end = b.position + (b.position - bones[p.bone.Replace("Lower", "Upper")].position) * 0.8f;
                else end = b.position + Vector3.up * r;

                var go = b.gameObject;
                go.layer = Layer;
                if (p.bone == "Head")
                {
                    var sc = go.AddComponent<SphereCollider>();
                    sc.radius = r / scale;
                    var neck = bones.ContainsKey("Neck") ? bones["Neck"].position : b.position - Vector3.up * 0.1f;
                    var dir = (b.position - neck).normalized;
                    sc.center = b.InverseTransformPoint(b.position + dir * r * 0.9f);
                }
                else if (p.box)
                {
                    var bc = go.AddComponent<BoxCollider>();
                    var le = b.InverseTransformPoint(end);
                    bc.center = le * 0.5f;
                    bc.size = Vector3.Max(Abs(le) + Vector3.one * (r * 2f / scale) * 0.6f, Vector3.one * (r * 2f / scale));
                }
                else
                {
                    var cc = go.AddComponent<CapsuleCollider>();
                    var le = b.InverseTransformPoint(end);
                    int axis = Abs(le).x > Abs(le).y ? (Abs(le).x > Abs(le).z ? 0 : 2) : (Abs(le).y > Abs(le).z ? 1 : 2);
                    cc.direction = axis;
                    cc.center = le * 0.5f;
                    cc.radius = r / scale;
                    cc.height = Mathf.Max(le.magnitude + cc.radius * 2f, cc.radius * 2.05f);
                }

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = p.mass * s;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.maxDepenetrationVelocity = 3f;
                rb.maxAngularVelocity = 18f;
                rb.solverIterations = 8;
                bodies[p.bone] = rb;
                all.Add(rb);

                if (parent != null)
                {
                    var j = go.AddComponent<CharacterJoint>();
                    j.connectedBody = parent;
                    j.enableProjection = true;
                    // Blender bones run along their local Y: twist around it, swing around X
                    j.axis = Vector3.up;
                    j.swingAxis = Vector3.right;
                    var lo = j.lowTwistLimit; lo.limit = -35f; j.lowTwistLimit = lo;
                    var hi = j.highTwistLimit; hi.limit = 35f; j.highTwistLimit = hi;
                    var s1 = j.swing1Limit; s1.limit = p.bone.StartsWith("Upper") ? 70f : 40f; j.swing1Limit = s1;
                    var s2 = j.swing2Limit; s2.limit = 35f; j.swing2Limit = s2;
                }
            }
            if (all.Count == 0) { Object.Destroy(holder, 3f); return; }

            // hand over the motion: the zombie's own velocity, the bullet push at the hit point, the blast
            Rigidbody nearest = all[0];
            float nd = float.MaxValue;
            foreach (var rb in all)
            {
                rb.AddForce(velocity + Random.insideUnitSphere * 0.4f, ForceMode.VelocityChange);
                float d = (rb.worldCenterOfMass - point).sqrMagnitude;
                if (d < nd) { nd = d; nearest = rb; }
                if (blastForce > 0f) rb.AddExplosionForce(blastForce * rb.mass, blastAt, 6f, 1.2f, ForceMode.Impulse);
            }
            if (impulse.sqrMagnitude > 0f) nearest.AddForceAtPosition(impulse * nearest.mass * 2.5f, point, ForceMode.Impulse);

            var corpse = holder.AddComponent<Corpse>();
            corpse.bodies = all.ToArray();
            corpses.Add(corpse);
            for (int i = corpses.Count - 1; i >= 0; i--) if (corpses[i] == null) corpses.RemoveAt(i);
            int extra = corpses.Count - MaxCorpses;
            for (int i = 0; i < extra; i++) corpses[i].Expire();
        }

        static void Reparent(Dictionary<string, Transform> bones, string child, string parent)
        {
            Transform c, p;
            if (bones.TryGetValue(child, out c) && bones.TryGetValue(parent, out p)) c.SetParent(p, true);
        }

        static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

        public static void Forget(Corpse c) { corpses.Remove(c); }
    }

    /// A ragdoll's life: tumble, come to rest, leave a blood stain, sink into the ground, go away.
    public class Corpse : MonoBehaviour
    {
        public Rigidbody[] bodies;
        float t, sinkT = -1f;
        bool frozen, stained;
        const float Life = 4.5f, MaxSpeed = 15f;

        public void Expire() { if (t < Life) t = Life; }

        void FixedUpdate()
        {
            if (frozen || bodies == null) return;
            // blasts can be violent: cap the speed so nothing is thrown out of the street
            foreach (var rb in bodies)
            {
                if (rb == null) continue;
                var v = rb.GetPointVelocity(rb.worldCenterOfMass);
                float m = v.magnitude;
                if (m > MaxSpeed) rb.AddForce(v * (MaxSpeed / m - 1f), ForceMode.VelocityChange);
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            if (!stained && t > 1.2f && bodies.Length > 0 && bodies[0] != null && bodies[0].position.y < 0.7f)
            {
                stained = true;
                Fx.I.BloodStain(bodies[0].position);
            }
            if (t >= Life && !frozen)
            {
                frozen = true;
                foreach (var rb in bodies) if (rb != null) { rb.isKinematic = true; rb.detectCollisions = false; }
                sinkT = 0f;
            }
            if (sinkT >= 0f)
            {
                sinkT += Time.deltaTime;
                if (sinkT > 0.6f) transform.position += Vector3.down * Time.deltaTime * 0.9f;
                if (sinkT > 2.4f) { Ragdoll.Forget(this); Destroy(gameObject); }
            }
        }
    }
}
