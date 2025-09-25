![Discover Banner](./Documentation/Media/banner.png "Discover")

# Discover

Discover is a Mixed Reality (MR) project demonstrating how to use key MR features and integrate them into your own project.

This codebase serves as both a reference and a template for MR projects. You can test the game on [Meta Horizon Store - Discover](https://www.meta.com/experiences/discover/7041851792509764/).

## Project Description

This project showcases the use of Scene API, Interaction SDK, Passthrough, Spatial Anchors, and Shared Spatial Anchors.

It was built using the [Unity engine](https://unity.com/) with [Photon Fusion](https://doc.photonengine.com/fusion/current/fusion-intro). It also includes the [Meta Utilities](./Packages/com.meta.utilities/README.md), [Meta Input Utilities](./Packages/com.meta.utilities.input/README.md), and [Meta Avatars Utilities](./Packages/com.meta.utilities.avatars/README.md) packages, which contain useful tools and methods.

## How to Run the Project in Unity

1. [Configure the project](./Documentation/Configuration.md) with Meta Quest and Photon
2. Make sure you're using  *Unity 6000.0.50f1* or newer.
3. Load the [Assets/Discover/Scenes/Discover](./Assets/Discover/Scenes/Discover.unity) scene.
4. To test in the Editor, you'll need to use Quest Link:
    <details>
      <summary><b>Quest Link</b></summary>

    - Enable Quest Link:
        - Put on your headset, navigate to "Quick Settings", and select "Quest Link" (or "Quest Air Link" if using Air Link).
        - Select your desktop from the list, then select "Launch". This launches the Quest Link app, allowing you to control your desktop from your headset.
    - With the headset on, select "Desktop" from the control panel in front of you. You should see your desktop in VR.
    - Navigate to Unity and press "Play"; the application should launch on your headset automatically.
    </details>

## Dependencies

This project uses the following plugins and software:

- [Unity](https://unity.com/download) 2022.3.5f1 or newer
- [Dependencies Hunter](https://github.com/AlexeyPerov/Unity-Dependencies-Hunter.git#upm)
- [Meta Avatars SDK](https://developers.meta.com/horizon/downloads/package/meta-avatars-sdk/)
- [Meta XR Utilities](https://developers.meta.com/horizon/documentation/unity/unity-package-manager/)
- [Meta XR Platform SDK](https://developers.meta.com/horizon/documentation/unity/ps-platform-intro/)
- [Meta XR Interaction SDK](https://developers.meta.com/horizon/documentation/unity/unity-isdk-interaction-sdk-overview/)
- [ParrelSync](https://github.com/brogan89/ParrelSync)
- [Photon Fusion](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)
- [Photon Voice 2](https://assetstore.unity.com/packages/tools/audio/photon-voice-2-130518)
- [Unity Toolbar Extender](https://github.com/marijnz/unity-toolbar-extender.git)
- [UniTask](https://github.com/Cysharp/UniTask)
- [NaughtyAttributes](https://github.com/dbrizov/NaughtyAttributes)

To test this project within Unity, you need:

- [The Meta Quest App](https://www.meta.com/quest/setup/)

## Getting the Code

First, ensure you have Git LFS installed by running:

```sh
git lfs install
```

Then, clone this repository using the "Code" button above or this command:

```sh
git clone https://github.com/oculus-samples/Unity-Discover.git
```

## Documentation

More information is available in the [Documentation](./Documentation) section of this project.

- [Configuration](./Documentation/Configuration.md)
- [Discover Overview](./Documentation/DiscoverOverview.md)
- [Project Structure](./Documentation/ProjectStructure.md)

Custom Packages:

- [Meta Utilities](./Packages/com.meta.utilities/README.md)
- [Meta Input Utilities](./Packages/com.meta.utilities.input/README.md)
- [Meta Avatars Utilities](./Packages/com.meta.utilities.avatars/README.md)

## License

Most of Discover is licensed under the [MIT LICENSE](./LICENSE). However, files from [Text Mesh Pro](https://unity.com/legal/licenses/unity-companion-license) and [Photon SDK](./Assets/Photon/LICENSE) are licensed under their respective terms.

## Contribution

See the [CONTRIBUTING](./CONTRIBUTING.md) file for information on how to contribute.
