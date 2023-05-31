using System;

namespace Oculus.Avatar2
{
    public partial class OvrAvatarEntity
    {
        // TODO: Should probably be private/internal?
        public bool GetAvailableManifestationFlags(out UInt32 manifestationFlags)
        {
            return CAPI.ovrAvatar2Entity_GetAvailableManifestationFlags(entityId, out manifestationFlags)
                .EnsureSuccess("ovrAvatar2Entity_GetAvailableManifestationFlags", logScope, this);
        }

        // TODO: Should probably be private/internal?
        public bool GetManifestationFlags(out CAPI.ovrAvatar2EntityManifestationFlags manifestationFlags)
        {
            return CAPI.ovrAvatar2Entity_GetManifestationFlags(entityId, out manifestationFlags)
                .EnsureSuccess("ovrAvatar2Entity_GetAvailableManifestationFlags", logScope, this);
        }

        // TODO: Should probably be private/internal?
        public bool SetManifestationFlags(CAPI.ovrAvatar2EntityManifestationFlags manifestation)
        {
            return CAPI.ovrAvatar2Entity_SetManifestationFlags(entityId, manifestation)
                .EnsureSuccess("ovrAvatar2Entity_SetManifestationFlags", logScope, this);
        }
    }
}
