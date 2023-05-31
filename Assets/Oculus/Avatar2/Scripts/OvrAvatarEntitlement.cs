using System;

namespace Oculus.Avatar2
{
    public static class OvrAvatarEntitlement
    {
        private static string logScope = "entitlement";
        private static string _accessToken = "";

        public static bool AccessTokenIsValid() => !String.IsNullOrEmpty(_accessToken);

        public static void SetAccessToken(string token)
        {
            var result = CAPI.ovrAvatar2_UpdateAccessToken(token);
            if (result.IsSuccess())
            {
                _accessToken = token;
            }
            else
            {
                OvrAvatarLog.LogError($"UpdateAccessToken Failed: {result}", logScope);
            }
        }

        public static void ResendAccessToken()
        {
            if (AccessTokenIsValid())
            {
                SetAccessToken(_accessToken);
            }
            else
            {
                OvrAvatarLog.LogError("Cannot resend access token when there is no valid token.", logScope);
            }
        }
    }
}
