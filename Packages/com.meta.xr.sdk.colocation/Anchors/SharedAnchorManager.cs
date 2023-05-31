// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace ColocationPackage {
  public class SharedAnchorManager {
    /// <summary>
    ///   Anchors that are created and owned locally
    /// </summary>
    private readonly List<OVRSpatialAnchor> _localAnchors = new();

    /// <summary>
    ///   Anchors that are shared from other clients and relocalized
    /// </summary>
    private readonly List<OVRSpatialAnchor> _sharedAnchors = new();

    /// <summary>
    ///   Users this instance will share locally owned anchors with
    /// </summary>
    private readonly HashSet<OVRSpaceUser> _userShareList = new();

    public GameObject AnchorPrefab { get; set; }
    public IReadOnlyList<OVRSpatialAnchor> LocalAnchors => _localAnchors;

    public async UniTask<OVRSpatialAnchor> CreateAnchor(Vector3 position, Quaternion orientation) {
      Debug.Log("CreateAnchor: Attempt to InstantiateAnchor");
      var anchor = InstantiateAnchor();
      Debug.Log("CreateAnchor: Attempt to Set Position and Rotation of Anchor");
      if (anchor == null) {
        Debug.Log("CreateAnchor: Anchor is null");
      } else if (anchor.transform == null) {
        Debug.Log("CreateAnchor: anchor.transform is null");
      }

      Debug.Log("SharedAnchorManager: CreateAnchor: Before Test");
      await UniTask.WaitWhile(() => anchor.PendingCreation, PlayerLoopTiming.PreUpdate);
      Debug.Log("SharedAnchorManager: CreateAnchor: Past Pending Creation");

      if (!anchor || !anchor.Created) {
        Debug.Log($"{nameof(SharedAnchorManager)}: Anchor creation failed.");
        return null;
      }

      Debug.Log($"{nameof(SharedAnchorManager)}: Created anchor with id {anchor.Uuid}");

      _localAnchors.Add(anchor);
      return anchor;
    }

    public async UniTask<bool> SaveLocalAnchorsToCloud() {
      UniTaskCompletionSource<bool> utcs = new();

      Debug.Log(
        $"{nameof(SharedAnchorManager)}: Saving anchors: {string.Join(", ", _localAnchors.Select(el => el.Uuid))}"
      );

      OVRSpatialAnchor.Save(
        _localAnchors,
        new OVRSpatialAnchor.SaveOptions {Storage = OVRSpace.StorageLocation.Cloud},
        (_, result) => { utcs.TrySetResult(result == OVRSpatialAnchor.OperationResult.Success); }
      );

      return await utcs.Task;
    }

    public async UniTask<IReadOnlyList<OVRSpatialAnchor>> RetrieveAnchors(Guid[] anchorIds) {
      Assert.IsTrue(anchorIds.Length <= OVRPlugin.SpaceFilterInfoIdsMaxSize, "SpaceFilterInfoIdsMaxSize exceeded.");

      UniTaskCompletionSource<IReadOnlyList<OVRSpatialAnchor>> utcs = new();

      Debug.Log($"{nameof(SharedAnchorManager)}: Querying anchors: {string.Join(", ", anchorIds)}");

      OVRSpatialAnchor.LoadUnboundAnchors(
        new OVRSpatialAnchor.LoadOptions {
          StorageLocation = OVRSpace.StorageLocation.Cloud,
          Timeout = 0,
          Uuids = anchorIds
        },
        async unboundAnchors => {
          if (unboundAnchors == null) {
            Debug.LogError(
              $"{nameof(SharedAnchorManager)}: Failed to query anchors - {nameof(OVRSpatialAnchor.LoadUnboundAnchors)} returned null."
            );
            utcs.TrySetResult(null);
            return;
          }

          if (unboundAnchors.Length != anchorIds.Length) {
            Debug.LogError(
              $"{nameof(SharedAnchorManager)}: {anchorIds.Length - unboundAnchors.Length}/{anchorIds.Length} anchors failed to relocalize."
            );
          }

          var createdAnchors = new List<OVRSpatialAnchor>();
          var createTasks = new List<UniTask>();

          // Bind anchors
          foreach (var unboundAnchor in unboundAnchors) {
            var anchor = InstantiateAnchor();
            try {
              unboundAnchor.BindTo(anchor);
              _sharedAnchors.Add(anchor);
              createdAnchors.Add(anchor);
              createTasks.Add(UniTask.WaitWhile(() => anchor.PendingCreation, PlayerLoopTiming.PreUpdate));
            } catch {
              Object.Destroy(anchor);
              throw;
            }
          }

          // Wait for anchors to be created
          await UniTask.WhenAll(createTasks);

          utcs.TrySetResult(createdAnchors);
        }
      );

      return await utcs.Task;
    }

    public async UniTask<bool> ShareAnchorsWithUser(ulong userId) {
      _userShareList.Add(new OVRSpaceUser(userId));

      if (_localAnchors.Count == 0) {
        Debug.Log($"{nameof(SharedAnchorManager)}: No anchors to share.");
        return true;
      }

      OVRSpaceUser[] users = _userShareList.ToArray();
      //Debug.Log($"{nameof(SharedAnchorManager)}: Sharing {_localAnchors.Count} anchors with users: {string.Join(", ", users)}");
      Debug.Log($"{nameof(SharedAnchorManager)}: Sharing {_localAnchors.Count} anchors with users: {userId}");

      UniTaskCompletionSource<bool> utcs = new();
      OVRSpatialAnchor.Share(
        _localAnchors,
        users,
        (_, result) => { utcs.TrySetResult(result == OVRSpatialAnchor.OperationResult.Success); }
      );

      return await utcs.Task;
    }

    public void StopSharingAnchorsWithUser(ulong userId) {
      _userShareList.RemoveWhere(el => el.Id == userId);
    }

    private OVRSpatialAnchor InstantiateAnchor() {
      GameObject anchorGo;
      if (AnchorPrefab != null) {
        anchorGo = Object.Instantiate(AnchorPrefab);
      } else {
        anchorGo = new GameObject();
        anchorGo.AddComponent<OVRSpatialAnchor>();
      }

      anchorGo.name = $"_{anchorGo.name}";

      var anchor = anchorGo.GetComponent<OVRSpatialAnchor>();
      Assert.IsNotNull(anchor, $"{nameof(AnchorPrefab)} must have an OVRSpatialAnchor component attached to it.");
      Debug.Log($"SharedAnchorManager: InstantiateAnchor: anchorGo is real: {anchorGo != null}");
      Debug.Log($"SharedAnchorManager: InstantiateAnchor: anchor is real: {anchor != null}");
      return anchor;
    }

    public void AddAnchorFromHost(OVRSpatialAnchor newAnchor) {
      _localAnchors.Add(newAnchor);
    }
  }
}
