using UnityEditor;

namespace ZombiePile.EditorTools
{
    /// Any model dropped into Assets/Resources/Zombies/ (e.g. Quaternius' free CC0 zombie packs)
    /// is imported with Legacy animation so the game can play its clips by name without an Animator setup.
    public class ZombieModelImport : AssetPostprocessor
    {
        bool InFolder { get { return assetPath.Replace('\\', '/').Contains("/Resources/Zombies/"); } }

        void OnPreprocessModel()
        {
            if (!InFolder) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.importAnimation = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.globalScale = 1f;
        }

        void OnPreprocessAnimation()
        {
            if (!InFolder) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var c in clips)
            {
                string n = c.name.ToLowerInvariant();
                bool once = n.Contains("death") || n.Contains("die") || n.Contains("dead") || n.Contains("hit") || n.Contains("attack") || n.Contains("punch");
                c.loopTime = !once;
                c.wrapMode = once ? UnityEngine.WrapMode.ClampForever : UnityEngine.WrapMode.Loop;
            }
            mi.clipAnimations = clips;
        }
    }
}
