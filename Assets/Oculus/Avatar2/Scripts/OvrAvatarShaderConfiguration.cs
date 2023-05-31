using System;
using UnityEngine;

/// @file OvrAvatarShaderManagerBase.cs

namespace Oculus.Avatar2
{
    ///
    /// Contains material properties, shader keywords and texture map names
    /// for shading an avatar. You can have multiple instances of this class
    /// which apply to shading different parts of your avatar.
    /// @see OvrAvatarShaderManagerBase
    /// @see OvrAvatarShaderManagerSingle
    /// @see OvrAvatarShaderManagerMulti
    ///

    [CreateAssetMenu(fileName = "DefaultShaderConfiguration", menuName = "Facebook/Avatar/SDK/OvrAvatarShaderConfiguration", order = 1)]
    public class OvrAvatarShaderConfiguration : ScriptableObject
    {
        public Material Material;
        public Shader Shader;

        // These texture input names are based on what GLTF 2.0 has to offer,
        // regardless of what the specific implementation application uses
        public string NameTextureParameter_baseColorTexture = "_MainTex"; // for metallic roughness materials
        public string NameTextureParameter_diffuseTexture = "_MainTex";  // for specular glossy materials
        public string NameTextureParameter_metallicRoughnessTexture = "_MetallicGlossMap";
        public string NameTextureParameter_specularGlossiness = "_Specular";
        public string NameTextureParameter_normalTexture = "_BumpMap";
        public string NameTextureParameter_occlusionTexture = "_OcclusionMap";
        public string NameTextureParameter_emissiveTexture = "_EmissiveMap";
        public string NameTextureParameter_flowTexture = "_FlowMap";

        public string NameColorParameter_BaseColorFactor = "_Color";
        public bool UseColorParameter_BaseColorFactor = false;

        public string NameFloatParameter_MetallicFactor = "_Metallic";
        public bool UseFloatParameter_MetallicFactor = false;

        public string NameFloatParameter_RoughnessFactor = "_Roughness";
        public bool UseFloatParameter_RoughnessFactor = false;

        public string NameColorParameter_DiffuseFactor = "_Diffuse";
        public bool UseColorParameter_DiffuseFactor = false;

        public string[] KeywordsEnumerations;
        public string[] KeywordsToEnable;

        public string[] NameFloatConstants;
        public float[] ValueFloatConstants;

        public OvrAvatarMaterialExtensionConfig ExtensionConfiguration;

        public void ApplyKeywords(Material material)
        {
            if (KeywordsEnumerations != null && KeywordsEnumerations.Length > 0)
            {
                foreach (var keyword in KeywordsEnumerations)
                {
                    material.DisableKeyword(keyword);
                }
            }
            if (KeywordsToEnable != null && KeywordsToEnable.Length > 0)
            {
                foreach (var keyword in KeywordsToEnable)
                {
                    material.EnableKeyword(keyword);
                }
            }
        }

        public void ApplyFloatConstants(Material material)
        {
            if (NameFloatConstants != null && ValueFloatConstants != null &&
                NameFloatConstants.Length > 0 && ValueFloatConstants.Length > 0)
            {
                for (int i = 0; i < NameFloatConstants.Length && i < ValueFloatConstants.Length; i++)
                {
                    string nameConstant = NameFloatConstants[i];
                    float valueConstant = ValueFloatConstants[i];
                    material.SetFloat(nameConstant, valueConstant);
                }
            }
        }
    }
}
