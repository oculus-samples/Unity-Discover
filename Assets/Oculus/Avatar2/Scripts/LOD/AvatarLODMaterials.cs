using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

#endif


namespace Oculus.Avatar2
{
    public class AvatarLODMaterials
    {
#if UNITY_EDITOR
        // TODO: Move these materials into the Avatar SDK and reference them there:
        public static readonly string AVATAR_LOD_MATERIAL_PATH = "Assets/Package/AvatarAssetsSrc/Res/LOD/Materials/";
#endif

#if UNITY_EDITOR
        private static Material lodNoneMaterial_ = null;
        public static Material LodNoneMaterial
        {
            get
            {
                if (lodNoneMaterial_ == null)
                {
                    lodNoneMaterial_ = GetOrCreateLODMaterial(AVATAR_LOD_MATERIAL_PATH + "LODNoneMaterial.mat", Color.white);
                }
                return lodNoneMaterial_;
            }
        }
#else
    public readonly static Material LodNoneMaterial = null;
#endif


#if UNITY_EDITOR
        private static Material lodOutOfRangeMaterial_ = null;

        public static Material LodOutOfRangeMaterial
        {
            get
            {
                if (lodOutOfRangeMaterial_ == null)
                {
                    lodOutOfRangeMaterial_ =
                      GetOrCreateLODMaterial(AVATAR_LOD_MATERIAL_PATH + "LODOutOfRangeMaterial.mat", Color.white);
                }
                return lodOutOfRangeMaterial_;
            }
        }
#else
    public readonly static Material LodOutOfRangeMaterial = null;
#endif

        private static List<Material> lodMaterials_ = null;

        public static List<Material> LodMaterials
        {
            get
            {
#if UNITY_EDITOR
                if (lodMaterials_ == null)
                {
                    lodMaterials_ = new List<Material>();
                    for (int i = 0; i < AvatarLODManager.LOD_COLORS.Length; i++)
                    {
                        lodMaterials_.Add(GetOrCreateLODMaterial(AVATAR_LOD_MATERIAL_PATH + "LOD" + i + "Material.mat",
                          AvatarLODManager.LOD_COLORS[i]));
                    }
                }
#endif
                return lodMaterials_;
            }
        }

        private static Material GetOrCreateLODMaterial(string materialPath, Color color)
        {
            Material lodMaterial = null;
#if UNITY_EDITOR
            if (File.Exists(materialPath))
            {
                lodMaterial = AssetDatabase.LoadAssetAtPath(materialPath, typeof(Material)) as Material;
                Color col = lodMaterial.GetColor("_Color");
                if (col != color)
                {
                    lodMaterial.SetColor("_Color", color);
                }
            }
#endif
            return lodMaterial;
        }
    }
}
