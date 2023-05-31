// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Discover.SpatialAnchors
{
    public class SpatialAnchorManager<TData> where TData : SpatialAnchorSaveData
    {
        private Dictionary<Guid, TData> m_anchorUidToData;
        private List<TData> m_anchorSavedData;
        private ISpatialAnchorFileManager<TData> m_fileManager;

        public Func<TData, GameObject> OnAnchorDataLoadedCreateGameObject;

        public SpatialAnchorManager(ISpatialAnchorFileManager<TData> fileManager)
        {
            m_fileManager = fileManager;
            m_anchorUidToData = new Dictionary<Guid, TData>();
            m_anchorSavedData = new List<TData>();
        }

        public async void SaveAnchor(OVRSpatialAnchor anchor, TData data)
        {
            await UniTask.WaitUntil(() => anchor.Uuid != Guid.Empty);
            anchor.Save((savedAnchor, b) =>
            {
                Debug.Log($"Anchor with {AnchorUtils.GuidToString(anchor.Uuid)} saved");
                m_anchorUidToData[savedAnchor.Uuid] = data;
                OnSpaceSaveComplete(anchor.Uuid, data);
            });
        }

        public void EraseAnchor(
            OVRSpatialAnchor anchor, bool saveOnErase = true, Action<OVRSpatialAnchor, bool> onAnchorErased = null)
        {
            var uuid = anchor.Uuid;
            anchor.Erase((erasedAnchor, success) =>
            {
                if (success)
                {
                    Debug.Log($"Erased anchor data {erasedAnchor.Uuid}");
                    var dataToRemove = m_anchorSavedData.Find(data => data.AnchorUuid == uuid);
                    _ = m_anchorSavedData.Remove(dataToRemove);
                    _ = m_anchorUidToData.Remove(uuid);
                    if (saveOnErase)
                    {
                        SaveToFile();
                    }
                }
                else
                {
                    Debug.LogError($"Failed to erased anchor data {erasedAnchor.Uuid}");
                }

                onAnchorErased?.Invoke(erasedAnchor, success);
            });
        }

        public void LoadAnchors()
        {
            m_anchorSavedData = m_fileManager.ReadDataFromFile();
            var anchorsToQuery = new HashSet<Guid>();
            foreach (var data in m_anchorSavedData)
            {
                _ = anchorsToQuery.Add(data.AnchorUuid);
                m_anchorUidToData[data.AnchorUuid] = data;
            }

            if (anchorsToQuery.Count < 1)
            {
                Debug.Log("No anchors to load");
                return;
            }

            QueryAnchors(anchorsToQuery);
        }

        private void QueryAnchors(HashSet<Guid> anchorUuids)
        {
            var anchorIds = anchorUuids.ToList();

            Debug.Log($"Querying for anchors {anchorIds.Count}");
            var options = new OVRSpatialAnchor.LoadOptions
            {
                StorageLocation = OVRSpace.StorageLocation.Local,
                Uuids = anchorIds,
                Timeout = 0,
            };
            _ = OVRSpatialAnchor.LoadUnboundAnchors(options, OnCompleteUnboundAnchors);
        }

        private void OnCompleteUnboundAnchors(OVRSpatialAnchor.UnboundAnchor[] unboundAnchors)
        {
            if (unboundAnchors == null)
                return;

            foreach (var queryResult in unboundAnchors)
            {
                Debug.Log($"Initializing app with guid {AnchorUtils.GuidToString(queryResult.Uuid)}");
                var appData = m_anchorUidToData[queryResult.Uuid];
                var gameObject = OnAnchorDataLoadedCreateGameObject(appData);
                var anchor = gameObject.AddComponent<OVRSpatialAnchor>();
                queryResult.BindTo(anchor);
            }
        }

        private void OnSpaceSaveComplete(Guid anchorUuid, TData data)
        {
            data.AnchorUuid = anchorUuid;
            m_anchorSavedData.Add(data);

            SaveToFile();
        }

        private void SaveToFile()
        {
            m_fileManager.WriteDataToFile(m_anchorSavedData);
        }

        public void ClearData()
        {
            m_anchorSavedData.Clear();
            m_anchorUidToData.Clear();
            SaveToFile();
        }
    }
}