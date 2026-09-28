using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SkyDrop
{
    /// Optional imported character (e.g. from Mixamo) that replaces the procedural body.
    ///
    /// Drop FBX files into Assets/Resources/SkyDrop/Character/ :
    ///   Jumper.fbx     the character (with skin)
    ///   Idle.fbx       standing at the plane door        (loop)
    ///   Freefall.fbx   falling / skydiving pose           (loop)
    ///   Canopy.fbx     hanging under the parachute        (loop)
    ///   Land.fbx       touching down                      (once)
    ///   Celebrate.fbx  victory / cheering                 (loop)
    ///   Tumble.fbx     flailing after a hit (optional)    (loop)
    /// The editor importer (SkyDropModelImport) sets them to Humanoid and names the clips.
    /// Clips are blended with the Playables API, so no Animator Controller is needed.
    public class CharacterModel : MonoBehaviour
    {
        public const string Folder = "SkyDrop/Character";
        static readonly string[] Names = { "Idle", "Freefall", "Canopy", "Land", "Celebrate", "Tumble" };
        const int Idle = 0, Freefall = 1, Canopy = 2, Land = 3, Celebrate = 4, TumbleClip = 5;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[Names.Length];
        readonly AnimationClip[] clips = new AnimationClip[Names.Length];
        readonly float[] weights = new float[Names.Length];
        int current = -1;
        float stateTime;

        public static CharacterModel TryCreate(Transform parent)
        {
            var prefab = Resources.Load<GameObject>(Folder + "/Jumper");
            if (prefab == null) return null;
            var all = Resources.LoadAll<AnimationClip>(Folder);

            var go = Instantiate(prefab, parent);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Normalize to ~1.8 m tall with the feet under the hips.
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                if (b.size.y > 0.01f) go.transform.localScale *= 1.8f / b.size.y;
                foreach (var r in renderers)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
            go.transform.localPosition = new Vector3(0f, -0.95f, 0f);

            var anim = go.GetComponentInChildren<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var cm = go.AddComponent<CharacterModel>();
            cm.Init(anim, all);
            return cm;
        }

        static AnimationClip Find(AnimationClip[] all, string name)
        {
            foreach (var c in all)
                if (c != null && !c.name.StartsWith("__preview__") && string.Equals(c.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return c;
            return null;
        }

        void Init(Animator anim, AnimationClip[] all)
        {
            graph = PlayableGraph.Create("Jumper");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", anim);
            mixer = AnimationMixerPlayable.Create(graph, Names.Length);
            output.SetSourcePlayable(mixer);
            for (int i = 0; i < Names.Length; i++)
            {
                clips[i] = Find(all, Names[i]);
                if (clips[i] == null) continue;
                playables[i] = AnimationClipPlayable.Create(graph, clips[i]);
                graph.Connect(playables[i], 0, mixer, i);
            }
            graph.Play();
        }

        bool Has(int i) { return clips[i] != null; }

        /// mode: 0 idle, 1 free fall, 2 canopy, 3 landed, 4 tumbling
        public void Play(int mode, float speed01, float dt)
        {
            int target;
            switch (mode)
            {
                case 1: target = Freefall; break;
                case 2: target = Canopy; break;
                case 3:
                    target = current == Land && stateTime < (clips[Land] != null ? clips[Land].length : 0f) ? Land
                           : current == Land || current == Celebrate ? Celebrate : Land;
                    break;
                case 4: target = TumbleClip; break;
                default: target = Idle; break;
            }
            // fallbacks for missing clips
            if (!Has(target) && target == TumbleClip) target = Freefall;
            if (!Has(target) && target == Land) target = Celebrate;
            if (!Has(target) && target == Celebrate) target = Idle;
            if (!Has(target) && target == Canopy) target = Idle;
            if (!Has(target) && target == Freefall) target = Idle;
            if (!Has(target)) return;

            if (target != current)
            {
                current = target;
                stateTime = 0f;
                playables[target].SetTime(0);
            }
            stateTime += dt;
            playables[target].SetSpeed(target == Freefall ? 0.8f + speed01 * 0.6f : target == TumbleClip ? 1.6f : 1f);

            float total = 0f;
            for (int i = 0; i < Names.Length; i++)
            {
                weights[i] = Mathf.MoveTowards(weights[i], i == current ? 1f : 0f, dt * 5f);
                total += weights[i];
            }
            for (int i = 0; i < Names.Length; i++)
                if (Has(i)) mixer.SetInputWeight(i, total > 0f ? weights[i] / total : 0f);
        }

        /// Crash: keep the model (flailing) but hand it to the debris system.
        public Transform Detach(Transform newParent)
        {
            Play(4, 1f, 0f);
            transform.SetParent(newParent, true);
            return transform;
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
