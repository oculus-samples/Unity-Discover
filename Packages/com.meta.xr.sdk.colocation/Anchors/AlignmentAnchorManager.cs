// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using UnityEngine;

namespace ColocationPackage {
  public class AlignmentAnchorManager : MonoBehaviour {
    private Coroutine _alignmentCoroutine;
    private OVRSpatialAnchor _anchorToAlignTo;
    private Transform _cameraRigTransform;
    private Transform _playerHandsTransform;

    public Action OnAfterAlignment;

    private void OnDestroy() {
      if (_alignmentCoroutine != null) {
        StopCoroutine(_alignmentCoroutine);
        _alignmentCoroutine = null;
      }
    }

    private void OnEnable()
    { 
      if (OVRManager.display != null) {
        OVRManager.display.RecenteredPose += RealignToAnchor;
      }
    }

    private void OnDisable()
    {
      if (OVRManager.display != null) {
        OVRManager.display.RecenteredPose -= RealignToAnchor;
      }
    }

    public void Init(Transform cameraRigTransform, Transform playerHandsTransform = null) {
      Debug.Log("AlignmentAnchorManager: Called Init function");
      _cameraRigTransform = cameraRigTransform;
      _playerHandsTransform = playerHandsTransform;
      OnAfterAlignment += () => { };
    }

    public void AlignPlayerToAnchor(OVRSpatialAnchor anchor) {
      Debug.Log("AlignmentAnchorManager: Called AlignPlayerToAnchor");
      if (_alignmentCoroutine != null) {
        StopCoroutine(_alignmentCoroutine);
        _alignmentCoroutine = null;
      }

      _alignmentCoroutine = StartCoroutine(AlignmentCoroutine(anchor, 2));
    }

    private void RealignToAnchor()
    {
      if (_anchorToAlignTo != null)
      {
        AlignPlayerToAnchor(_anchorToAlignTo);
      }
    }
    
    private IEnumerator AlignmentCoroutine(OVRSpatialAnchor anchor, int alignmentCount) {
      Debug.Log("AlignmentAnchorManager: called AlignmentCoroutine");
      while (alignmentCount > 0) {
        if (_anchorToAlignTo != null) {
          _cameraRigTransform.position = Vector3.zero;
          _cameraRigTransform.eulerAngles = Vector3.zero;

          yield return null;
        }

        var anchorTransform = anchor.transform;
        if (_cameraRigTransform != null) {
          Debug.Log("AlignmentAnchorManager: CameraRigTransform is valid");
          _cameraRigTransform.position = anchorTransform.InverseTransformPoint(Vector3.zero);
          _cameraRigTransform.eulerAngles = new Vector3(0, -anchorTransform.eulerAngles.y, 0);
        }

        if (_playerHandsTransform != null) {
          _playerHandsTransform.localPosition = -_cameraRigTransform.position;
          _playerHandsTransform.localEulerAngles = -_cameraRigTransform.eulerAngles;
        }

        _anchorToAlignTo = anchor;
        alignmentCount--;
        yield return new WaitForEndOfFrame();
      }

      Debug.Log("AlignmentAnchorManager: Finished Alignment!");
      OnAfterAlignment.Invoke();
    }
  }
}
