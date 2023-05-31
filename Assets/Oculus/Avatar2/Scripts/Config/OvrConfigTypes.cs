namespace Oculus.Avatar2
{
    public enum DefaultableBool : sbyte
    {
        Off = 0,
        On = 1,
        Default = -1,
    }

    public static class DefaultableExtensions
    {
        public static bool GetValue(this DefaultableBool defaultableBool, bool defaultValue = false)
        {
            return (defaultableBool == DefaultableBool.On) ||
                   (defaultableBool == DefaultableBool.Default && defaultValue);
        }
    }
}
