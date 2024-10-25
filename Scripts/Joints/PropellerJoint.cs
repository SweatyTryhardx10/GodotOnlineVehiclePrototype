using Godot;
using System;

public sealed partial class PropellerJoint : HingeJoint3D
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
	private bool TurnedOn
	{
		get => turnedOn;
		set
		{
			turnedOn = value;
			this.Set("motor/enable", value);
		}
	}

	private float AngularSpeed
	{
		get => GetParam(Param.MotorTargetVelocity);
		set => SetParam(Param.MotorTargetVelocity, value);
	}
	/// <summary>The current angular speed on the joint axis.<br />NOTE: The joint axis is currently not configurable (Z-axis only).</summary>
	private float RealAngularSpeed => (GlobalBasis.Inverse() * bodyB.AngularVelocity)[2];

	private RigidBody3D bodyA;
	private RigidBody3D bodyB;

	public override void _Ready()
	{
		AssessCollisionSettings();

		// Store references to connected physics bodies (to avoid constant type-casting)
		bodyA = GetNode<RigidBody3D>(NodeA);
		bodyB = GetNode<RigidBody3D>(NodeB);
	}

	public override void _PhysicsProcess(double delta)
	{
		// MotorJoint base
		base._PhysicsProcess(delta);

		if (TimeSinceUse > maxRunningTime)
		{
			TurnedOn = false;
		}

		if (TurnedOn)
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

		// Eliminate the torque acting on body A by counteracting it.
		// Vector3 inertiaA = PhysicsServer3D.BodyGetParam(bodyA.GetRid(), PhysicsServer3D.BodyParameter.Inertia).As<Vector3>();
		// Vector3 inertiaB = PhysicsServer3D.BodyGetParam(bodyB.GetRid(), PhysicsServer3D.BodyParameter.Inertia).As<Vector3>();
		// float bodyInertiaRatioZ = (inertiaB / inertiaA)[2] / 2f;
		// float angVelDiffZ = (GlobalBasis.Inverse() * bodyB.AngularVelocity)[2] - (GlobalBasis.Inverse() * bodyA.AngularVelocity)[2];
		// bodyA.ApplyTorque(GlobalBasis.Z * -angVelDiffZ / (float)delta);

		// if (Engine.GetPhysicsFrames() % 30 == 0)
		// 	GD.Print($"Inertia A: {inertiaA} | Inertia B: {inertiaB} | Ratio: {bodyInertiaRatioZ}");

		GD.Print($"Angular velocity param: {AngularSpeed}");

		// Thrust effect (proportional to the angular speed of the selected axis)
		if (speedToThrustRatio > 0f)
		{
			bodyA.ApplyForce(GlobalBasis.Z * RealAngularSpeed * speedToThrustRatio, this.GlobalPosition - bodyA.GlobalPosition);
		}

		// Gyroscopic-like effect (mitigates angular velocity on the owner)
		if (gyroscopicFactor > 0f)
		{
			if (!float.IsNaN(bodyB.AngularVelocity.Length()) && bodyB.AngularVelocity.Length() > 1f)
			{
				float ownerToPropellerRatio = bodyA.AngularVelocity.Length() / RealAngularSpeed;
				Vector3 torque = -bodyA.AngularVelocity * (1f - ownerToPropellerRatio) * bodyA.Inertia;
				bodyA.ApplyTorque(torque);
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
		if (!TurnedOn)
		{
			if (TimeBeforeCanUse <= 0f)
			{
				// Set state for this object (for all peers on the network)
				Rpc(MethodName.SetA, !TurnedOn);
			}
		}
		else
		{
			Rpc(MethodName.SetA, !TurnedOn);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetA(bool state)
	{
		TurnedOn = state;

		if (TurnedOn)
			lastUseTime = Time.GetTicksMsec();
	}

	public void Reset()
	{
		throw new NotImplementedException();
	}
}
