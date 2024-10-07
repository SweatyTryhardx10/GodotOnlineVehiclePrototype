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

		// ==============================================================================
		// TODO: Velocity is post contact. Make the code below get the previous velocity.
		// NOTE: Use a state history (similar to link below).
		// https://forum.godotengine.org/t/determining-the-exact-global-position-of-a-collision-with-rigidbody2d-body-shape-entered/77007/2?u=sweatix
		// ==============================================================================

		if (isRigidbody)
		{
			RigidBody3D rb = (RigidBody3D)co;
			PhysicsDirectBodyState3D rbState = PhysicsServer3D.BodyGetDirectState(rb.GetRid());
			int contactIdx = rbState.GetContactIndexFromNode(n.GetInstanceId());
			Vector3 contactPosition = contactIdx != -1 ? rbState.GetContactColliderPosition(contactIdx) : Vector3.Zero;
			Vector3 contactNormal = contactIdx != -1 ? rbState.GetContactLocalNormal(contactIdx) : Vector3.Zero;

			if (linearVelocityInfluence > 0f)
			{
				// Modulate the damage based on this source's motion direction relative to the direction towards the impact point, and the speed of the damage receiver.
				// TODO: Reformulate.
				Vector3 toContactPoint = contactPosition - rbState.Transform.Origin;
				Vector3 linVelocityDifference = rbState.LinearVelocity - DetermineVelocity(n).Project(rbState.LinearVelocity);

				float toPointDotVelocity = toContactPoint.Normalized().Dot(linVelocityDifference); // NOTE: Is intentionally not normalized.
				toPointDotVelocity = Mathf.Max(0f, toPointDotVelocity);     // Clamp off negative values (no negative damage allowed!)

				linVelMod = (toPointDotVelocity - linearVelocityRange[0]) / (linearVelocityRange[1] - linearVelocityRange[0]);
				linVelMod = Mathf.Clamp(linVelMod, 0f, 1f);

				// DEBUGGING
				if (linVelocityDifference.LengthSquared() > 0.01f)
					this.ShowLine(3f, contactPosition, contactPosition + linVelocityDifference * (toPointDotVelocity / linVelocityDifference.Length()), 0.05f, Colors.OrangeRed);
			}
			if (angularVelocityInfluence > 0f)
			{
				// TODO: Implement angular velocity influence.
				//   ...You may need to base it on tangent-velocity.
				Vector3 contactVelocity = rbState.GetContactLocalVelocityAtPosition(contactIdx);
				Vector3 contactVelocityDiff = contactVelocity - DetermineVelocity(n).Project(contactVelocity);

				angVelMod = (contactVelocityDiff.Length() - angularVelocityRange[0]) / (angularVelocityRange[1] - angularVelocityRange[0]);
				angVelMod = Mathf.Clamp(angVelMod, 0f, 1f);

				// DEBUGGING
				if (contactVelocityDiff.LengthSquared() > 0.01f)
					this.ShowLine(3f, contactPosition, contactPosition + contactVelocityDiff, 0.05f, Colors.GreenYellow);
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
					contactPosition,
					contactNormal
				));

				// Add shake to the scene
				ShakeObject.Create(this, 0.8f, 12f, 0.05f * maxMod, contactPosition, 10f);
			}
		}
	}

	private Vector3 DetermineVelocity(Node n)
	{
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
