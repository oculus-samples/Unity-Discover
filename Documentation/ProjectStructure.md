# Project Structure

Here you will be able to get an high overview of the project structure and the key elements.

## Coding Standards

In order to keep the project easy to read and easy to understand we setup a coding standard that applies to all the code of our project.
We use the `dotnet format Unity-Discover.sln` function to ensure and apply the appropriate formatting while developing. The specifics of the rules used are setup in the [.editorconfig](../.editorconfig) as well as [Unity-Discover.sln.DotSettings](../Unity-Discover.sln.DotSettings) and [Assembly-CSharp.csproj.DotSettings](../Assembly-CSharp.csproj.DotSettings) file.

## Discover Content

We separated the project per type of assets, keeping it simple to navigate. As an exception to this, we separated the internal applications (DroneRage and MRBike) into their own structure, which we address later on. The directories are self explanatory but we will go over some of them that might be of interest to you.

### Config

In this directory we find the configurations related to Discover. Here you will find scriptable objects related to the App Manifests as well as the App List.

The App List is simply a list of the manifests we want to use in the project, this list is shared and linked to different scripts on prefabs, this gives us the freedom to easily modify it without code modification as well as simplify the logic to access that list.

The App Manifests, contain the information required to setup the applications in the project. It contains data related to icons, 3d icons, placement information, name and the prefab instantiated to launch the application.

### Scenes

The main scene is [Discover.unity](../Assets/Discover/Scenes/Discover.unity), this is the entry point to the project and the only scene used to run the Discover Application.

In the [Examples](../Assets/Discover/Scenes/Examples) folder we can find scenes that demonstrates certain features presented in the application. They are simplified scenes to more easily understand certain concepts.

* [Colocation](../Assets/Discover/Scenes/Examples/Colocation.unity): Demonstrate a simple scene that setup colocation between players in the same room, where they will see scene elements in the same location.
* [RoomMapping](../Assets/Discover/Scenes/Examples/RoomMapping.unity): Demonstrates how the Scene API loads the rooms and generate all elements related to the room once the room is mapped by the user in headset.
* [SimpleMRScene](../Assets/Discover/Scenes/Examples/SimpleMRScene.unity): As the name depict, it's a scene that is showing the setup required to use passthrough and start an MR application.
* [StartupExample](../Assets/Discover/Scenes/Examples/StartupExample.unity): Simple launcher for the application that handles entitlement checks and logged in user data.

## Discover Scripts

Here we will give some more information on some of the sub groups of scripts included in [Assets/Discover/Scripts](../Assets/Discover/Scripts).

## Colocation

[Colocation](../Assets/Discover/Scripts/Colocation) is where we find the implementation of the interfaces required for the [colocation package](../Packages/com.meta.xr.sdk.colocation) as well as the impementation on using that package to colocate users.

[ColocationDriverNetObj](../Assets/Discover/Scripts/Colocation/ColocationDriverNetObj.cs) is the key component that sets up the application to use colocation. It is a network behaviour that when spawned starts the colocation logic.

In the Test folder we find the [ColocationTestBootStrapper](../Assets/Discover/Scripts/Colocation/Test/ColocationTestBootStrapper.cs) which is used in the Colocation example scene to test out the colocation flow.

## Networking

[Networking](../Assets/Discover/Scripts/Networking) is where we have specific functionalities related to Photon Fusion Networking.

## NUX

[NUX](../Assets/Discover/Scripts/NUX) implements the management for new user experiences views that shows multiple pages. The NUXManager handles the different NUXControllers to launch, stop and reset them.

## SpatialAnchors

[SpatialAnchors](../Assets/Discover/Scripts/SpatialAnchors) implements functionalities to save, load and erase local anchors to file.

This is a very reusable system to enable simple save to file or any desired location (like a custom cloud solution) for the users saved anchors including some meta information to be used on load.

## Fake Room

When using the editor it can be faster to load directly in editor without using Link and using the XRSimulator. To enable this feature we created a FakeRoom system that will load the equivalent of the Scene API to enable testing in editor.

[FakeRoom](../Assets/Discover/Scripts/FakeRoom) directory contains the custom implementation for classifications since we needed to create our own equivalence from the OVRSemanticClassification.

[MRSceneLoader](../Assets/Discover/Scripts/MRSceneLoader.cs) the room loader handles loading the right scene. If no headsets are connected in editor, it will load the [FakeRoom](../Assets/Discover/Prefabs/FakeRoom/FakeRoom.prefab) prefab.

## Applications

### DroneRage

Located at [Assets/Discover/DroneRage](../Assets/Discover/DroneRage), this is where all the content of the application is contained. The structure of the content is setup relative to the different element of the game rather than separated by type of assets. All the scripts are located in the [Scripts](../Assets/Discover/DroneRage/Scripts) folder.

### MRBike

Located at [Assets/MRBike](../Assets/MRBike), this is where all the content of the application is contained.

This is a standalone application that we integrated in Discover. The root of the application is the [BikeInteraction prefab](../Assets/MRBike/Prefabs/BikeInteraction.prefab). This prefab can be instantiated by the photon runner and this will launch the bike application.

The BikeVisibleObjects are networked to keep their state as we place or move parts around.
