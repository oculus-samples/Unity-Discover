using System;

using UnityEngine;

using ShaderType = Oculus.Avatar2.OvrAvatarShaderManagerBase.ShaderType;

/**
 * @file OvrAvatarRenderable.cs
 */
namespace Oculus.Avatar2
{
    [RequireComponent(typeof(MeshFilter))]
    /**
     * @class OvrAvatarRenderable
     * Component that encapsulates the meshes of an avatar.
     * This component can only be added to game objects that
     * have a Unity Mesh and a Mesh filter.
     *
     * Each OvrAvatarRenderable has one OvrAvatarPrimitive
     * that encapsulates the Unity Mesh and Material rendered.
     * Primitives may be shared across renderables but
     * renderables cannot be shared across avatars.
     *
     * @see OvrAvatarPrimitive
     * @see ApplyMeshPrimitive
     */
    public class OvrAvatarRenderable : MonoBehaviour, IDisposable
    {
        private string _logScopeCache = null;
        protected string logScope => _logScopeCache ??= GetType().Name;

        private const string OVR_VERTEX_HAS_TANGENTS_KEYWORD = "OVR_VERTEX_HAS_TANGENTS";
        private const string OVR_VERTEX_NO_TANGENTS_KEYWORD = "OVR_VERTEX_NO_TANGENTS";

        private const string OVR_VERTEX_INTERPOLATE_ATTRIBUTES_KEYWORD = "OVR_VERTEX_INTERPOLATE_ATTRIBUTES";
        private const string OVR_VERTEX_DO_NOT_INTERPOLATE_ATTRIBUTES_KEYWORD = "OVR_VERTEX_DO_NOT_INTERPOLATE_ATTRIBUTES";

        private const string OVR_VERTEX_FETCH_VERT_BUFFER = "OVR_VERTEX_FETCH_VERT_BUFFER";
        private const string OVR_VERTEX_FETCH_TEXTURE_KEYWORD = "OVR_VERTEX_FETCH_TEXTURE";
        private const string OVR_VERTEX_FETCH_EXTERNAL_BUFFER_KEYWORD = "OVR_VERTEX_FETCH_EXTERNAL_BUFFER";

        // Make sure these match the shader
        protected enum VertexFetchMode
        {
            VertexBuffer = 0,
            ExternalBuffers = 1,
            ExternalTextures = 2,
        }

        /// Designates whether this renderable is visible or not.
        public bool Visible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    var wasRendered = IsRendered;
                    _isVisible = value;
                    var isRendered = IsRendered;
                    if (wasRendered != isRendered)
                    {
                        OnVisibilityChanged(isRendered);
                    }
                }
            }
        }

        /// Designates whether this renderable is hidden or not.
        public bool IsHidden
        {
            get => _isHidden;
            set
            {
                if (_isHidden != value)
                {
                    var wasRendered = IsRendered;
                    _isHidden = value;
                    var isRendered = IsRendered;
                    if (wasRendered != isRendered)
                    {
                        OnVisibilityChanged(isRendered);
                    }
                }
            }
        }

        public bool IsRendered => _isVisible && !_isHidden;

        /// Triangle and vertex counts for all levels of detail.
        public ref readonly AvatarLODCostData CostData => ref AppliedPrimitive.CostData;

        /// Get which view(s) (first person, third person) this renderable applies to.
        /// These are established when the renderable is loaded.
        public CAPI.ovrAvatar2EntityViewFlags viewFlags => AppliedPrimitive.viewFlags;

        /// LOD bit flags for this renderable.
        /// These flags indicate which levels of detail this renderable is used by.
        public CAPI.ovrAvatar2EntityLODFlags lodFlags => AppliedPrimitive.lodFlags;

        /// Get which body parts of the avatar this renderable is used by.
        /// These are established when the renderable is loaded.
        public CAPI.ovrAvatar2EntityManifestationFlags manifestationFlags => AppliedPrimitive.manifestationFlags;

        /// Get whether this renderable has a mesh
        public bool HasMesh => MyMesh != null;

        /// Get the submeshes that are to be rendered.
        /// As excluded by the index buffer.
        public CAPI.ovrAvatar2EntitySubMeshInclusionFlags subMeshInclusionFlags => AppliedPrimitive.subMeshInclusionFlags;

        /// Get the quality flag preferences. This let's you render the asset differently, based on what's in the asset.
        public CAPI.ovrAvatar2EntityHighQualityFlags highQualityFlags => AppliedPrimitive.highQualityFlags;

        /// Get the vertex count of this renderable's mesh
        public int MeshVertexCount => MyMesh.vertexCount;

        /// Get the Unity Renderer used to render this renderable.
        public Renderer rendererComponent { get; private set; } = null;

