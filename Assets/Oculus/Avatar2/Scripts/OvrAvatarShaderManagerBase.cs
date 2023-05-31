using System;

using UnityEngine;

/// @file OvrAvatarShaderManagerBase.cs

namespace Oculus.Avatar2
{
    ///
    /// Maintains a list of shader configurations containing
    /// material properties and texture names for the various types
    /// of shading performed on the avatar.
    /// There are several distince shader types (eye, skin, hair, emissive, ...)
    /// each with their own shader configuration. The shader manager suggests
    /// the shader used to synthesize a material based off these configurations.
    /// @see OvrAvatarShaderConfiguration
    /// @see OvrAvatarShaderManagerSingle
    /// @see OvrAvatarShaderManagerMultiple
    ///

    public abstract class OvrAvatarShaderManagerBase : MonoBehaviour
    {

        // The intent here is for each shader "type" to have it's own shader configuration.
        // Possible shader types are "combined", "separate", "eyes", "hair", "transparent".
        // We need to know, first from the "material" specification in the GLTF, and second
        // from the parts metadata in GLTF meshes/primitives, what shader model to use...
        public enum ShaderType
        {
            Default,
            Array,
            SolidColor,
            Transparent,
            Emmisive,
            Skin,
            LeftEye,
            RightEye,
            Hair,
            FastLoad
        }
        /// Gets the number of shader types.
        public static readonly int ShaderTypeCount = Enum.GetNames(typeof(ShaderType)).Length;

        /// True if initialized, else false.
        public bool Initialized { get; protected set; } = false;

        ///
        /// Get the shader configuration for the given shader type.
        /// @param type shader type to get configuration for.
        /// @return shader configuration for the input type, null if none specified.
        /// @see ShaderType
        ///
        public abstract OvrAvatarShaderConfiguration GetConfiguration(ShaderType type);

        protected virtual void Start()
        {
            Initialize(true);
        }

        protected void OnShutdown()
        {
            Initialized = false;
        }

        ///
        /// Initialize the shader manager.
        ///
        protected virtual void Initialize(bool force)
        {
            if (!force && Initialized) return;

            RegisterShaderConfigurationInitializers();
        }

        protected abstract void RegisterShaderConfigurationInitializers();

        ///
        /// Determine the shader type from material properties.
        /// @param materialName name of the material.
        /// @param hasMetallic  true if the material is metallic.
        /// @param hasSpecular  true if the material has specular reflections.
        /// @return shader type to use.
        /// @see GetConfiguration
        ///
        public virtual ShaderType DetermineConfiguration(string materialName, bool hasMetallic, bool hasSpecular, bool hasTextures)
        {
            // TODO: look at the texture inputs for a material to determine if they are a texture array or not.
            // This presents a difficult situation because we need to know information about the textures before
            // determining the shader type to begin synthesis of the material. It may require an extra call to
            // OvrAvatarLibrary.MakeTexture() or something equivalent.

            if (!hasTextures)
            {
                return ShaderType.FastLoad;
            }
            if (hasMetallic || hasSpecular)
            {
                return ShaderType.Default;
            }
            return ShaderType.SolidColor;
        }

        ///
        /// Automatically generate shader configurations.
        /// Creates a @ref OvrAvatarShaderConfiguration ScriptableObject
        /// for one or more shaders.
        ///
        public abstract bool AutoGenerateShaderConfigurations();

        protected virtual void InitializeComponent(ref OvrAvatarShaderConfiguration configuration)
        {
            configuration.Shader = Shader.Find("Standard");

            configuration.NameTextureParameter_baseColorTexture = "_MainTex";
            configuration.NameTextureParameter_diffuseTexture = "_MainTex";
            configuration.NameTextureParameter_metallicRoughnessTexture = "_MetallicGlossMap";
            configuration.NameTextureParameter_specularGlossiness = "_Specular";
            configuration.NameTextureParameter_normalTexture = "_BumpMap";
            configuration.NameTextureParameter_occlusionTexture = "_OcclusionMap";
            configuration.NameTextureParameter_emissiveTexture = "_EmissiveMap";
            configuration.NameTextureParameter_flowTexture = "_FlowMap";

            configuration.NameColorParameter_BaseColorFactor = "_Color";
            configuration.NameColorParameter_DiffuseFactor = "_Diffuse";
        }
    }
}
