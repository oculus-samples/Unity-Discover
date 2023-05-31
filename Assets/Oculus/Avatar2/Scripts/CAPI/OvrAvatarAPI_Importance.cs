using System;
using System.Runtime.InteropServices;

namespace Oculus.Avatar2
{
    public partial class CAPI
    {
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Importance_SetBudget(UInt32 maxActiveEntities, UInt32 budget);
        
        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern CAPI.ovrAvatar2Result ovrAvatar2Importance_SetImportanceAndCost(ovrAvatar2EntityId entityId, float importance, UInt32 cost);
    }
}
