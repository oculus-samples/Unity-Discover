namespace Oculus.Avatar2
{
    public static class OvrAvatarPlugin
    {
#if UNITY_EDITOR_OSX
        public const string PluginFolderPath = "Assets/Oculus/Avatar2/Plugins/";
        public const string InternalPluginFolderPath = "Assets/Internal/Plugins/";
        public const string FlavorFolder = "";
        public const string OSFolder = "Macos/";
        public const string FullPluginFolderPath = PluginFolderPath + FlavorFolder + OSFolder;
        public const string FullInternalPluginFolderPath = InternalPluginFolderPath + FlavorFolder + OSFolder;
#endif  // UNITY_EDITOR_OSX
    }
}
