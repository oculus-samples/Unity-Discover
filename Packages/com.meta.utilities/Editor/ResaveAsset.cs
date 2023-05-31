// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Linq;
using System.Threading.Tasks;
using UnityEditor;

namespace Meta.Utilities.Editor
{
    public class ResaveAsset
    {
        [MenuItem("Assets/Resave")]
        public static async void ResaveAssets()
        {
            try
            {
                _ = EditorUtility.DisplayCancelableProgressBar("Resaving assets", "", 0);

                var paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).ToArray();
                var assets = AssetDatabase.FindAssets("", paths);
                foreach (var (i, asset) in assets.Enumerate())
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(asset);
                    if (EditorUtility.DisplayCancelableProgressBar("Resaving assets", assetPath, i * 1.0f / assets.Length))
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }

                    var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    if (obj != null)
                    {
                        EditorUtility.SetDirty(obj);
                    }
                    await Task.Yield();
                }

                _ = EditorUtility.DisplayCancelableProgressBar("Resaving assets", "Saving...", 1);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Assets/Resave Assets", true)]
        public static bool ResaveAssetsValid() => Selection.assetGUIDs.Any();
    }
}
