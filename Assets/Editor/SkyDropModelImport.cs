using System.IO;
using UnityEditor;

namespace SkyDrop.EditorTools
{
    /// Auto-configures character FBX files dropped into Assets/Resources/SkyDrop/Character/:
    /// Humanoid rig, one clip named after the file, looping where it should, root motion baked.
    public class SkyDropModelImport : AssetPostprocessor
    {
        const string Folder = "/Resources/SkyDrop/Character/";
        static readonly string[] Looping = { "idle", "freefall", "canopy", "celebrate", "tumble" };

        bool InFolder { get { return assetPath.Replace('\\', '/').Contains(Folder); } }

        void OnPreprocessModel()
        {
            if (!InFolder) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPreprocessAnimation()
        {
            if (!InFolder) return;
            var mi = (ModelImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool loop = System.Array.IndexOf(Looping, name.ToLowerInvariant()) >= 0;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = i == 0 ? name : name + i;
                clips[i].loopTime = loop;
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }
    }
}
