using UnityEditor;
using UnityEngine;

namespace ZombiePile.EditorTools
{
    /// Import settings for the Quaternius "Zombie Apocalypse Kit" models in Assets/Resources/ZombieKit/:
    /// characters get Legacy animation (clips played by name, no Animator setup), props get none.
    public class KitImport : AssetPostprocessor
    {
        string P { get { return assetPath.Replace('\\', '/'); } }
        bool InKit { get { return P.Contains("/Resources/ZombieKit/"); } }
        bool IsCharacter { get { return P.Contains("/Resources/ZombieKit/Characters/"); } }

        void OnPreprocessModel()
        {
            if (!InKit) return;
            var mi = (ModelImporter)assetImporter;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
            if (IsCharacter)
            {
                mi.animationType = ModelImporterAnimationType.Legacy;
                mi.importAnimation = true;
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!InKit || !IsCharacter) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var c in clips)
            {
                string n = c.name.ToLowerInvariant();
                bool once = n.Contains("death") || n.Contains("hitreact") || n.Contains("punch") || n.EndsWith("jump") || n.Contains("jump_land") || n.Contains("wave") || n.EndsWith("yes") || n.EndsWith("no");
                c.loopTime = !once;
                c.wrapMode = once ? WrapMode.ClampForever : WrapMode.Loop;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!InKit) return;
            var ti = (TextureImporter)assetImporter;
            ti.filterMode = FilterMode.Point;          // the atlas is flat color swatches: keep them crisp
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
