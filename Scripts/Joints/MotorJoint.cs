using Godot;
using System;

public partial class MotorJoint : RigidBody3D
{
	protected enum JointAxis
	{
		AxisX = 0,
		AxisY = 1,
		AxisZ = 2
	}

	[Export] protected bool active = true;
	[ExportGroup("Configuration")]
	[Export] protected JointAxis rotationAxis;
	[Export] public float AngularSpeed { get; set; } = 0f;
	[Export] public float AngularAcceleration { get; set; } = 0f;

	// protected float currentAngle;
	private Vector3 lastAngularVelocity;

	protected Transform3D localLockTransform;
	protected RigidBody3D parent;

	public override void _Ready()
	{
		localLockTransform = Transform;

		if (GetParent<Node3D>() is RigidBody3D pb)
		{
			parent = pb;
		}

		// // Configure a pin joint so the custom joint stays in place while affecting the parent.
		// PinJoint3D pj = new PinJoint3D(){ NodeA = parent.GetPath(), NodeB = this.GetPath() };
		// CallDeferred(MethodName.AddSibling, pj);

		Inertia = Vector3.One;
	}

	public override void _IntegrateForces(PhysicsDirectBodyState3D state)
	{
		#region Static non-physics method 
		// currentAngle += AngularSpeed * (float)GetPhysicsProcessDeltaTime();

		// Basis newBasis = parent.Basis * localLockTransform.Basis;

		// Vector3 axis;
		// switch (rotationAxis)
		// {
		// 	case JointAxis.AxisX:
		// 		axis = newBasis.X;
		// 		break;
		// 	case JointAxis.AxisY:
		// 		axis = newBasis.Y;
		// 		break;
		// 	case JointAxis.AxisZ:
		// 		axis = newBasis.Z;
		// 		break;
		// 	default:
		// 		axis = newBasis.X;
		// 		break;
		// }
		// axis = axis.Normalized();

		// // Position = localLockTransform.Origin
		// Basis newBasisRotated = newBasis.Rotated(axis, currentAngle);
		// state.Transform = new Transform3D(newBasisRotated, state.Transform.Origin);
		#endregion

		// Compute difference in rotation with one quaternion
		//  * Compute quaternion axis and rotation amount
		//  * Counter-rotate the body by applying torque
		//  * Rotate the body around the target axis

		Basis parentBasis = parent.GlobalBasis;
		Basis currBasis = state.Transform.Basis;
		Basis basisDelta = parentBasis * currBasis.Inverse();

		// Position & Rotation
		float currentAngle = (parentBasis.Inverse() * currBasis).GetEuler().Y;
		Basis targetBasis = parentBasis * new Basis(Vector3.Up, currentAngle);

		state.Transform = new Transform3D(targetBasis, parent.GlobalPosition + parent.GlobalBasis * localLockTransform.Origin);
		Vector3 angularAcceleration = (state.AngularVelocity - lastAngularVelocity) / state.Step;

		Vector3 angVel = Vector3.Up * AngularSpeed;
		Vector3 angVelDiff = angVel - state.AngularVelocity;
		Vector3 correctiveTorque = (targetBasis * currBasis.Inverse()).GetEuler() * 150f - angularAcceleration * 1f;
		state.ApplyTorque(angVelDiff);
		state.ApplyTorque(correctiveTorque.LimitLength(100f));
		
		// Clamp rotation
		if (basisDelta.GetEuler().Length() > Mathf.Pi)
		{
			state.Transform = new Transform3D(parent.GlobalBasis * localLockTransform.Basis, parent.GlobalPosition + parent.GlobalBasis * localLockTransform.Origin);
			state.AngularVelocity = state.AngularVelocity * Vector3.Up;
		}

		// int axis = (int)rotationAxis;
		// Vector3[] axes = { Vector3.Right, Vector3.Up, Vector3.Forward };

		// float threshold = Mathf.DegToRad(20f);
		// if (rotDelta.GetRotationQuaternion().GetAngle() > threshold)
		// {
		// 	float t = (rotDelta.GetRotationQuaternion().GetAngle() - threshold) / threshold;
		// 	Basis clampedBasis = currRot.Slerp(targetRot, t);
		// 	state.Transform = new Transform3D(clampedBasis, state.Transform.Origin);
		// }

		// float threshold = Mathf.DegToRad(5f);
		// Basis localBasis = parent.GlobalBasis.Inverse() * state.Transform.Basis;
		// float xClamp = Mathf.Clamp(localBasis.GetEuler().X, -threshold, threshold);
		// float y = localBasis.GetEuler().Y;
		// float zClamp = Mathf.Clamp(localBasis.GetEuler().Z, -threshold, threshold);
		// Basis localBasis_clamped = Basis.FromEuler(new Vector3(xClamp, y, zClamp)).Orthonormalized();

		// Basis basis_clamped = parent.GlobalBasis * localBasis;
		// state.Transform = new Transform3D(basis_clamped, state.Transform.Origin);

		// // Angular spring
		// Vector3 angularAcceleration = (state.AngularVelocity - lastAngularVelocity) / state.Step;
		// Vector3 torque = rotDelta.GetEuler() * 100f - angularAcceleration * 10f;

		// torque = torque.LimitLength(500f);

		// state.ApplyTorque(torque);

		lastAngularVelocity = state.AngularVelocity;
	}
}
