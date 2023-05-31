using System;
using UnityEngine;

namespace Oculus.Avatar2
{

    public partial class OvrAvatarEntity : MonoBehaviour
    {
        private static readonly int DEBUG_TINT_ID = Shader.PropertyToID("_DebugTint");

        private OvrAvatarMaterial _material = null;

        [Obsolete("Deprecated - no longer necessary")]
        public void InitializeMaterialPropertyBlock()
        {
        }

        public OvrAvatarMaterial Material
        {
            get { return _material; }
        }

        /**
         * Enables or disables a shader keyword for this avatar.
         * The changes are immediately applied to all its renderables.
         */
        [Obsolete("Use OvrAvatarMaterial instead", false)]
        public void SetMaterialKeyword(string keyword, bool enable)
        {
            // remember keyword for future renderables
            _material.SetKeyword(keyword, enable);
            foreach (var meshNodeKVP in _meshNodes)
            {
                foreach (var primRenderable in meshNodeKVP.Value)
                {
                    var renderable = primRenderable.renderable;
                    if (!renderable) { continue; }
                    renderable.SetMaterialKeyword(keyword, enable);
                }
            }
        }
        /**
         * Changes the shader used by this avatar.
         * The changes are immediately applied to all its renderables.
         */
        [Obsolete("Use OvrAvatarMaterial instead", false)]
        public void SetMaterialShader(Shader shader)
        {
            // remember shader for future renderables
            _material.SetShader(shader);
            foreach (var meshNodeKVP in _meshNodes)
            {
                foreach (var primRenderable in meshNodeKVP.Value)
                {
                    var renderable = primRenderable.renderable;
                    if (!renderable) { continue; }
                    renderable.SetShader(shader);
                }
            }
        }

        /**
         * Changes one or more material properties via a callback.
         * The changes are immediately applied to all its renderables.
         */
        [Obsolete("Use OvrAvatarMaterial instead", false)]
        public void SetMaterialProperties(Action<OvrAvatarMaterial> callback)
        {
            callback(_material);
            ApplyMaterial();
        }

        /**
         * Changes one or more material properties via a callback.
         * The changes are immediately applied to all its renderables.
         */
        [Obsolete("Use OvrAvatarMaterial instead", false)]
        public void SetMaterialProperties<TParam>(Action<OvrAvatarMaterial, TParam> callback, TParam userData)
        {
            callback(_material, userData);
            ApplyMaterial();
        }

        /**
         * Applies the shader, keywords and material properties from
         * OvrAvatarEntity.material to all the renderables associated
         * with this avatar.
         */
        public void ApplyMaterial()
        {
            foreach (var meshNodeKVP in _meshNodes)
            {
                foreach (var primRenderable in meshNodeKVP.Value)
                {
                    var renderable = primRenderable.renderable;
                    if (!renderable) { continue; }
                    _material.Apply(renderable);
                }
            }
        }

        public void SetSharedMaterialProperties(Action<UnityEngine.Material> callback)
        {
            // TODO: This will not cover all future renderables
            // Each primitive has its own property block so callback has to be called once per primitive
            // TODO: Check if there's a way around this

            foreach (var meshNodeKVP in _meshNodes)
            {
                foreach (var primRenderable in meshNodeKVP.Value)
                {
                    var renderable = primRenderable.renderable;
                    if (!renderable) { continue; }
                    var rend = renderable.rendererComponent;
                    callback(rend.sharedMaterial);
                }
            }
        }

        private void UpdateAvatarLodColor()
        {
            if (AvatarLOD.Level > -1 && AvatarLODManager.Instance.debug.displayLODColors)
            {
                _material.SetKeyword("DEBUG_TINT", true);
                _material.SetColor(DEBUG_TINT_ID, AvatarLODManager.LOD_COLORS[AvatarLOD.overrideLOD ? AvatarLOD.overrideLevel : AvatarLOD.Level]);
            }
            else
            {
                _material.SetKeyword("DEBUG_TINT", true);
                _material.SetColor(DEBUG_TINT_ID, Color.white);
            }
            ApplyMaterial();
        }

        /***
         * Applies the current material state (keywords, shader, properties)
         * to the given renderable. This function should be called whenever a
         * new renderable is added.
         */
        internal void ConfigureRenderableMaterial(OvrAvatarRenderable renderable)
        {
            if (!renderable) { return; }
            _material.Apply(renderable);
        }
    }
}
