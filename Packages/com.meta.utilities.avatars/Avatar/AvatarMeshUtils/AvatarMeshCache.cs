// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Concurrent;
using System.Linq;
using Meta.Utilities;
using Oculus.Avatar2;
using UnityEngine;

namespace Meta.Utilities.Avatars
{
    public class AvatarMeshCache : Singleton<AvatarMeshCache>
    {
        [SerializeField, AutoSet]
        private OvrAvatarManager m_avatarManager;

        public struct MeshData
        {
            public Vector3[] Positions { get; set; }
            public BoneWeight[] Weights { get; set; }
        }

        private ConcurrentDictionary<int, MeshData> m_meshes = new();

        private string CachedAssetIds => m_meshes.Keys.ListToString();

        public event System.Action<OvrAvatarPrimitive> OnMeshLoaded;

        private new void OnEnable()
        {
            base.OnEnable();
            m_avatarManager.OnAvatarMeshLoaded += OnAvatarMeshLoadedCallback;
        }

        private void OnDisable()
        {
            m_avatarManager.OnAvatarMeshLoaded -= OnAvatarMeshLoadedCallback;
        }

        private void OnAvatarMeshLoadedCallback(OvrAvatarManager mgr, OvrAvatarPrimitive prim, OvrAvatarManager.MeshData mesh)
        {
            if (mesh.BoneWeights.Length > 0 && mesh.Positions.Length > 0)
            {
                m_meshes[(int)prim.assetId] = new() { Positions = mesh.Positions.ToArray(), Weights = mesh.BoneWeights.ToArray() };
                OnMeshLoaded?.Invoke(prim);
            }
        }

        public MeshData? GetMeshData(OvrAvatarPrimitive prim) =>
            m_meshes.TryGetValue((int)prim.assetId, out var data) ? data : null;
    }
}
