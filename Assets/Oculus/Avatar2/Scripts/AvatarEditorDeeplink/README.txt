The DLLs in this folder are only used for Avatar Editor Deeplinking. If you do not plan to open the avatar editor from your app, follow these steps to remove the dlls from your builds:
1) Add DISABLE_IPC_CONNECTOR to your Scripting Define Symbols
2) Remove all calls to AvatarEditorDeeplink.LaunchAvatarEditor()
3) Delete the AvatarEditorDeeplink folder and its contents