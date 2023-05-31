using System.Collections.Generic;
using UnityEditor;

namespace Oculus.Avatar2
{
    public static class EditorBuildScenes
    {
        public static List<string> OverrideScenes { get; } = new List<string>();

        public static string[] GetBuildScenes()
        {
            if (OverrideScenes.Count > 0)
            {
                return OverrideScenes.ToArray();
            }

            // Fall back to scenes from build settings
            var buildScenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    buildScenes.Add(scene.path);
                }
            }

            return buildScenes.ToArray();
        }
    }
}
