// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;

namespace ColocationPackage {
  public interface INetworkMessenger {
    public void SendMessageUsingOculusId(byte eventCode, ulong oculusId, object messageData = null);
    public void SendMessageUsingNetworkId(byte eventCode, int networkId, object messageData = null);
    public void SendMessageUsingHeadsetId(byte eventCode, Guid headsetId, object messageData = null);
    public void SendMessageToAll(byte eventCode, object messageData = null);
    public void RegisterEventCallback(byte eventCode, Action<object> callback);
    public void UnregisterEventCallback(byte eventCode);
  }
}
