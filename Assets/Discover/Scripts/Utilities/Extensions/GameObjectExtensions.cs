// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Discover.Utilities.Extensions
{
    public static class GameObjectExtensions
    {

        public static Bounds GetCombinedRendererBounds(this GameObject gameObject)
        {
            var objectBounds = new Bounds();
            var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>();
            if (meshRenderers.Length > 0)
            {
                objectBounds = meshRenderers[0].bounds;
                for (var i = 1; i < meshRenderers.Length; ++i)
                {
                    objectBounds.Encapsulate(meshRenderers[i].bounds);
                }
                return objectBounds;
            }
            return objectBounds;
        }

        public static Bounds GetCombinedColliderBounds(this GameObject gameObject)
        {
            var objectBounds = new Bounds();
            var colliders = gameObject.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                objectBounds = colliders[0].bounds;
                for (var i = 1; i < colliders.Length; ++i)
                {
                    objectBounds.Encapsulate(colliders[i].bounds);
                }
                return objectBounds;
            }
            return objectBounds;
        }

        public static void SetLayerRecursively(this GameObject gameObject, int layer)
        {
            gameObject.layer = layer;

            var transform = gameObject.transform;
            for (var i = 0; i < transform.childCount; ++i)
            {
                SetLayerRecursively(transform.GetChild(i).gameObject, layer);
            }
        }

        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}
