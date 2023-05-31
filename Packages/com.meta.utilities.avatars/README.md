# Meta Avatar Utilities Package

This package contains the implementation of the AvatarEntity class used in many Meta XR samples. It is used to integrate the [Meta Avatars SDK](https://developer.oculus.com/documentation/unity/meta-avatars-overview/) into networked Unity projects. It also contains utilities for querying information about an Avatar's mesh, such as characteristics about its arms.

You can integrate this package into your own project by using the Package Manager to [add the following Git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html):

```txt
https://github.com/oculus-samples/Unity-Discover.git?path=/Packages/com.meta.utilities.avatars
```

## Contents

|Script|Description|
|-|-|
|[AvatarEntity](./Avatar/AvatarEntity.cs)|Implementation of the OvrAvatarEntity that sets up the avatar based on the user ID, integrates the body tracking, events on joints loaded, hide and show avatar, as well as local and remote setup.|
|[AvatarMeshUtils](./Avatar/AvatarMeshUtils/README.md)|Information about the Avatar mesh querying system can be found here.|
