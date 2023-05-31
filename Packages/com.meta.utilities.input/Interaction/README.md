# Interaction Utilities

This directory contains general utilities that assist in developing the input (controllers, hands, etc.) components of a project.

|Utility|Description|
|-|-|
|[FromXRHandDataSource](./FromXRHandDataSource.cs)|Class encapsulating useful hand data, including bone transforms, poses, and skeletons.|
|[FromXRHmdDataSource](./FromXRHmdDataSource.cs)|Class encapsulating useful HMD data, including bone transforms, poses, and configurations.|
|[HandednessFilter](./HandednessFilter.cs)|An interactor Filter that restricts the Interactable to only register interactions from a specific hand (left or right).|
|[HandRefHelper](./HandRefHelper.cs)|Singleton that provides easy public references to the player's [Hands](../../com.oculus.integration.interaction/Runtime/Scripts/Input/Hands/Hand.cs) and [HandRefs](../../com.oculus.integration.interaction/Runtime/Scripts/Input/Hands/HandRef.cs).|
|[InteractableFilterActiveState](./InteractableFilterActiveState.cs)|Simple script for determining the state (active or inactive) of an [IInteractorView](../../com.oculus.integration.interaction/Runtime/Scripts/Interaction/Core/IInteractor.cs)|
|[XRHandRefChooser](./XRHandRefChooser.cs)|Class that determines what form of input the user is currently using and swaps between them dynamically.|