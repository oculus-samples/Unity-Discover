// Copyright (c) Meta Platforms, Inc. and affiliates.

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Discover.UI.Taskbar
{
    public class ProfilePicAssigner : MonoBehaviour
    {
        [SerializeField] private Image m_profilePicImage;

        private void Awake()
        {
            Initialize();
        }

        private async void Initialize()
        {
            // Could be provided instead of using the OculusPlatformUtils
            var user = await OculusPlatformUtils.GetLoggedInUser();
            _ = FetchProfilePicAsync(user.ImageURL).ContinueWith(AssignProfilePic);
        }

        private void AssignProfilePic(Texture2D profilePicTexture)
        {
            if (m_profilePicImage)
            {
                m_profilePicImage.sprite = Texture2DToSprite(profilePicTexture);
            }
            else
            {
                Debug.LogError("Profile Image reference not assigned.");
            }
        }

        private static Sprite Texture2DToSprite(Texture2D t)
        {
            return t ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f)) : null;
        }

        private static async UniTask<Texture2D> FetchProfilePicAsync(string url)
        {
            url = url.Trim();

            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            using var www = UnityWebRequestTexture.GetTexture(url);
            _ = await www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(www.error);
                return null;
            }
            else
            {
                return ((DownloadHandlerTexture)www.downloadHandler).texture;
            }
        }
    }
}
