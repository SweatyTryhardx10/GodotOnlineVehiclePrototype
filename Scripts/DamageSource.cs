using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;

[GlobalClass]
public partial class DamageSource : Node
{
	public long PeerID { get; set; } = -1;

	[Export] private float baseDamage = 1f;
	[Export] private float variance = 0f;

	[ExportGroup("Physics Settings")]
	[Export(PropertyHint.Range, "0,1")] private float linearVelocityInfluence = 1f;
	[Export] private Vector2 linearVelocityRange = new Vector2(5f, 10f);
	[Export(PropertyHint.Range, "0,1")] private float angularVelocityInfluence = 0f;
	[Export] private Vector2 angularVelocityRange = Vector2.Down;

	[ExportGroup("Force Settings")]
	[Export(PropertyHint.Range, "0,3")] private float percentageAddedCollisionForce = 0f;
	/// <summary>Used exclusively for collision objects without mass (e.g. StaticBody3D, AnimatableBody3D).</summary>
	[Export(PropertyHint.Range, "0,10000,1")] private float nonRigidBodyCollisionMass = 100f;

	private CollisionObject3D co;

	private bool isArea;
	private bool isRigidbody;
	private bool isAnimatableBody;

	// State history (of physics object)
	// TODO: Find a way to isolate this physics-related code away from this class - probably just a new class that can be composed onto this script.
	private const int HISTORY_LENGTH = 3;
	private Transform3D[] transformHistory = new Transform3D[HISTORY_LENGTH];
	private Vector3[] velocityHistory = new Vector3[HISTORY_LENGTH];
	private Vector3[] angularVelocityHistory = new Vector3[HISTORY_LENGTH];
	private bool hasBeenUpdatedThisFrame = false;   // Used to prevent a double-update when updating the history in a collision event

	public override void _EnterTree()
	{
		co = GetParent<CollisionObject3D>();

		// Determine type of collision object
		if (co.IsValid())
		{
			if (co is Area3D)
			{
				isArea = true;
			}
			else if (co is RigidBody3D)
			{
				isRigidbody = true;
			}
			else if (co is AnimatableBody3D)
			{
				isAnimatableBody = true;
			}
			else
			{
				GD.PushWarning($"{co.Name} is not a valid collision object!");
			}
		}

		// Subscribe to relevant collision signals
		if (isArea)
		{
			Area3D a = (Area3D)co;
			a.BodyEntered += OnBodyEntered;
		}
		if (isRigidbody)
		{
			RigidBody3D rb = (RigidBody3D)co;
			rb.BodyEntered += OnBodyEntered;
		}
	}

