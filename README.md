![Discover Banner](./Documentation/Media/banner.png "Discover")

# Discover

Discover is a Mixed Reality (MR) project that demonstrates how to use key MR features and quickly integrate them in your own project.

This codebase is available both as a reference and as a template for MR projects. The [Oculus License](LICENSE) applies to the SDK and supporting material. The MIT License applies to only certain, clearly marked documents. If an individual file does not indicate which license it is subject to, then the Oculus License applies.

See the [CONTRIBUTING](./CONTRIBUTING.md) file for how to help out.

This project was built using the [Unity engine](https://unity.com/) with [Photon Fusion](https://doc.photonengine.com/fusion/current/getting-started/fusion-intro).

You will be able to test the game out on AppLab - Discover (COMING SOON).

## Project Description

In this project you can see how we use Scene API, Interaction SDK, Passthrough, Spatial Anchors and Shared Spatial Anchors.

The project also includes the [Meta Utilities](./Packages/com.meta.utilities/README.md) and [Meta Input Utilities](./Packages/com.meta.utilities.input/README.md) packages, which contain many useful tools and methods.

## How to run the project in Unity

1. [Configure the project](./Documentation/Configuration.md) with Meta Quest and Photon
2. Make sure you're using  *Unity 2022.2.12f1* or newer.
3. Load the [Assets/Discover/Scenes/Discover](./Assets/Discover/Scenes/Discover.unity) scene.
4. To test in Editor you will need to use Quest Link:
    <details>
      <summary><b>Quest Link</b></summary>

    - Enable Quest Link:
        - Put on your headset and navigate to "Quick Settings"; select "Quest Link" (or "Quest Air Link" if using Air Link).
        - Select your desktop from the list and then select, "Launch". This will launch the Quest Link app, allowing you to control your desktop from your headset.
    - With the headset on, select "Desktop" from the control panel in front of you. You should be able to see your desktop in VR!
    - Navigate to Unity and press "Play" - the application should launch on your headset automatically.
    </details>

## Dependencies

This project makes use of the following plugins and software:

- [Unity](https://unity.com/download) 2022.2.12f1 or newer
- [Dependencies Hunter](https://github.com/AlexeyPerov/Unity-Dependencies-Hunter.git#upm)
- [Meta Avatars SDK](https://developer.oculus.com/downloads/package/meta-avatars-sdk/)
- [Meta XR Utilities](https://developer.oculus.com/documentation/unity/unity-package-manager/)
- [Meta XR Platform SDK](https://developer.oculus.com/documentation/unity/ps-platform-intro/)
- [Meta XR Interaction SDK](https://developer.oculus.com/documentation/unity/unity-isdk-interaction-sdk-overview/)
- [ParrelSync](https://github.com/brogan89/ParrelSync)
- [Photon Fusion](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)
- [Photon Voice 2](https://assetstore.unity.com/packages/tools/audio/photon-voice-2-130518)
- [Unity Toolbar Extender](https://github.com/marijnz/unity-toolbar-extender.git)

The following is required to test this project within Unity:

- [The Oculus App](https://www.oculus.com/setup/)

# Getting the code

First, ensure you have Git LFS installed by running this command:

```sh
git lfs install
```

Then, clone this repo using the "Code" button above, or this command:

```sh
git clone https://github.com/oculus-samples/Unity-Discover.git
```

# Documentation

More information can be found in the [Documentation](./Documentation) section of this project.

- [Configuration](./Documentation/Configuration.md)
- [Discover Overview](./Documentation/DiscoverOverview.md)
- [Project Structure](./Documentation/ProjectStructure.md)

Custom Packages:

- [Meta Colocation](./Packages/com.meta.xr.sdk.colocation)
- [Meta Utilities](./Packages/com.meta.utilities/README.md)
- [Meta Input Utilities](./Packages/com.meta.utilities.input/README.md)
- [Meta Avatars Utilities](./Packages/com.meta.utilities.avatars/README.md)
