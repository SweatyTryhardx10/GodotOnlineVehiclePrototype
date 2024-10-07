using Godot;
using System;

[GlobalClass]
public partial class SkeletonPhysics : SkeletonModifier3D
{
	private Skeleton3D skeleton;
	private PhysicalBone3D[] bones;

	private const float SPRINT_JOINT_STRENGTH = 10f;
	private const float SPRING_JOINT_DAMPING = 0.5f;

    public override void _Ready()
    {
        skeleton = GetSkeleton();
		
		bones = this.GetAllNodes<PhysicalBone3D>();
    }

    public override void _ProcessModification()
    {
		for (int i = 0; i < skeleton.GetBoneCount(); i++)
		{
			Quaternion boneRot = skeleton.GetBonePoseRotation(i);
			Quaternion restRot = skeleton.GetBoneRest(i).Basis.GetRotationQuaternion();
			
			Quaternion rotDiff = restRot * boneRot.Inverse();
		}
    }

}
