// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ColocationPackage {
  public class ColocationLauncher {
    private AlignmentAnchorManager _alignmentAnchorManager;
    private UniTaskCompletionSource<bool> _alignToAnchorTask;
    private OVRSpatialAnchor _myAlignmentAnchor;

    private ulong _myOculusId;

    private Guid _myHeadsetGuid;
    // Note this should be assuming one person comes at a time for now
    // We can implement batching later on but for now ignore it

    private INetworkData _networkData;
    private INetworkMessenger _networkMessenger;

    private ulong _oculusIdToColocateTo;
    private SharedAnchorManager _sharedAnchorManager;

    private UniTaskCompletionSource _createColocatedSpaceTask;

    private Dictionary<CaapEventCode, byte> _caapEventCodeDictionary;

    public bool CreateAnchorIfColocationFailed { get; set; }
    public Action OnAutoColocationFailed;

    public void Init(
      ulong myOculusId,  
      Guid myHeadsetGuid,  
      INetworkData networkData,
      INetworkMessenger networkMessenger,
      SharedAnchorManager sharedAnchorManager,
      AlignmentAnchorManager alignmentAnchorManager,
      Dictionary<CaapEventCode, byte> overrideEventCode = null
    ) {
      Debug.Log($"ColocationLauncher: Init function called oculusId:{myOculusId} headsetGuid:{myHeadsetGuid}");
      _myOculusId = myOculusId;
      _myHeadsetGuid = myHeadsetGuid;
      _networkData = networkData;
      _networkMessenger = networkMessenger;

      _caapEventCodeDictionary = CreateCaapEventCodeDictionary(overrideEventCode);

      _networkMessenger.RegisterEventCallback(_caapEventCodeDictionary[CaapEventCode.TellOwnerToShareAnchor], TellOwnerToShareAnchor);

      _networkMessenger.RegisterEventCallback(
        _caapEventCodeDictionary[CaapEventCode.TellAnchorRequesterToLocalizeAnchor],
        TellAnchorRequesterToLocalizeAnchor
      );

      _sharedAnchorManager = sharedAnchorManager;
      _alignmentAnchorManager = alignmentAnchorManager;
    }

    public void RegisterOnAfterColocationReady(Action action) {
      _alignmentAnchorManager.OnAfterAlignment += action;
    }

    public void UnregisterOnAfterColocationReady(Action action) {
      _alignmentAnchorManager.OnAfterAlignment -= action;
    }

    public void DestroyAlignementAnchor()
    {
      if (_myAlignmentAnchor != null)
      {
        GameObject.Destroy(_myAlignmentAnchor.gameObject);
      }
    }

    public void OnPlayerLeft(ulong oculusId)
    {
      var player = _networkData.GetPlayer(oculusId);
      if (player.HasValue)
      {
        _networkData.RemovePlayer(player.Value);
      }
    }

    private Dictionary<CaapEventCode,byte> CreateCaapEventCodeDictionary(Dictionary<CaapEventCode, byte> overrideEventCode) {
      Dictionary<CaapEventCode, byte> dictionary = new Dictionary<CaapEventCode, byte>();
      dictionary.Add(CaapEventCode.TellOwnerToShareAnchor, (byte) CaapEventCode.TellOwnerToShareAnchor);
      dictionary.Add(CaapEventCode.TellAnchorRequesterToLocalizeAnchor, (byte) CaapEventCode.TellAnchorRequesterToLocalizeAnchor);

      if (overrideEventCode == null) {
        return dictionary;
      }

      if (overrideEventCode.ContainsKey(CaapEventCode.TellOwnerToShareAnchor)) {
        dictionary[CaapEventCode.TellOwnerToShareAnchor] = overrideEventCode[CaapEventCode.TellOwnerToShareAnchor];
      }

      if (overrideEventCode.ContainsKey(CaapEventCode.TellAnchorRequesterToLocalizeAnchor)) {
        dictionary[CaapEventCode.TellAnchorRequesterToLocalizeAnchor] =
          overrideEventCode[CaapEventCode.TellAnchorRequesterToLocalizeAnchor];
      }

      return dictionary;
    }

    public void ColocateAutomatically() {
     ExecuteAction(ColocationMethod.ColocateAutomatically);
    }

    public void ColocateByPlayerWithOculusId(ulong oculusId) {
      ExecuteAction(ColocationMethod.ColocateByPlayerWithOculusId);
    }

    public void CreateColocatedSpace() {
      ExecuteAction(ColocationMethod.CreateColocatedSpace);
    }

    private void ExecuteAction(ColocationMethod colocationMethod) {
      switch (colocationMethod) {
        case ColocationMethod.ColocateAutomatically:
          ColocateAutomaticallyInternal();
          break;
        case ColocationMethod.ColocateByPlayerWithOculusId:
          ColocateByPlayerWithOculusIdInternal(_oculusIdToColocateTo);
          break;
        case ColocationMethod.CreateColocatedSpace:
          CreateColocatedSpaceInternal();
          break;
        default:
          Debug.LogError($"ColocationLauncher: Unknown action: {colocationMethod}");
          break;
      }
    }

    private bool IsValidOculusId(ulong oculusId) {
      return oculusId != 0;
    }

    private async void ColocateAutomaticallyInternal() {
      Debug.Log("ColocationLauncher: Called Init Anchor Flow");
      var successfullyAlignedToAnchor = false;

      List<Anchor> alignmentAnchors = GetAllAlignmentAnchors();
      foreach (var anchor in alignmentAnchors)
        if (await AttemptToShareAndLocalizeToAnchor(anchor)) {
          successfullyAlignedToAnchor = true;
          Debug.Log($"ColocationLauncher: successfully aligned to anchor with id: {anchor.uuid}");
          _networkData.AddPlayer(new Player(_myOculusId, anchor.colocationGroupId));
          AlignPlayerToAnchor();
          break;
        }

      if (!successfullyAlignedToAnchor) {
        if (CreateAnchorIfColocationFailed)
        {
          CreateNewColocatedSpace().Forget();
        }
        else
        {
          OnAutoColocationFailed?.Invoke();
        }
      }
    }

    private async void ColocateByPlayerWithOculusIdInternal(ulong oculusId) {
      Anchor? anchorToAlignTo = FindAlignmentAnchorUsedByOculusId(oculusId);
      if (anchorToAlignTo == null) {
        Debug.LogError($"Unable to find alignment anchor used by oculusId {oculusId}");
        return;
      }

      bool result = await AttemptToShareAndLocalizeToAnchor(anchorToAlignTo.Value);
      if (result) {
        Debug.Log($"ColocationLauncher: successfully aligned to anchor with id: {anchorToAlignTo.Value.uuid}");
        _networkData.AddPlayer(new Player(_myOculusId, anchorToAlignTo.Value.colocationGroupId));
      } else {
        Debug.LogError("ColocationLauncher: ColocateByPlayerWithOculusIdInternal: Failed to ShareAndLocalizeToAnchor");
        return;
      }

      AlignPlayerToAnchor();
    }

    private Anchor? FindAlignmentAnchorUsedByOculusId(ulong oculusId) {
      List<Player> players = _networkData.GetAllPlayers();
      uint? colocationGroupId = null;

      foreach (var player in players)
        if (oculusId == player.oculusId) {
          colocationGroupId = player.colocationGroupId;
        }

      if (colocationGroupId == null) {
        Debug.LogError($"Could not find the colocated group belonging to oculusId: {oculusId}");
        return null;
      }

      List<Anchor> anchors = _networkData.GetAllAnchors();
      foreach (var anchor in anchors)
        if (colocationGroupId.Value == anchor.colocationGroupId) {
          return anchor;
        }

      Debug.LogError($"Could not find the anchor belonging on colocationGroupId: {colocationGroupId}");
      return null;
    }

    private void CreateColocatedSpaceInternal() {
      CreateNewColocatedSpace().Forget();
    }

    private async UniTaskVoid CreateNewColocatedSpace() {
      _myAlignmentAnchor = await CreateAlignmentAnchor();
      if (_myAlignmentAnchor == null) {
        Debug.LogError("ColocationLauncher: Could not create the anchor");
        return;
      }

      uint newColocationGroupdId = _networkData.GetColocationGroupCount();
      _networkData.IncrementColocationGroupCount();
      _networkData.AddAnchor(new Anchor(true, _myAlignmentAnchor.Uuid.ToString(), _myOculusId, newColocationGroupdId));
      _networkData.AddPlayer(new Player(_myOculusId, newColocationGroupdId));
      AlignPlayerToAnchor();
      await UniTask.Yield();
    }

    private void AlignPlayerToAnchor() {
      Debug.Log("ColocationLauncher: AlignPlayerToAnchor was called");
      _alignmentAnchorManager.AlignPlayerToAnchor(_myAlignmentAnchor);
    }

    private List<Anchor> GetAllAlignmentAnchors() {
      var alignmentAnchors = new List<Anchor>();
      List<Anchor> allAnchors = _networkData.GetAllAnchors();
      foreach (var anchor in allAnchors)
        if (anchor.isAlignmentAnchor) {
          alignmentAnchors.Add(anchor);
        }

      return alignmentAnchors;
    }

    private UniTask<bool> AttemptToShareAndLocalizeToAnchor(Anchor anchor) {
      Debug.Log(
        $"ColocationLauncher: Called AttemptToShareAndLocalizeToAnchor with id: {anchor.uuid} and oculusId: {_myOculusId}"
      );
      _alignToAnchorTask = new UniTaskCompletionSource<bool>();
      var anchorOwner = anchor.ownerOculusId;
      if (anchorOwner == _myOculusId)
      {
        // In the case a player returns and wants to localize to an anchor they created
        // we simply localize to that anchor
        var sharedAnchorId = new Guid(anchor.uuid.ToString());
        LocalizeAnchor(sharedAnchorId);
        return _alignToAnchorTask.Task;
      }
      Debug.Log($"ColocationLauncher: Attempt to send it to id: {anchorOwner}");
      if (!_networkData.GetPlayer(anchor.ownerOculusId).HasValue)
      {
        Debug.Log($"ColocationLauncher: Anchor owner {anchorOwner} not found");
        var colocationGroup = anchor.colocationGroupId;
        var newOwner = _networkData.GetFirstPlayerInColocationGroup(colocationGroup);
        if (newOwner.HasValue)
        {
          anchorOwner = newOwner.Value.oculusId;
          Debug.Log($"ColocationLauncher: Different anchor owner found {anchorOwner}, Attempt to send it");
        }
        else
        {
          Debug.LogError($"ColocationLauncher: No owner found for anchor {anchor.uuid} with Colocation group {colocationGroup}. Colocation Aborted.");
          _alignToAnchorTask.TrySetResult(false);
          return _alignToAnchorTask.Task;
        }
      }
      var data = new ShareAndLocalizeParams(anchorOwner, _myOculusId, _myHeadsetGuid, anchor.uuid.ToString());
      _networkMessenger.SendMessageUsingOculusId(
        _caapEventCodeDictionary[CaapEventCode.TellOwnerToShareAnchor],
        anchorOwner,
        data
      );

      return _alignToAnchorTask.Task;
    }

    private async void TellOwnerToShareAnchor(object data) {
      Debug.Log($"ColocationLauncher: TellOwnerToShareAnchor with oculusId: {_myOculusId}");
      var shareAndLocalizeParams = (ShareAndLocalizeParams) data;
      ulong requestedAnchorOculusId = shareAndLocalizeParams.oculusIdAnchorRequester;
      bool isAnchorSharedSuccessfully = await _sharedAnchorManager.ShareAnchorsWithUser(requestedAnchorOculusId);

      shareAndLocalizeParams.anchorFlowSucceeded = isAnchorSharedSuccessfully;

      Debug.Log($"ColocationLauncher: Anchor Shared: {isAnchorSharedSuccessfully}");
      _networkMessenger.SendMessageUsingHeadsetId(
        _caapEventCodeDictionary[CaapEventCode.TellAnchorRequesterToLocalizeAnchor],
        shareAndLocalizeParams.headsetIdAnchorRequester,
        shareAndLocalizeParams
      );
    }

    private void TellAnchorRequesterToLocalizeAnchor(object messageData) {
      var data = (ShareAndLocalizeParams) messageData;
      Debug.Log($"ColocationLauncher: TellAnchorRequesterToLocalizeAnchor was called oculusId: {_myOculusId}");
      bool isAnchorSharingSuccessful = data.anchorFlowSucceeded;

      if (!isAnchorSharingSuccessful) {
        _alignToAnchorTask.TrySetResult(false);
        return;
      }

      var sharedAnchorId = new Guid(data.uuid.ToString());
      LocalizeAnchor(sharedAnchorId);
    }

    private async UniTask<OVRSpatialAnchor> CreateAlignmentAnchor() {
      var anchor = await _sharedAnchorManager.CreateAnchor(Vector3.zero, Quaternion.identity);
      if (anchor == null) {
        Debug.Log("ColocationLauncher: _sharedAnchorManager.CreateAnchor returned null");
      }

      Debug.Log($"ColocationLauncher: Anchor created: {anchor?.Uuid}");

      bool isAnchorSavedToCloud = await _sharedAnchorManager.SaveLocalAnchorsToCloud();
      if (!isAnchorSavedToCloud) {
        Debug.LogError("ColocationLauncher: We did not save the local anchor to the cloud");
      } else {
        Debug.Log("ColocationLauncher: Local Anchor was saved successfully");
      }

      return anchor;
    }

    private async void LocalizeAnchor(Guid anchorToLocalize) {
      Debug.Log($"ColocationLauncher: Localize Anchor Called id: {_myOculusId}");
      IReadOnlyList<OVRSpatialAnchor> sharedAnchors = null;
      Guid[] anchorIds = {anchorToLocalize};
      sharedAnchors = await _sharedAnchorManager.RetrieveAnchors(anchorIds);
      if (sharedAnchors == null || sharedAnchors.Count == 0) {
        Debug.LogError("ColocationLauncher: Retrieving Anchors Failed");
        _alignToAnchorTask.TrySetResult(false);
      } else {
        Debug.Log("ColocationLauncher: Localizing Anchors is Successful");
        // For now we will only take the first anchor that was shared
        // This should be refactored later to be more generic or for cases where we have multiple alignment anchors if that case comes up
        _myAlignmentAnchor = sharedAnchors[0];
        _alignToAnchorTask.TrySetResult(true);
      }
    }
  }
}