	public override void _ExitTree()
	{
		// Unsubscribe to relevant collision signals
		if (isArea)
		{
			Area3D a = (Area3D)co;
			a.BodyEntered -= OnBodyEntered;
		}
		if (isRigidbody)
		{
			RigidBody3D rb = (RigidBody3D)co;
			rb.BodyEntered -= OnBodyEntered;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Maintain a state history if the collision object is a rigidbody
		if (isRigidbody && !((RigidBody3D)co).Sleeping)
		{
			// NOTE: The method call is deferred to ensure that the physics body has been updated.
			// TODO: Determine if a deferred call is actually necessary!
			CallDeferred(MethodName.UpdatePhysicsHistory);
		}
		if (isAnimatableBody)
		{
			UpdateAnimatedTransformHistory();
			AnimatableBodyCollisionRoutine(delta);
		}
	}

	private void UpdatePhysicsHistory()
	{
		// Prevent updating the history if it was already updated in this frame
		if (hasBeenUpdatedThisFrame)
		{
			// Reset control variable.
			hasBeenUpdatedThisFrame = false;
			return;
		}

		var rb = (RigidBody3D)co;

		for (int i = HISTORY_LENGTH - 1; i >= 0; i--)
		{
			if (i == 0)
			{
				// Add new entry to history
				transformHistory[i] = rb.GlobalTransform;
				velocityHistory[i] = rb.LinearVelocity;
				angularVelocityHistory[i] = rb.AngularVelocity;
			}
			else
			{
				// Offset old entries by one index thus deleting the oldest entry
				transformHistory[i] = transformHistory[i - 1];
				velocityHistory[i] = velocityHistory[i - 1];
				angularVelocityHistory[i] = angularVelocityHistory[i - 1];
			}
		}
	}

	/// <summary>
	/// Only used for the case where the source is an AnimatableBody3D.
	/// </summary>
	private void UpdateAnimatedTransformHistory()
	{
		var ab = (AnimatableBody3D)co;

		for (int i = HISTORY_LENGTH - 1; i >= 0; i--)
		{
			if (i == 0)
			{
				// Add new entry to history
				transformHistory[i] = ab.GlobalTransform;

				// Infer linear and angular velocity from the transform history
				Vector3 deltaPos = transformHistory[i].Origin - transformHistory[i + 1].Origin;
				velocityHistory[i] = deltaPos / (float)GetPhysicsProcessDeltaTime();

				Vector3 deltaRot;
				if (Mathf.IsZeroApprox(transformHistory[i].Basis.Determinant()) || Mathf.IsZeroApprox(transformHistory[i + 1].Basis.Determinant()))
					deltaRot = Vector3.Zero;    // Failsafe: Initial transform history contains non-invertable matrices. A delta rotation cannot be computed.
				else
					deltaRot = (transformHistory[i].Basis * transformHistory[i + 1].Basis.Inverse()).GetEuler();
				angularVelocityHistory[i] = deltaRot / (float)GetPhysicsProcessDeltaTime();
			}
			else
			{
				// Offset old entries by one index thus deleting the oldest entry
				transformHistory[i] = transformHistory[i - 1];
				velocityHistory[i] = velocityHistory[i - 1];
				angularVelocityHistory[i] = angularVelocityHistory[i - 1];
			}
		}
	}

	private void OnBodyEntered(Node n)
	{
		if (isRigidbody)
		{
			// Update state history to get it up-to-date (since history is updated at idle time by default).
			UpdatePhysicsHistory();
			hasBeenUpdatedThisFrame = true;

			RigidBody3D rb = (RigidBody3D)co;
			PhysicsDirectBodyState3D rbState = PhysicsServer3D.BodyGetDirectState(rb.GetRid());
			int contactIdx = rbState.GetContactIndexFromNode(n.GetInstanceId());

			Vector3 contactPoint = contactIdx != -1 ? rbState.GetContactColliderPosition(contactIdx) : Vector3.Zero;
			Vector3 contactNormal = contactIdx != -1 ? rbState.GetContactLocalNormal(contactIdx) : Vector3.Zero;
			Vector3 localContactPoint = transformHistory[0].Basis.Inverse() * (contactPoint - transformHistory[0].Origin);
			Vector3 contactVelocity = ComputeVelocityAtPoint(localContactPoint);
			Vector3 comToContactPoint = contactPoint - rb.ToGlobal(rb.CenterOfMass);

			ComputeAndApplyCollisionDamage(n, rb.Mass, comToContactPoint, contactPoint, contactVelocity, contactNormal);
		}
		if (isArea)
		{
			ComputeAndApplyCollisionDamage(n, nonRigidBodyCollisionMass);
		}
	}

	/// <summary>
	/// Checks for collision on the AnimatableBody3D via TestMove().
	/// </summary>
	private void AnimatableBodyCollisionRoutine(double delta)
	{	
		KinematicCollision3D coll = new();
		bool hasCollided = ((AnimatableBody3D)co).TestMove(transformHistory[0], velocityHistory[0] * (float)delta, coll, maxCollisions: 4);
		if (hasCollided)
		{	
			List<ulong> collisionHistory = new();
			
			int collisions = coll.GetCollisionCount();
			for (int i = 0; i < collisions; i++)
			{
				// Skip colliders who have already been damaged in this update.
				if (collisionHistory.Contains(coll.GetColliderId(i)))
					continue;
				
				// Collision information
				Node other = coll.GetCollider(i) as Node;
				Vector3 contactPoint = coll.GetPosition(i);
				Vector3 localContactPoint = transformHistory[0].Basis.Inverse() * contactPoint;
				Vector3 contactVelocity = ComputeVelocityAtPoint(localContactPoint);
				Vector3 contactNormal = coll.GetNormal(i);
				
				ComputeAndApplyCollisionDamage(other, nonRigidBodyCollisionMass, localContactPoint, contactPoint, contactVelocity, contactNormal);
				
				// Register the 'other' node so damage is not applied twice (in case of two contact points on the same body)
				collisionHistory.Add(coll.GetColliderId(i));
			}
		}
	}

	/// <summary>
	/// Computes damage based on collision data and applies it to a body, <b>n</b>.
	/// </summary>
	/// <param name="n">The node receiving the damage.</param>
	/// <param name="mass">The mass of this damage source.</param>
	/// <param name="comToContactPoint">The vector from the center of mass to the contact point between this and the other body, <b>n</b>.</param>
	/// <param name="contactPoint">The contact point in world space.</param>
	/// <param name="contactVelocity">The contact velocity in world space.</param>
	/// <param name="contactNormal">The contact normal in world space.</param>
	private void ComputeAndApplyCollisionDamage(Node n, float mass, Vector3 comToContactPoint, Vector3 contactPoint, Vector3 contactVelocity, Vector3 contactNormal)
	{
		float damage = baseDamage;

		// Variance
		float v = 0f;
		if (variance > 0f)
		{
			v = (float)GD.Randfn(0f, variance);
		}

		// Physics influence
		float linVelMod = 1f;
		float angVelMod = 0f;

		if (linearVelocityInfluence > 0f)
		{
			// Modulate the damage based on this source's motion direction relative to the direction towards the impact point, and the speed of the damage receiver.
			// TODO: Reformulate.
			Vector3 linVelocityDifference = velocityHistory[1] - DetermineVelocity(n).Project(velocityHistory[1]);

			float toPointDotVelocity = comToContactPoint.Normalized().Dot(linVelocityDifference); // NOTE: Delta velocity is intentionally not normalized.
			toPointDotVelocity = Mathf.Max(0f, toPointDotVelocity);     // Clamp off negative values (no negative damage allowed!)

			linVelMod = (toPointDotVelocity - linearVelocityRange[0]) / (linearVelocityRange[1] - linearVelocityRange[0]);
			linVelMod = Mathf.Clamp(linVelMod, 0f, 1f);

			// DEBUGGING
			if (linVelocityDifference.LengthSquared() > 0.01f)
				this.ShowLine(3f, contactPoint, contactPoint + linVelocityDifference * (toPointDotVelocity / linVelocityDifference.Length()), 0.05f, Colors.OrangeRed);
		}
		if (angularVelocityInfluence > 0f)
		{
			// TODO: Implement angular velocity influence.
			//   ...You may need to base it on tangent-velocity.
			Vector3 contactVelocityDiff = contactVelocity - DetermineVelocity(n).Project(contactVelocity);

			angVelMod = (contactVelocityDiff.Length() - angularVelocityRange[0]) / (angularVelocityRange[1] - angularVelocityRange[0]);
			angVelMod = Mathf.Clamp(angVelMod, 0f, 1f);

			// DEBUGGING
			if (contactVelocityDiff.LengthSquared() > 0.01f)
				this.ShowLine(3f, contactPoint, contactPoint + contactVelocityDiff, 0.05f, Colors.GreenYellow);
		}

		// Apply physics modifier to damage amount
		float maxMod = Mathf.Clamp(linVelMod + angVelMod, 0f, 1f);
		damage *= maxMod;

		// Apply damage to node (if this is the local client OR is a non-player object on the server)
		// NOTE: Replication is handled by the affected health node(s)
		bool isLocal = this.TreeMP().GetUniqueId() == PeerID;
		bool isServerSideNonPlayer = this.TreeMP().GetUniqueId() == 1 && PeerID == -1;
		if (isLocal || isServerSideNonPlayer)
		{
			n.Dmg(new Health.DamageData(
				PeerID,
				damage,
				contactPoint,
				contactNormal,
				new Health.KnockbackModifier(contactVelocity * mass * percentageAddedCollisionForce, contactPoint)
			));

			// Add shake to the scene
			// TODO: Move this to the health node.
			ShakeObject.Create(this, 0.8f, 12f, 0.05f * maxMod, contactPoint, 10f);
		}
	}

	/// <summary>
	/// Computes damage based on a limited set of data and applies it to a body, <b>n</b>.<br />
	/// Use in conjunction with Area3D or other non-collision physics objects.
	/// </summary>
	/// <param name="n">The node receiving the damage.</param>
	/// <param name="mass">The mass of this damage source.</param>
	private void ComputeAndApplyCollisionDamage(Node n, float mass)
	{
		Vector3 dummyContactPoint = (n as Node3D).GlobalPosition;
		ComputeAndApplyCollisionDamage(n, mass, Vector3.Zero, dummyContactPoint, Vector3.Zero, Vector3.Up);
	}

	// Adapted from my post on the Godot Forum:
	// https://forum.godotengine.org/t/determining-the-exact-global-position-of-a-collision-with-rigidbody2d-body-shape-entered/77007/2?u=sweatix
	/// <summary>
	/// Extrapolates the state (using the state history) from the previous frame to the current frame to produce a usable collision velocity.
	/// </summary>
	/// <param name="localPoint">A point in the local space of the physics body.</param>
	/// <returns>A velocity vector in global space.</returns>
	private Vector3 ComputeVelocityAtPoint(Vector3 localPoint)
	{
		//====================================================================================
		// Compute point velocity based on the previous state (velocity, and angular velocity)
		// NOTE: The immediate state of the object is not useful for this use-case; the collision
		//		forces have already been applied to the rigidbody.
		//====================================================================================

		// Edge-case: Non-invertible matrices in the transformation history.
		if (Mathf.IsZeroApprox(transformHistory[0].Basis.Determinant()) || Mathf.IsZeroApprox(transformHistory[1].Basis.Determinant()))
			return velocityHistory[1];
		// ----------------------------------------------------------------

		// Compute the velocity resulting purely from the angular velocity
		// .. The difference in rotation between the two states (current and old state)
		Basis basisDiff = transformHistory[0].Basis * transformHistory[1].Basis.Inverse();
		// .. The vector that is "tangent" to the position (from the local point provided).
		var tangent = (transformHistory[1].Basis.Rotated(transformHistory[1].Basis.GetRotationQuaternion().GetAxis(), Mathf.Pi / 2.0f) * localPoint).Normalized();
		// .. The velocity at the point modulated by the angular_velocity and distance from the center.
		Vector3 velocityRot = basisDiff * tangent * angularVelocityHistory[1].Length() * localPoint.Length();

		return velocityHistory[1] + velocityRot;
	}

	private Vector3 DetermineVelocity(Node n)
	{
		// TODO: Check if the type-casting works in the if-block(s) below.
		
		// TODO: Implement globally accessible state history to allow lookup of another object's previous state(s).

		if (n is RigidBody3D rb)
		{
			return rb.LinearVelocity;
		}
		else
		{
			return Vector3.Zero;
		}
	}
}
