using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SkeletonRenderer : MonoBehaviour
{
    public Color color;

    public bool drawAxes;
    public float axisSize;

#if UNITY_EDITOR
    private void OnEnable()
    {
#if UNITY_2019_3_OR_NEWER
        SceneView.duringSceneGui += Draw;
#else
        SceneView.onSceneGUIDelegate += Draw;
#endif
    }

    private void OnDisable()
    {
#if UNITY_2019_3_OR_NEWER
        SceneView.duringSceneGui -= Draw;
#else
        SceneView.onSceneGUIDelegate -= Draw;
#endif
    }
#endif

#if UNITY_EDITOR
    private void Draw(SceneView sceneView)
    {
        Handles.matrix = Matrix4x4.identity;

        foreach (var xform in GetComponentsInChildren<Transform>())
        {
            var parent = xform.parent;
            var position = xform.position;
            if (parent)
            {
                Handles.color = color;
                Handles.DrawLine(position, parent.position);
            }

            if (drawAxes)
            {
                var xAxis = position + xform.rotation * Vector3.right * axisSize ;
                var yAxis = position + xform.rotation * Vector3.up * axisSize ;
                var zAxis = position + xform.rotation * Vector3.forward * axisSize;

                Handles.color = Color.blue;
                Handles.DrawLine(position, zAxis);

                Handles.color = Color.green;
                Handles.DrawLine(position, yAxis);

                Handles.color = Color.red;
                Handles.DrawLine(position, xAxis);
            }
        }
    }
#endif
}
