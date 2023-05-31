
using UnityEngine;

/// @file OvrAvatarShaderManagerBase.cs

namespace Oculus.Avatar2
{
    ///
    /// Configures avatar shader properties.
    /// This shader configuration is used for all avatars.
    /// This shader manager only supports one shader configuration.
    /// Use @ref OvrAvatarShaderManagerMultiple if you need more than one.
    /// @see OvrAvatarShaderManagerBase
    /// @see OvrAvatarShaderConfiguration
    ///
    public class OvrAvatarShaderManagerSingle : OvrAvatarShaderManagerBase
    {
        protected OvrAvatarShaderConfiguration _configuration = null;
        protected OvrAvatarShaderConfiguration _fastloadConfiguration = null;

        // The following requires maintenance but is an easy alternative to creating a custom Unity editor for this manager
        [SerializeField]
        protected OvrAvatarShaderConfiguration DefaultShaderConfigurationInitializer;
        [SerializeField]
        protected OvrAvatarShaderConfiguration FastLoadConfigurationInitializer;

        public override OvrAvatarShaderConfiguration GetConfiguration(ShaderType type)
        {
            int typeNumber = (int)type;
            if (!(type == ShaderType.Default || type == ShaderType.FastLoad) || typeNumber >= OvrAvatarShaderManagerBase.ShaderTypeCount)
            {
                OvrAvatarLog.LogError(
                  $"OvrAvatarShaderConfiguration for shader type [{type}] is invalid OvrAvatarShaderManagerSingle.");
                return _configuration;
            }
            if (type == ShaderType.FastLoad)
            {
                return _fastloadConfiguration;
            }
            else
            {
                return _configuration;
            }
        }

        protected override void Initialize(bool force)
        {
            base.Initialize(force);

            if (_configuration != null && _fastloadConfiguration != null)
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
            if (null == DefaultShaderConfigurationInitializer || null == FastLoadConfigurationInitializer)
            {
                return;
            }

            _configuration = DefaultShaderConfigurationInitializer;
            _fastloadConfiguration = FastLoadConfigurationInitializer;
        }

        public override bool AutoGenerateShaderConfigurations()
        {
            _configuration = ScriptableObject.CreateInstance<OvrAvatarShaderConfiguration>();
            InitializeComponent(ref _configuration);
            _fastloadConfiguration = ScriptableObject.CreateInstance<OvrAvatarShaderConfiguration>();
            InitializeComponent(ref _fastloadConfiguration);
            return true;
        }
    }
}
