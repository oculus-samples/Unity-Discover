// Copyright (c) Meta Platforms, Inc. and affiliates.

namespace ColocationPackage {
  public static class NetworkAdapter {
    public static INetworkData NetworkData { get; set; }

    public static INetworkMessenger NetworkMessenger { get; set; }

    public static void SetConfig(INetworkData networkData, INetworkMessenger networkMessenger) {
      NetworkData = networkData;
      NetworkMessenger = networkMessenger;
    }
  }
}
