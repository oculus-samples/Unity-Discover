using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

public class HandWithOffset : Hand
{
    [SerializeField]
    private Pose _offset;

    protected override void Apply(HandDataAsset data)
    {
        data.Root = PoseUtils.Multiply(data.Root, _offset);
    }
}
