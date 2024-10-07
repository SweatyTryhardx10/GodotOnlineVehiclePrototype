using Godot;
using System;

public sealed partial class PropellerJoint : MotorJoint
{
	[Export] private CollisionShape3D fastSpeedShape;
	[Export] private float fastSpeedThreshold = Mathf.Tau * 2f;
	[Export] private float speedToThrustRatio = 1f;
	/// <summary>The amount of angular velocity that should be negated from the owner of this object's angular velocity.</summary>
	[Export] private float gyroscopicFactor = 1f;

	[ExportGroup("CTRL settings")]
	[Export] private float maxRunningTime = 5f;
	[Export] private float timeToMaxSpeed = 1f;
	[Export] private float maxSpeed = Mathf.Tau * 3f;
	[Export] private float timeBetweenUses = 1f;    // (+ max running time!)
	private ulong lastUseTime;
	private float TimeSinceUse => (Time.GetTicksMsec() - lastUseTime) / 1000f;
	private float TimeBeforeCanUse => ((timeBetweenUses + maxRunningTime) - TimeSinceUse) / 1000f;
	private bool turnedOn;

	public override void _Ready()
	{
		// MotorJoint base
		base._Ready();

		AssessCollisionSettings();
	}

	public override void _PhysicsProcess(double delta)
	{
		// MotorJoint base
		base._PhysicsProcess(delta);

		if (TimeSinceUse > maxRunningTime)
			turnedOn = false;

		if (turnedOn)
		{
			// Accelerate to target speed
			if (AngularSpeed != maxSpeed)
			{
				float dt = Mathf.Abs(maxSpeed) * (float)delta / timeToMaxSpeed;
				AngularSpeed = Mathf.MoveToward(AngularSpeed, maxSpeed, dt);
			}
		}
		else
		{
			// Decelerate to standstill
			if (AngularSpeed != 0f)
			{
				float dt = Mathf.Abs(maxSpeed) * (float)delta / timeToMaxSpeed;
				AngularSpeed = Mathf.MoveToward(AngularSpeed, 0f, dt);
			}
		}

		// Thrust effect (proportional to the angular speed of the selected axis)
		if (speedToThrustRatio > 0f)
		{
			float realAngularSpeed = (GlobalBasis.Inverse() * AngularVelocity)[(int)rotationAxis];
			parent.ApplyForce(GlobalBasis.Y * realAngularSpeed * speedToThrustRatio, this.GlobalPosition - parent.GlobalPosition);
		}

		// Gyroscopic-like effect (mitigates angular velocity on the owner)
		if (gyroscopicFactor > 0f)
		{
			if (!float.IsNaN(AngularVelocity.Length()) && AngularVelocity.Length() > 1f)
			{
				float realAngularSpeed = (GlobalBasis.Inverse() * AngularVelocity)[(int)rotationAxis];
				float ownerToPropellerRatio = parent.AngularVelocity.Length() / realAngularSpeed;
				Vector3 torque = -parent.AngularVelocity * (1f - ownerToPropellerRatio) * parent.Inertia;
				parent.ApplyTorque(torque);
			}
		}

		AssessCollisionSettings();
	}

	private void AssessCollisionSettings()
	{
		if (fastSpeedShape.IsValid())
		{
			if (AngularSpeed >= fastSpeedThreshold)
			{
				fastSpeedShape.Disabled = false;
			}
			else
			{
				fastSpeedShape.Disabled = true;
			}
		}
	}
	
	// Called locally
	public void UseA()
	{
		if (!turnedOn)
		{
			if (TimeBeforeCanUse <= 0f)
			{
				// Set state for this object (for all peers on the network)
				Rpc(MethodName.SetA, !turnedOn);
			}
		}
		else
		{
			Rpc(MethodName.SetA, !turnedOn);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetA(bool state)
	{
		turnedOn = state;

		if (turnedOn)
			lastUseTime = Time.GetTicksMsec();
	}

	public void Reset()
	{
		throw new NotImplementedException();
	}
}
