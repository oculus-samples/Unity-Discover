using System.Collections.Generic;

namespace Oculus.Skinning.GpuSkinning
{
    internal enum SkinningOutputFrame { FrameZero, FrameOne, FrameTwo }

    internal interface IOvrGpuSkinnerDrawCall
    {
        bool EnableBlock(OvrSkinningTypes.Handle handle, SkinningOutputFrame writeDest);
        void RemoveBlock(OvrSkinningTypes.Handle handle);

        bool NeedsDraw(SkinningOutputFrame writeDest);
        void Draw(SkinningOutputFrame writeDest);

        void Destroy();
    }
}
