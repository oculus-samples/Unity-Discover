using UnityEngine;

/// @file OvrAvatarManager_Textures.cs

namespace Oculus.Avatar2
{
    public partial class OvrAvatarManager
    {
        private const int TEXTURE_SETTINGS_INSPECTOR_ORDER = 256;

        [Header("Avatar Texture Settings", order = TEXTURE_SETTINGS_INSPECTOR_ORDER)]

        [SerializeField]
        [Tooltip("Texture Filter Mode used for Avatar textures." +
                 "\nTrilinear is highest quality with excellent performance." +
                 "\nBilinear may produce artifacts at certain distances." +
                 "\nPoint is primarily for stylistic usage.")]
        private FilterMode _filterMode = OvrAvatarImage.defaultFilterMode;
        
        [SerializeField]
        [Tooltip("Anisotropic filtering level used for Avatar textures. Higher values are more expensive to render." +
                 "\n0 is off regardless of project Quality settings" +
                 "\n1 is off unless forced on via project Quality settings")]
        [Range(0, 16)]
        private int _anisoLevel = OvrAvatarImage.defaultAnisoLevel;

        public FilterMode TextureFilterMode => _filterMode;
        public int TextureAnisoLevel => _anisoLevel;
    }
}