#pragma warning disable CA2213 // Disposable fields should be disposed - it is not owned by this class
        protected OvrAvatarPrimitive AppliedPrimitive { get; private set; } = null;
#pragma warning restore CA2213 // Disposable fields should be disposed

        protected Mesh MyMesh { get; private set; } = null;
        protected MeshFilter MyMeshFilter { get; private set; } = null;

        /// True if this renderable has tangents for each vertex.
        protected bool HasTangents => AppliedPrimitive.hasTangents;

        protected virtual bool InterpolateAttributes => false;

        protected virtual VertexFetchMode VertexFetchType => VertexFetchMode.VertexBuffer;

        private Material _materialCopy = null;

        private bool _isVisible = false;
        private bool _isHidden = false;

        public int originalNumberIndices = 0;
        public UInt16[] originalIndexBuffer = null;
        private static AttributePropertyIds _propertyIds = default;

        protected MaterialPropertyBlock MatBlock { get; private set; }

        private static void CheckPropertyIdInit()
        {
            if (!_propertyIds.IsValid)
            {
                _propertyIds = new AttributePropertyIds(AttributePropertyIds.InitMethod.PropertyToId);
            }
        }

        /**
         * Sets the specified shader keyword for the material on this renderable.
         * @see SetShader
         * @see OvrAvatarMaterial
         */
        public void SetMaterialKeyword(string keyword, bool enable)
        {
            CopyMaterial();
            if (enable)
            {
                _materialCopy.EnableKeyword(keyword);
            }
            else
            {
                _materialCopy.DisableKeyword(keyword);
            }
        }

        /**
         * Sets the shader for the material on this renderable.
         * @see SetMaterialKeyword
         * @see OvrAvatarMaterial
         */
        public void SetShader(Shader shader)
        {
            CopyMaterial();
            _materialCopy.shader = shader;
        }


        /**
         * Invoked to instantiate the appropriate renderer class for this instance.
         */
        protected virtual void AddDefaultRenderer()
        {
            AddRenderer<MeshRenderer>();
        }

        /**
         * Invoked when `IsRendered` changes.
         * @see IsVisible
         * @see IsHidden
         * @see IsRendering
         */
        protected virtual void OnVisibilityChanged(bool isNowRendered)
        {
            enabled = isNowRendered;
            rendererComponent.forceRenderingOff = !isNowRendered;
        }

        protected void CheckDefaultRenderer()
        {
            if (rendererComponent == null)
            {
                AddDefaultRenderer();
            }
        }

        protected virtual void Awake()
        {
            CheckPropertyIdInit();
            AddDefaultRenderer();

            MyMeshFilter = gameObject.GetOrAddComponent<MeshFilter>();
            MatBlock = new MaterialPropertyBlock();
        }

        // TODO: This probably isn't a good pattern, too easy for subclasses to stomp on each other
        protected T AddRenderer<T>() where T : Renderer
        {
            var customRenderer = GetComponent<T>();
            if (!customRenderer)
            {
                customRenderer = gameObject.AddComponent<T>();
            }

            rendererComponent = customRenderer;
            return customRenderer;
        }

        protected void CopyMaterial()
        {
            if (_materialCopy == null)
            {
                var sharedMaterial = rendererComponent.sharedMaterial;
                OvrAvatarLog.AssertConstMessage(
                    sharedMaterial != null, "RendererComponent has no material!", logScope, this);
                _materialCopy = sharedMaterial is null ? new Material(EmergencyFallbackShader) : new Material(sharedMaterial);

                rendererComponent.sharedMaterial = _materialCopy;
            }
        }

        protected virtual void OnDestroy()
        {
            Dispose(true);
        }

        /**
         * Replaces the primitive with the Unity mesh and material.
         * Each renderable can reference a single primitive.
         * This primitive can be changed at run-time.
         *
         * The *OVR_VERTEX_HAS_TANGENTS* shader keyword is set.
         * based on whether this primitive has per-vertex tangents.
         */
        protected internal virtual void ApplyMeshPrimitive(OvrAvatarPrimitive primitive)
        {
            OvrAvatarLog.Assert(AppliedPrimitive == null);

            CheckDefaultRenderer();

            AppliedPrimitive = primitive;

            MyMeshFilter.sharedMesh = MyMesh = primitive.mesh;
            rendererComponent.sharedMaterial = primitive.material;

            // Check if has tangents keywords needs enabling or not but don't enable/disable
            // if material already has keyword enabled or not (save material copy at this point in time)
            Material sharedMaterial = rendererComponent.sharedMaterial;
            bool hasTangentsEnabled = sharedMaterial.IsKeywordEnabled(OVR_VERTEX_HAS_TANGENTS_KEYWORD);
            bool hasInterpolationEnabled = sharedMaterial.IsKeywordEnabled(OVR_VERTEX_INTERPOLATE_ATTRIBUTES_KEYWORD);
            VertexFetchMode keywordSpecifiedVertexFetchMode = GetVertexFetchModeFromKeywords(sharedMaterial);

            if (HasTangents != hasTangentsEnabled)
            {
                SetMaterialKeyword(OVR_VERTEX_HAS_TANGENTS_KEYWORD, HasTangents);
                SetMaterialKeyword(OVR_VERTEX_NO_TANGENTS_KEYWORD, !HasTangents);
            }

            if (InterpolateAttributes != hasInterpolationEnabled)
            {
                SetMaterialKeyword(OVR_VERTEX_INTERPOLATE_ATTRIBUTES_KEYWORD, InterpolateAttributes);
                SetMaterialKeyword(OVR_VERTEX_DO_NOT_INTERPOLATE_ATTRIBUTES_KEYWORD, !InterpolateAttributes);
            }

            if (VertexFetchType != keywordSpecifiedVertexFetchMode)
            {
                SetMaterialVertexFetchKeyword(VertexFetchType);
            }

            // Update particular properties via a property block in case the keywords aren't enabled (and other
            // properties)
            rendererComponent.GetPropertyBlock(MatBlock);
            MatBlock.SetInt(_propertyIds.HasTangentsPropId, HasTangents ? 1 : 0);
            MatBlock.SetInt(_propertyIds.InterpolateAttributesPropId, InterpolateAttributes ? 1 : 0);
            MatBlock.SetInt(_propertyIds.VertexFetchModePropId, (int)VertexFetchType);
            rendererComponent.SetPropertyBlock(MatBlock);
        }

        private VertexFetchMode GetVertexFetchModeFromKeywords(Material mat)
        {
            if (mat.IsKeywordEnabled(OVR_VERTEX_FETCH_TEXTURE_KEYWORD))
            {
                return VertexFetchMode.ExternalTextures;
            }

            if (mat.IsKeywordEnabled(OVR_VERTEX_FETCH_EXTERNAL_BUFFER_KEYWORD))
            {
                return VertexFetchMode.ExternalBuffers;
            }

            if (mat.IsKeywordEnabled(OVR_VERTEX_FETCH_VERT_BUFFER))
            {
                return VertexFetchMode.VertexBuffer;
            }

            return VertexFetchMode.VertexBuffer;
        }

        private void SetMaterialVertexFetchKeyword(VertexFetchMode mode)
        {
            switch (mode)
            {
                case VertexFetchMode.VertexBuffer:
                    SetMaterialKeyword(OVR_VERTEX_FETCH_VERT_BUFFER, true);
                    SetMaterialKeyword(OVR_VERTEX_FETCH_EXTERNAL_BUFFER_KEYWORD, false);
                    SetMaterialKeyword(OVR_VERTEX_FETCH_TEXTURE_KEYWORD, false);
                    break;
                case VertexFetchMode.ExternalBuffers:
                    SetMaterialKeyword(OVR_VERTEX_FETCH_VERT_BUFFER, false);
                    SetMaterialKeyword(OVR_VERTEX_FETCH_EXTERNAL_BUFFER_KEYWORD, true);
                    SetMaterialKeyword(OVR_VERTEX_FETCH_TEXTURE_KEYWORD, false);
                    break;
                case VertexFetchMode.ExternalTextures:
                    SetMaterialKeyword(OVR_VERTEX_FETCH_VERT_BUFFER, false);
                    SetMaterialKeyword(OVR_VERTEX_FETCH_EXTERNAL_BUFFER_KEYWORD, false);
                    SetMaterialKeyword(OVR_VERTEX_FETCH_TEXTURE_KEYWORD, true);
                    break;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool isMainThread)
        {
            if (isMainThread)
            {
                if (rendererComponent != null) { Renderer.Destroy(rendererComponent); }
                if (MyMeshFilter != null) { MyMeshFilter.sharedMesh = null; }
                if (_materialCopy != null) { Material.Destroy(_materialCopy); }
            }

            rendererComponent = null;
            AppliedPrimitive = null;
            MyMesh = null;
            MyMeshFilter = null;

            _materialCopy = null;
            MatBlock = null;
        }

        private struct AttributePropertyIds
        {
            public readonly int HasTangentsPropId;
            public readonly int VertexFetchModePropId;
            public readonly int InterpolateAttributesPropId;

            // These will both be 0 if default initialized, otherwise they are guaranteed unique
            public bool IsValid => HasTangentsPropId != VertexFetchModePropId;

            public enum InitMethod { PropertyToId }
            public AttributePropertyIds(InitMethod initMethod)
            {
                HasTangentsPropId = Shader.PropertyToID("_OvrHasTangents");
                VertexFetchModePropId = Shader.PropertyToID("_OvrVertexFetchMode");
                InterpolateAttributesPropId = Shader.PropertyToID("_OvrInterpolateAttributes");
            }
        }

        public void GetRenderParameters(out Mesh mesh, out Material material, out Transform transform, MaterialPropertyBlock matrialProps)
        {
            mesh = null;
            material = null;
            transform = null;

            MeshRenderer renderer = rendererComponent as MeshRenderer;
            if (renderer == null)
            {
                return;
            }
            transform = renderer.transform;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null)
            {
                mesh = filter.sharedMesh;
            }
            material = renderer.material;
            renderer.GetPropertyBlock(matrialProps);
        }

        private const ShaderType FallbackShaderType = ShaderType.Default;
        private static Shader _fallbackShader = null;
        private static Shader EmergencyFallbackShader
        {
            get
            {
                if (_fallbackShader == null)
                {
                    var manager = OvrAvatarManager.Instance;
                    if (manager == null) { return null; }

                    var shaderManager = manager.ShaderManager;
                    if (shaderManager == null) { return null; }

                    var configuration = shaderManager.GetConfiguration(FallbackShaderType);
                    if (configuration == null) { return null;}

                    _fallbackShader = configuration.Shader;
                }
                return _fallbackShader;
            }
        }
    } // end class
}
