# Colocation Package

This package implement a simple way to do colocation in a Mixed Reality project. This implements the core logic to use [Shared Spatial Anchors](https://developer.oculus.com/documentation/unity/unity-shared-spatial-anchors/) and an interface to the networking layer to enable colocation.

You can integrate this package into your own project by using the Package Manager to [add the following Git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html):

```txt
https://github.com/oculus-samples/Unity-Discover.git?path=/Packages/com.meta.xr.sdk.colocation
```

## Interface Implementation

### INetworkData

This will contain the information about all players and anchors created as well as the number of colocated groups.
The list of players and anchors will be shared/networked to all users.

### INetworkMessenger

This implementation handles the messaging from clients to clients. Sending RPCs to the right users to share the Shared Spatial Anchor with them and send back that the anchor was shared.

## Shared Anchor Manager

This handles creating, saving and sharing of the anchors on the cloud.

## Alignment Anchor Manager

Handles aligning the player to the given anchor. This is how we keep everyone aligned in the space. We place the players in the frame of reference of the shared anchor.

## How it works

You will have to create an instance of the Colocation Launcher and initialize it. This is where the interface elements will need to be assigned to as well as the Shared Anchor Manager and the Alignement Anchor Manager.
Once initialized, we can set the callback for when colocation is ready and then call colocation.

There are 3 types of colocation:

- **ColocateAutomatically**: It tries to find an existing anchor and colocate to it, if it fails it can either create one or callback that it fails. (see ColocationLauncher.CreateAnchorIfColocationFailed)
- **CreateColocatedSpace**: Create a shared anchor and share it
- **ColocateByPlayerWithOculusId**: Colocate to a specific user

Overall the communication goes like this:

1. *UserA*: Creates a shared anchor and saves it in INetworkData.
2. *UserB*: Checks if there is anchors in INetworkData, if there is asks *UserA* (through INetworkMessenger) to share the anchor with them.
3. *UserA* shares the anchor and tells *UserB* that the anchor was shared.
4. *UserB* gets the shared anchor, loads and localizes it, and finally aligns to it.
5. *UserA* and *UserB* are colocated and have the same frame of reference.
