# Project Configuration

To make this project functional in the editor and on a device, some initial setup is necessary.

## Application Configuration

To run the project and use platform services, create an application on the [Meta Quest Developer Center](https://developers.meta.com/horizon/).

For device operation, you need a Quest application, and for editor operation, a PC VR application. The following sections describe the necessary configuration.

### Data Use Checkup

Specify the type of data required for the application in the _Data Use Checkup_ section.

![data use checkup](./Media/dashboard/datausecheckup.png "Data use Checkup")

Configure the required Data Usage:

![data use checkup options](./Media/dashboard/datausecheckup_options.png "Data use Checkup options")

- **User Id**: Avatars, Oculus Username
- **User Profile**: Avatars, Oculus Username
- **Avatars**: Avatars

After completing this, submit the request by clicking the submit button at the bottom.

![data use checkup submit](./Media/dashboard/datausecheckup_submit.png "Data use Checkup submit")

To enable sharing of Spatial Anchors, go to Cloud Storage under Development.

![cloud storage all platform](./Media/dashboard/cloudstorage_allplatform.png "Cloud Storage all platform")

Enable Automatic Cloud Backup and press submit.

![cloud storage enable](./Media/dashboard/cloudstorage_enable.png "Cloud Storage enable")

### Set the Application ID

Set the application ID in your Unity project.

Find the identifier (__App ID__) in the _API_ section.

![Application API](./Media/dashboard/dashboard_api.png "Application API")

Place it in [Assets/Resources/OculusPlatformSettings.asset](../Assets/Resources/OculusPlatformSettings.asset), accessible via _Meta_ > Platform > Edit Settings_ in the menubar.

![Oculus Platform Settings Menu](./Media/editor/oculusplatformsettings_menu.png "Oculus Platform Settings Menu")

![Oculus Platform Settings](./Media/editor/oculusplatformsettings.png "Oculus Platform Settings")

## Photon Configuration

To get the sample working, configure Photon with your account and applications. The Photon base plan is free.

- Visit [photonengine.com](https://www.photonengine.com) and [create an account](https://doc.photonengine.com/realtime/current/getting-started/obtain-your-app-id).
- From your Photon dashboard, click "Create A New App".
    - Create two apps: "Fusion" and "Voice".
- Fill out the form for "Photon Fusion" and click Create.
    - Select "Fusion 1" in the Select SDK Version item.
- Fill out the form for "Photon Voice" and click Create.

Your new app will appear on your Photon dashboard. Click the App ID to reveal and copy the full string for each app.

Open your Unity project and paste your Fusion App ID and Voice App ID in [Assets/Photon/Fusion/Resources/PhotonAppSettings](../Assets/Photon/Fusion/Resources/PhotonAppSettings.asset), accessible via _Fusion > RealtimeSettings_ in the menubar.

![Photon App Settings](./Media/editor/photonappsettings.png "Photon App Settings")

The Photon Realtime transport should now work. Verify network traffic on your Photon account dashboard.

## Headset Permissions

When you first launch the application, a permission popup will ask to share the point cloud; select yes to use colocation. If you select no, go to device settings: **Settings > Privacy > Device Permissions > Share Point Cloud Data**. Enable it.

Ensure the application has Spatial Data permission enabled; verify on the device: **Settings -> Apps -> Permissions -> Spatial Data**.

## Upload to Release Channel

To enable colocation using shared spatial anchors, upload an initial build to a release channel. Refer to the [developer center](https://developers.meta.com/horizon/resources/publish-release-channels-upload/) for instructions. To test with other users, add them to the channel; more information is available in the [Add Users to Release Channel](https://developers.meta.com/horizon/resources/publish-release-channels-add-users/) topic.

Once the initial build is uploaded, you can use any development build with the same application ID, eliminating the need to upload every build to test local changes.
