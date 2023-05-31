# Discover Overview

This document will explain this project's functionality at a high level. We will present the flow of the applications and what technical components are in play.

## 1. Launching

When launching the application, the splash screen is shown and loads the Discover scene. This is where the app setup happens. We initialize the platform, check for entitlement, and get the logged in user information that will be shared with other players (username, profile picture) and the data use for the system (userId for Avatars and shared spatial anchors). This scene also sets up the player rig for passthrough rendering.

## 2. New User Experience (Networking)

First-time users will be presented with the NUX panels explaining how to connect. Once read through, it can be reset through the settings to view it again.

## 3. Networking Choice

This is the initial popup window that displays after the NUX. In this window you have an input field to insert the session name, 3 buttons to select how to connect (Host, Join, Join Remote) and the settings button.

### 3a. Host

When clicking Host, you will be hosting a session. To do so, you first need to set up your room, if it wasn't already done. Once the room is setup it will load the layout through the Scene API. Next, we will connect to the photon session. If no session name was provided, we generate a random one.

Once connected as a host we create the ColocationDriver, a network component that handles the colocation using the colocation package. This will create a shared spatial anchor that other players will be able to get in order to colocate. Once the anchor is created, we realign the player to it.

The host will also spawn the scene network objects (walls, ceiling, floor, desks, etc.) in relation to the room layout.

We then load the 3D icons in the space if any were saved on disk.

### 3b. Join

If the player is in the same room as the host, they can join as colocated. If the system detects that they are not in the same space, the player will join as a remote player.

First the player will need to input the session name in the input field and then click Join. This will join the Photon session. Once in the photon session, the ColocationDriver is spawned (since it's already on the network) and the system will try to colocate. We send a request to the host to share the anchor, and once the host has shared the anchor, it notifies the player that they can access it. The player then fetches the shared anchors and loads it. Once loaded, the player realigns to the anchor so that they are in the same frame of reference as the host.

All the scene objects are loaded once the player has joined the photon session.

### 3c. Join Remote

This is for players that are not in the same space. They will join as an Avatar in the colocated session. They will see other players as Avatars and others will see them as an Avatar. They will also be able to locomote using their controllers or hand gestures as the space might not fit their current room.

The ColocationDriver skips the colocation setup when joining remotely.

All the scene objects are loaded once the player has joined the photon session.

### 3d. Settings

This is where you can reset the NUX to get the information about the network popup.

You can also choose a networking region if you connect remotely to a host in a different region. By default, this is set to Best region, which chooses the region with the least amount of latency.

## 4. Application NUX

Once you select the method to join a session, first time users will see a panel that explains how the application works before they connect to the session. This can also be reset in the settings.

## 5. Place/Move Icons

Once connected we can now place and move icons. The host and anyone colocated can place icons in their space. First, you'll need to open the Menu (using your left-hand menu button) if an application is not already placed, you can click on that icon, and it will start the placement flow.

You will notice a puck with the icon hovering, pointing at different surface in the room will show if the placement is allowed or not. If allowed, you can then click with your index (pinch if hand tracking) to place it. You can also rotate the icon using the joystick or moving your hand while pinching to place.

To move the icons, you will need to point at a 3D icon and long press. While pressing a move icon will show up, once released you will enter the placement flow.

Once an icon is placed, we save the placement on disk so that when you host again, the icons will be in the same locations. Each icon uses a spatial anchor so that they are anchored in the scene. We save that anchor with some data (app name) so that we can reload them in the same exact anchored spot.

## 6. Launch Applications

Once an icon is in place, anyone can launch the application. Either click on the icon in the menu or on the 3D icon in the space.

Applications are networked containers that contain the application logic. The host will spawn the application, and everyone will then have the application spawned. Anyone can then close the application from the menu.

Each application has its own logic and setup from there.

## 7. Menu

The menu contains multiple panels.

* Apps: This is where you can launch or place an application.

* Settings: Reset NUX, clear the saved 3D Icons position, leave the session.

* Info: Information about the Discover application and networking

* Scene: Toggle scene highlight

On the menu bottom bar, you will find the room session name, time, and battery level information.
