using UnityEditor;
using UnityEngine;

namespace ZombiePile.EditorTools
{
    /// Import settings for the Quaternius "Zombie Apocalypse Kit" models in Assets/Resources/ZombieKit/:
    /// characters get Legacy animation (clips played by name, no Animator setup), props get none.
    /// Also the UI sprites in Assets/Resources/UI/: full size, uncompressed, no mipmaps (they are 9-sliced in code).
    public class KitImport : AssetPostprocessor
    {
        string P { get { return assetPath.Replace('\\', '/'); } }
        bool InKit { get { return P.Contains("/Resources/ZombieKit/"); } }
        bool IsCharacter { get { return P.Contains("/Resources/ZombieKit/Characters/"); } }
        bool IsUI { get { return P.Contains("/Resources/UI/"); } }

        // bump to make Unity reimport everything this postprocessor touches
        public override uint GetVersion() { return 2; }

        void OnPreprocessModel()
        {
            if (!InKit) return;
            var mi = (ModelImporter)assetImporter;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
            mi.isReadable = true;   // Kit reads the vertices once per model (size/facing check), also in WebGL builds
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
                bool once = n.Contains("death") || n.Contains("hitreact") || n.Contains("punch") || n.EndsWith("jump") || n.Contains("jump_land") || n.Contains("wave") || n.EndsWith("yes") || n.EndsWith("no") || n.EndsWith("duck");
                c.loopTime = !once;
                c.wrapMode = once ? WrapMode.ClampForever : WrapMode.Loop;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (IsUI)
            {
                var ui = (TextureImporter)assetImporter;
                ui.textureType = TextureImporterType.Default;
                ui.npotScale = TextureImporterNPOTScale.None;   // keep exact pixel sizes: the 9-slice borders depend on them
                ui.mipmapEnabled = false;
                ui.alphaIsTransparency = true;
                ui.wrapMode = TextureWrapMode.Clamp;
                ui.filterMode = FilterMode.Bilinear;
                ui.maxTextureSize = 2048;
                ui.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            if (!InKit) return;
            var ti = (TextureImporter)assetImporter;
            ti.filterMode = FilterMode.Point;          // the atlas is flat color swatches: keep them crisp
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
