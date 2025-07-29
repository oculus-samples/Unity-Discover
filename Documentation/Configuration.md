# Project Configuration
In order for this project to be functional in editor and on device there is some initial setup that needs to be done.

## Application Configuration
To run the project and use the platform services we need to create an application on the [Meta Quest Developer Center](https://developers.meta.com/horizon/).

To run on device you will need a Quest application, and to run in editor you will need a Rift application. The following sections will describe the configuration required for the application to run.

### Data Use Checkup
To use the features from the Platform we need to request which kind of data is required for the application. This can be found in the _Data Use Checkup_ section of the application.

![data use checkup](./Media/dashboard/datausecheckup.png "Data use Checkup")

And configure the required Data Usage:

![data use checkup options](./Media/dashboard/datausecheckup_options.png "Data use Checkup options")

- **User Id**: Avatars, Oculus Username
- **User Profile**: Avatars, Oculus Username
- **Avatars**: Avatars

Once completed you will need to submit the request, click the submit button at the bottom.

![data use checkup submit](./Media/dashboard/datausecheckup_submit.png "Data use Checkup submit")

To allow sharing of Spatial Anchors the Platform Service Cloud Storage needs to be enabled as well. To enable this go to All Platform Services and then click Add Service Under Cloud Storage

![cloud storage all platform](./Media/dashboard/cloudstorage_allplatform.png "Cloud Storage all platform")

Then Enable Automatic Cloud Backup and press submit

![cloud storage enable](./Media/dashboard/cloudstorage_enable.png "Cloud Storage enable")


### Set the Application ID
We then need to the set the application ID in our project in Unity.

The identifier (__App ID__) can be found in the _API_ section.

![Application API](./Media/dashboard/dashboard_api.png "Application API")

Then it needs to be placed in the [Assets/Resources/OculusPlatformSettings.asset](../Assets/Resources/OculusPlatformSettings.asset), which can be accessed in the menubar via _Oculus > Platform > Edit Settings_.

![Oculus Platform Settings Menu](./Media/editor/oculusplatformsettings_menu.png "Oculus Platform Settings Menu")

![Oculus Platform Settings](./Media/editor/oculusplatformsettings.png "Oculus Platform Settings")

## Photon Configuration

To get the sample working, you will need to configure Photon with your own account and applications. The Photon base plan is free.
- Visit [photonengine.com](https://www.photonengine.com) and [create an account](https://doc.photonengine.com/en-us/realtime/current/getting-started/obtain-your-app-id)
- From your Photon dashboard, click "Create A New App"
    - We will create 2 apps, "Fusion" and "Voice"
- First fill out the form making sure to set type to "Photon Fusion". Then click Create.
    - Select "Fusion 1" in the Select SDK Version item.
- Second fill out the form making sure to set type to "Photon Voice". Then click Create.

Your new app will now show on your Photon dashboard. Click the App ID to reveal the full string and copy the value for each app.

Open your unity project and paste your Fusion App ID and Voice App ID in [Assets/Photon/Fusion/Resources/PhotonAppSettings](../Assets/Photon/Fusion/Resources/PhotonAppSettings.asset), which can be accessed in the menubar via _Fusion > RealtimeSettings_.

![Photon App Settings](./Media/editor/photonappsettings.png "Photon App Settings")


The Photon Realtime transport should now work. You can check the dashboard in your Photon account to verify there is network traffic.

## Headset permissions
When you first launch the application a permission popup will ask to share point cloud, you must say yes if you want to use colocation.
If you answered no, from this application or another application using shared point cloud data, you can go on device to
**Settings > Privacy > Device Permissions > Share Point Cloud Data**. It must be enabled.

The application should also have Spatial Data permission enabled, this can be verified on device here: **Settings -> Apps -> Permissions -> Spatial Data**

## Upload to release channel
In order to have colocation working using the shared spatial anchors, you will first need to upload an initial build to a release channel.
For instructions you can go to the [developer center](https://developers.meta.com/horizon/resources/publish-release-channels-upload/). Then to be able to test with other users you will need to add them to the channel, more information in the [Add Users to Release Channel](https://developers.meta.com/horizon/resources/publish-release-channels-add-users/) topic.

Once the initial build is uploaded you will be able to use any development build with the same application Id, no need to upload every build to test local changes.
