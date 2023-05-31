using System.Linq;
using System.Reflection;

using UnityEditor;

using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Oculus.Avatar2
{
    public class AvatarResourcesWindow : EditorWindow
    {
        public enum ResourceTabId : int
        {
            Loaders = 0,
            GpuSkinning = 1,

            Default = Loaders,
        }

        private static GUIStyle _titleStyle;
        Vector2 _scrollPosition;
        private OvrAvatarPrimitive _primitive;

        private string[] _tabs = new string[] { "Resource Loaders", "GPU Skinning" };
        private ResourceTabId _selectedTab = ResourceTabId.Default;

        private static readonly string[] _inputPrefixes = new string[] { "neutral", "morphSrc", "morphCombined", "indirection", "joints" };
        private static readonly string[] _outputPrefixes = new string[] { "morphJointSkinnerOutput", "jointSkinnerOutput", "morphSkinnerOutput" };

        private long _totalTextureMemoryUsed = 0;
        private long _totalMeshMemoryUsed = 0;

        // Add menu named "My Window" to the Window menu
        [MenuItem("AvatarSDK2/Resources Window")]
        static void Init()
        {
            AvatarResourcesWindow window = (AvatarResourcesWindow)EditorWindow.GetWindow(typeof(AvatarResourcesWindow));
            window.Show();

        }

        void OnGUI()
        {
            if (EditorApplication.isPlaying)
            {
                _selectedTab = (ResourceTabId)GUILayout.Toolbar((int)_selectedTab, _tabs);
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                switch (_selectedTab)
                {
                    case ResourceTabId.Loaders:
                        {
                            if (_primitive != null)
                            {
                                RenderPrimitiveData();
                            }
                            else
                            {
                                RenderResourceLoaders();
                            }
                        }
                        break;
                    case ResourceTabId.GpuSkinning:
                        RenderGPUSkinningData();
                        break;
                }
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.LabelField("Game must be running to view resources");
            }
        }

        void RenderPrimitiveData()
        {
            _titleStyle = new GUIStyle(EditorStyles.helpBox);
            _titleStyle.fontSize = 24;
            if (GUILayout.Button("Back"))
            {
                _primitive = null;
            }

            if (_primitive == null)
            {
                EditorGUILayout.LabelField("Data has no value");
                return;
            }

            var fields = _primitive.data.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            EditorGUILayout.LabelField(_primitive.name, _titleStyle);
            int i = 0;
            foreach (var field in fields)
            {
                GUI.backgroundColor = i % 2 == 0 ? Color.white : Color.gray;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(field.Name);
                EditorGUILayout.LabelField(field.GetValue(_primitive.data).ToString());
                EditorGUILayout.EndHorizontal();
                i++;
            }

            long memoryUsed = Profiler.GetRuntimeMemorySizeLong(_primitive.mesh);
            GUI.backgroundColor = i % 2 == 0 ? Color.white : Color.gray;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Mesh Memory");
            EditorGUILayout.LabelField((memoryUsed / 1000000f).ToString() + "mb");
            EditorGUILayout.EndHorizontal();
        }

        void RenderResourceLoaders()
        {
            if (OvrAvatarManager.Instance != null)
            {
                var _resourcesByID = OvrAvatarManager.Instance.GetResourceID();
                EditorGUILayout.LabelField("Resource Loaders");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Texture Memory: " + (_totalTextureMemoryUsed / 1000000f) + "mb");
                EditorGUILayout.LabelField("Mesh Memory: " + (_totalMeshMemoryUsed / 1000000f) + "mb");
                EditorGUILayout.EndHorizontal();
                _totalTextureMemoryUsed = 0;
                _totalMeshMemoryUsed = 0;

                int i = 0;
                foreach (var kvp in _resourcesByID)
                {
                    long totalMemoryPerLoader = 0;

                    var primitives = kvp.Value.Primitives;
                    var images = kvp.Value.Images;
                    GUI.backgroundColor = i % 2 == 0 ? Color.white : Color.gray;
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(kvp.Key.ToString(), GUILayout.MaxWidth(50));
                    EditorGUILayout.BeginVertical();
                    foreach (var p in primitives)
                    {
                        long memoryUsed = Profiler.GetRuntimeMemorySizeLong(p.mesh);
                        totalMemoryPerLoader += memoryUsed;
                        if (GUILayout.Button(p.name))
                        {
                            _primitive = p;
                        }
                        _totalMeshMemoryUsed += memoryUsed;
                    }
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    foreach (var im in images)
                    {
                        long memoryUsed = Profiler.GetRuntimeMemorySizeLong(im.texture);
                        _totalTextureMemoryUsed += memoryUsed;
                        totalMemoryPerLoader += memoryUsed;
                        EditorGUILayout.BeginVertical();
                        var rect = GUILayoutUtility.GetRect(128, 128);
                        EditorGUI.DrawPreviewTexture(rect, im.texture);
                        EditorGUILayout.LabelField((memoryUsed / 1000000f).ToString() + "mb");
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxWidth(82));
                    EditorGUILayout.LabelField("Total Memory:", GUILayout.MaxWidth(82));
                    EditorGUILayout.LabelField((totalMemoryPerLoader / 1000000f).ToString() + "mb", GUILayout.MaxWidth(82));
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                    i++;
                }
            }
        }

        void RenderGPUSkinningData()
        {
            var textures = Resources.FindObjectsOfTypeAll<Texture>();


            var inputs = textures.Where(t =>
            {
                foreach (var p in _inputPrefixes)
                {
                    if (t.name.StartsWith(p))
                    {
                        return true;
                    }
                }
                return false;
            });

            var outputs = textures.Where(t =>
            {
                foreach (var p in _outputPrefixes)
                {
                    if (t.name.StartsWith(p))
                    {
                        return true;
                    }
                }
                return false;
            });

            EditorGUILayout.LabelField("Inputs");
            foreach (var t in inputs)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(t, typeof(Texture), true);

                textureLabelField(t);
                EditorGUILayout.LabelField(t.filterMode.ToString(), GUILayout.MaxWidth(50));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField("Outputs");
            foreach (var t in outputs)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(t, typeof(Texture), true);

                textureLabelField(t);
                EditorGUILayout.LabelField($"{t.graphicsFormat}");
                EditorGUILayout.LabelField(t.filterMode.ToString(), GUILayout.MaxWidth(50));

                EditorGUILayout.EndHorizontal();

            }
        }

        private static void textureLabelField(Texture t)
        {
            switch (t.dimension)
            {
                case TextureDimension.Tex3D:
                case TextureDimension.Tex2DArray:
                    EditorGUILayout.LabelField($"{t.width}x{t.height}^{getTextureSliceCount(t)}");
                    break;
                default:
                    EditorGUILayout.LabelField($"{t.width}x{t.height}");
                    break;
            }
        }

        private static int getTextureSliceCount(Texture t)
        {
            if (t == null) { return 0; }
            if (t is RenderTexture rT) { return rT.volumeDepth; }
            if (t is Texture2DArray aT) { return aT.depth; }

            OvrAvatarLog.LogError("Unrecognized texture type", LOG_SCOPE, t);
            return 1;
        }

        private const string LOG_SCOPE = "AvatarResourcesWindow";
    }
}
