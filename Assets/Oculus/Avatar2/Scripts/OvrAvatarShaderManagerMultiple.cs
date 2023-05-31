
using UnityEngine;

/// @file OvrAvatarShaderManagerMultiple.cs

namespace Oculus.Avatar2
{
    ///
    /// Configures shader properties for more than one shader type.
    /// There are several distince shader types (eye, skin, hair, emissive, ...)
    /// each with their own shader configuration. The shader manager suggests
    /// the shader used to synthesize a material based off these configurations.
    /// @see OvrAvatarShaderConfiguration
    /// @see OvrAvatarShaderManagerSingle
    /// @see OvrAvatarShaderManagerBase
    ///
    public class OvrAvatarShaderManagerMultiple : OvrAvatarShaderManagerBase
    {
        protected OvrAvatarShaderConfiguration[] _configurations = null;

        // The following requires maintenance but is an easy alternative to creating a custom Unity editor for this manager
        [SerializeField]
        protected OvrAvatarShaderConfiguration DefaultShaderConfigurationInitializer;
        [SerializeField]
        protected OvrAvatarShaderConfiguration ArrayShaderConfigurationInitializer;
        [SerializeField]
        protected OvrAvatarShaderConfiguration SolidColorShaderConfigurationInitializer;
        [SerializeField]
        protected OvrAvatarShaderConfiguration TransparentShaderConfigurationInitializer;
        [SerializeField]
        protected OvrAvatarShaderConfiguration EmmisiveShaderConfigurationInitializer;
        [SerializeField]
        protected OvrAvatarShaderConfiguration SkinShaderConfigurationInitializer;
        [SerializeField]
        protected OvrAvatarShaderConfiguration LeftEyeShaderConfigurationInitializer;
        [SerializeField]
        protected OvrAvatarShaderConfiguration RightEyeShaderConfigurationInitializer;
        [SerializeField]
        protected OvrAvatarShaderConfiguration HairShaderConfigurationInitializer;
        [SerializeField]
        protected OvrAvatarShaderConfiguration FastLoadConfigurationInitializer;

        public override OvrAvatarShaderConfiguration GetConfiguration(ShaderType type)
        {
            int typeNumber = (int)type;
            if (_configurations == null || typeNumber >= _configurations.Length || _configurations[typeNumber] == null)
            {
                OvrAvatarLog.LogError(
                  $"OvrAvatarShaderConfiguration for shader type [{type}] has not been initialized. Please add it to the ShaderManager.");
                return (_configurations != null && _configurations.Length > 0) ? _configurations[(int)ShaderType.Default] : null;
            }
            return _configurations[typeNumber];
        }

        protected override void Initialize(bool force)
        {
            base.Initialize(force);

            if (_configurations != null && _configurations.Length > 0)
            {
                Initialized = true;
            }
            else
            {
                Initialized = AutoGenerateShaderConfigurations();
            }
        }

        protected override void RegisterShaderConfigurationInitializers()
        {
            // if all of these elements are null or empty just quit and let the system run AutoGenerateShaderConfigurations() later
            if (null == DefaultShaderConfigurationInitializer &&
                null == ArrayShaderConfigurationInitializer &&
                null == SolidColorShaderConfigurationInitializer &&
                null == TransparentShaderConfigurationInitializer &&
                null == EmmisiveShaderConfigurationInitializer &&
                null == SkinShaderConfigurationInitializer &&
                null == LeftEyeShaderConfigurationInitializer &&
                null == RightEyeShaderConfigurationInitializer &&
                null == HairShaderConfigurationInitializer &&
                null == FastLoadConfigurationInitializer)
            {
                return;
            }

            _configurations = new OvrAvatarShaderConfiguration[ShaderTypeCount];
            _configurations[(int)ShaderType.Default] = DefaultShaderConfigurationInitializer;
            _configurations[(int)ShaderType.Array] = ArrayShaderConfigurationInitializer;
            _configurations[(int)ShaderType.SolidColor] = SolidColorShaderConfigurationInitializer;
            _configurations[(int)ShaderType.Transparent] = TransparentShaderConfigurationInitializer;
            _configurations[(int)ShaderType.Emmisive] = EmmisiveShaderConfigurationInitializer;
            _configurations[(int)ShaderType.Skin] = SkinShaderConfigurationInitializer;
            _configurations[(int)ShaderType.LeftEye] = LeftEyeShaderConfigurationInitializer;
            _configurations[(int)ShaderType.RightEye] = RightEyeShaderConfigurationInitializer;
            _configurations[(int)ShaderType.Hair] = HairShaderConfigurationInitializer;
            _configurations[(int)ShaderType.FastLoad] = FastLoadConfigurationInitializer;
        }

        public override bool AutoGenerateShaderConfigurations()
        {
            _configurations = new OvrAvatarShaderConfiguration[ShaderTypeCount];
            for (int i = 0; i < _configurations.Length; i++)
            {
                _configurations[i] = ScriptableObject.CreateInstance<OvrAvatarShaderConfiguration>();
                InitializeComponent(ref _configurations[i]);
            }
            return true;
        }
    }
}
