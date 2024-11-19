using Godot;
using System;

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

	private CollisionObject3D co;

	private bool isArea;
	private bool isRigidbody;

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
			CallDeferred(MethodName.UpdatePhysicsHistory);
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

	private void OnBodyEntered(Node n)
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

		if (isRigidbody)
		{
			// Update state history to get it up-to-date (history is updated at idle time).
			UpdatePhysicsHistory();
			hasBeenUpdatedThisFrame = true;

			RigidBody3D rb = (RigidBody3D)co;
			PhysicsDirectBodyState3D rbState = PhysicsServer3D.BodyGetDirectState(rb.GetRid());
			int contactIdx = rbState.GetContactIndexFromNode(n.GetInstanceId());
			
			Vector3 contactPoint = contactIdx != -1 ? rbState.GetContactColliderPosition(contactIdx) : Vector3.Zero;
			Vector3 contactNormal = contactIdx != -1 ? rbState.GetContactLocalNormal(contactIdx) : Vector3.Zero;
			Vector3 localContactPoint = transformHistory[0].Basis.Inverse() * (contactPoint - transformHistory[0].Origin);
			Vector3 contactVelocity = ComputeVelocityAtPoint(localContactPoint);
			Vector3 ComToContactPoint = contactPoint - rb.ToGlobal(rb.CenterOfMass);

			if (linearVelocityInfluence > 0f)
			{
				// Modulate the damage based on this source's motion direction relative to the direction towards the impact point, and the speed of the damage receiver.
				// TODO: Reformulate.
				Vector3 linVelocityDifference = velocityHistory[1] - DetermineVelocity(n).Project(velocityHistory[1]);

				float toPointDotVelocity = ComToContactPoint.Normalized().Dot(linVelocityDifference); // NOTE: Is intentionally not normalized.
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
					contactNormal
				));
				
				// Add shake to the scene
				// TODO: Move this to the health node.
				ShakeObject.Create(this, 0.8f, 12f, 0.05f * maxMod, contactPoint, 10f);
			}
		}
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
