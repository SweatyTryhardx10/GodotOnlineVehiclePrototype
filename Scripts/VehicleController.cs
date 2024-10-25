using Godot;
using System;
using System.Linq;

public partial class VehicleController : RigidBody3D
{
	[Export] private VehicleSystem.Engine engine;

	private WheelJoint[] allJoints;

	private bool queueReset;

	private bool queuePhysicsState;
	private Transform3D queuedTransform;
	private Vector3 queuedVelocity;
	private Vector3 queuedAngularVelocity;

	// INPUT
	/// <summary>A value in the range of [-1, 1] where a negative value is reversing/braking, and a positive is accelerating.</summary>
	public float throttle;
	/// <summary>A value in the range of [-1, 1] dictating which way the vehicle should be turning.</summary>
	public float steering;
	private float actualSteeringAngle;

	public bool OnFloor
	{
		get
		{
			int groundedJoints = 0;
			foreach (Node3D n in allJoints)
			{
				if (n is WheelJoint s)
					if (s.OnFloor)
						groundedJoints++;
			}

			return groundedJoints >= allJoints.Length / 2;
		}
	}

	private Transform3D initialTransform;

	public override void _Ready()
	{
		initialTransform = GlobalTransform;

		allJoints = this.GetAllNodes<WheelJoint>();

		// Bind the engine to relevant objects
		engine.BindBody(this);
		engine.BindJoints(allJoints);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{	
		// ACCELERATION (Engine)
		if (engine.IsValid())
		{
			float speed = -(GlobalBasis.Inverse() * LinearVelocity).Z;
			engine.Throttle = throttle;
			
			DebugOverlay.ShowTextOnScreen($"Speed: {speed:0.00}", new Vector2(0.7f, 0.8f), "Speed-o-meter");
		}

		// STEERING
		foreach (var j in allJoints)
		{
			if (j.IsValid())
				j.SetSteeringDirection(steering);
		}
	}

	public override void _IntegrateForces(PhysicsDirectBodyState3D state)
	{
		if (queueReset)
		{
			Transform3D tx = initialTransform;
			state.Transform = tx;

			state.LinearVelocity = Vector3.Zero;
			state.AngularVelocity = Vector3.Zero;

			// Reset all spring/wheel
			foreach (Node3D n in allJoints)
			{
				if (n is WheelJoint s)
					s.ResetState();
			}

			queueReset = false;
		}

		if (queuePhysicsState)
		{
			state.Transform = queuedTransform;
			state.LinearVelocity = queuedVelocity;
			state.AngularVelocity = queuedAngularVelocity;

			queuePhysicsState = false;
		}
	}

	/// <summary>Resets the physics configuration and positions the vehicle near the origin.</summary>
	public void QueueReset()
	{
		this.Sleeping = false;	// If body is sleeping, _IntegrateForces() is not executed.
		
		queueReset = true;

		engine.Reset();

		foreach (var n in allJoints)
		{
			if (n.IsValid() && n is WheelJoint j)
				j.AngularVelocity = 0f;
		}
	}

	public void QueuePhysicsState(Transform3D tx, Vector3 linearVelocity, Vector3 angularVelocity)
	{
		this.Sleeping = false;	// If body is sleeping, _IntegrateForces() is not executed.
		
		queuePhysicsState = true;
		
		queuedTransform = tx;
		queuedVelocity = linearVelocity;
		queuedAngularVelocity = angularVelocity;
	}
}
