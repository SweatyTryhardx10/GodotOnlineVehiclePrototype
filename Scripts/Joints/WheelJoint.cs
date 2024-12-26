using Godot;
using System;

public partial class WheelJoint : Node3D
{
	[Export] public float Inertia { get; private set; } = 0.2f;

	[ExportGroup("Spring Settings")] // Properties
	[Export] private float length { get => springLength; set => springLength = value; }
	[Export] private float stiffness { get => springStiffness; set => springStiffness = value; }
	[Export] private float damping { get => springDamping; set => springDamping = value; }

	private float springLength = 0.5f;
	private float springStiffness = 100f;
	private float springDamping = 20f;

	[ExportGroup("Tire Settings")] // Properties
	[Export] private float radius { get => wheelRadius; set => wheelRadius = value; }
	// [Export] private Curve lateralSlipCurve { get; set; }
	// [Export] private Curve longitudinalSlipCurve { get; set; }
	[Export] private float lateralSlipScaling = 1f;
	[Export] private float longitudinalSlipScaling = 1f;
	[Export] private float longitudinalInfluenceOnLateral = 1f;
	[Export] private float camberInfluence = 1f;
	
	[ExportSubgroup("Friction")]
	[Export] private float normalizedFrictionMaxLat = 10f;	// The maximum multiple of the wheel load exerted as a friction force.
	[Export] private float normalizedFrictionMaxLong = 10f;	// The maximum multiple of the wheel load exerted as a friction force.
	
	/// <summary>The signed slip ratio (lateral).</summary>
	public float LatSlipRatio { get; private set; }
	/// <summary>The signed slip ratio (longitudinal).</summary>
	public float LongSlipRatio { get; private set; }
	public float GetEvaluatedSlipLong() => CurrentTerrainProfile.longitudinalSlipCurve.Sample(LongSlipRatio);

	public float wheelRadius = 0.5f;
	private float wheelWidth = 0.2f;

	private TerrainProfile[] terrainProfiles = new TerrainProfile[0];
	private int latestTerrainProfileIdx;
	public TerrainProfile CurrentTerrainProfile => terrainProfiles.Length > 0 ? terrainProfiles[latestTerrainProfileIdx] : null;

	[ExportGroup("Axle Settings")]
	[Export] public Vector2 SteeringInterval { get; private set; } = new Vector2(-30f, 30f);
	[Export] private float steeringTime = 1f;
	[Export] public bool PoweredByEngine { get; private set; } = true;
	[Export] public bool HasBrakes { get; private set; } = true;

	/// <summary>Dictates the target steering state of the joint (range: [-1, 1]).</summary>
	private float steeringDirection;

	[ExportGroup("Other")]
	[Export(PropertyHint.Layers3DPhysics)] private uint collisionMask = 1;
	[Export] private bool interpolateSpringState = true;
	[Export] private bool showDebug = false;

	[ExportCategory("References")]
	[Export] private Node3D wheelMesh;

	private RigidBody3D rb;

	public Vector3 ContactPoint { get => springState.EndPoint; }
	public Vector3 ContactVelocity { get => (springState.EndPoint - oldSpringState.EndPoint) / (float)GetPhysicsProcessDeltaTime(); }

	private SpringState springState;
	private SpringState oldSpringState;

	public SpringState SpringInfo => springState;
	public Vector3 LastAppliedSpringForce { get; private set; }
	public Vector3 LastAppliedCollisionForce { get; private set; }

	// INTERPOLATION (only used for non-gameplay parts e.g. visuals and audio)
	private double interpolationTimer;
	private SpringState betweenState;

	/// <summary>The angular velocity of this joint's axle (in rad/s)<br />Note: Negative is forward!</summary>
	private float angularVelocity;
	/// <summary>The angular velocity of this joint's axle (in rad/s)<br />Note: Negative is forward!</summary>
	public float AngularVelocity
	{
		get => angularVelocity;
		set
		{
			angularVelocity = value;
			lastAngularVelocityManipulationTime = Time.GetTicksMsec();
		}
	}
	private ulong lastAngularVelocityManipulationTime;
	private float TimeSinceAngularVelocitySet => (Time.GetTicksMsec() - lastAngularVelocityManipulationTime) / 1000f;

	public bool OnFloor { get; private set; }
	public bool WasOnFloor { get; private set; }
	public bool JustOnFloor => OnFloor && !WasOnFloor;

	private Godot.Collections.Dictionary recentRayResults;

	private bool queueReset;

	public struct SpringState
	{
		public Vector3 origin;
		public Vector3 vector;
		public float velocity;

		public float Compression(float maxLength) { return 1f - (vector.Length() / maxLength); }

		/// <summary>The end of the spring in global coordinates.</summary>
		public Vector3 EndPoint { get => origin + vector; }

		public SpringState(Vector3 globalPosition, Vector3 globalVector, float globalVelocity)
		{
			origin = globalPosition;
			vector = globalVector;
			velocity = globalVelocity;
		}
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		rb = GetParent<RigidBody3D>();

		// // Initialize slip curves (if not set)
		// if (!lateralSlipCurve.IsValid())
		// {
		// 	lateralSlipCurve = new Curve();
		// }
		// if (!longitudinalSlipCurve.IsValid())
		// {
		// 	longitudinalSlipCurve = new Curve();
		// }

		// Get references to any terrain profiles present under this node
		terrainProfiles = this.GetAllNodes<TerrainProfile>();
	}

	public override void _Process(double delta)
	{
		// State interpolation for smooth visuals
		if (interpolateSpringState)
		{
			float t = (float)(interpolationTimer / GetPhysicsProcessDeltaTime());
			t = Mathf.Clamp(t, 0f, 1f);

			betweenState = new SpringState(
				oldSpringState.origin.Lerp(springState.origin, t),
				oldSpringState.vector.Lerp(springState.vector, t),
				Mathf.Lerp(oldSpringState.velocity, springState.velocity, t)
			);

			UpdateMesh();

			interpolationTimer += delta;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		oldSpringState = springState;

		if (queueReset)
		{
			queueReset = false;
			return;
		}

		PerformSteering(delta);

		SpringDynamics(delta);

		TireDynamics(delta);

		UpdateMesh();

		// DEBUG
		if (showDebug)
		{
			if (OnFloor)
			{
				this.ShowLine((float)delta, springState.origin, springState.EndPoint, 0.01f, Colors.Red);
				Vector3 cylStart = springState.EndPoint - (Vector3)recentRayResults["normal"] * 0.05f;
				Vector3 cylEnd = springState.EndPoint + (Vector3)recentRayResults["normal"] * 0.05f;
				this.ShowCylinder((float)delta, cylStart, cylEnd, 0.5f, 8, true, Colors.Red, false);
			}
			else
			{
				this.ShowLine((float)delta, springState.origin, springState.EndPoint, 0.01f, Colors.Green);
			}
		}

		if (interpolateSpringState)
		{
			// Reset the "cycle" of the timer (to preserve the "leftovers" from the previous timer interval)
			// e.g. 0.33 % 0.2 == 0.13..
			interpolationTimer = interpolationTimer % GetPhysicsProcessDeltaTime();

			// Failsafe in case the interpolationTimer gets out of phase with the physics process
			if (interpolationTimer > GetPhysicsProcessDeltaTime() / 2f)
				interpolationTimer = 0d;
		}
	}

	private void SpringDynamics(double delta)
	{
		// TODO: The front and back rays are not working as intended. Please fix.

		// Raycast queue
		Rid[] excludes = new Rid[] { rb.GetRid() };
		Vector3 centerRayStart = GlobalPosition;
		Vector3 centerRayEnd = centerRayStart - GlobalBasis.Y * springLength;

		float longRayOffset = wheelRadius / 2f;
		Vector3 frontRayOffset = GlobalBasis.Z * longRayOffset + GlobalBasis.Y * Mathf.Sin(Mathf.Pi / 2f * (longRayOffset / wheelRadius)) * wheelRadius;
		Vector3 backRayOffset = GlobalBasis.Z * -longRayOffset + GlobalBasis.Y * Mathf.Sin(Mathf.Pi / 2f * (longRayOffset / wheelRadius)) * wheelRadius;
		Util.RaycastOperation[] rays = new Util.RaycastOperation[] {
			new Util.RaycastOperation(centerRayStart, centerRayEnd, excludes, collisionMask),
			new Util.RaycastOperation(centerRayStart + wheelWidth / 2f * GlobalBasis.X, centerRayEnd + wheelWidth / 2f * GlobalBasis.X, excludes, collisionMask),
			new Util.RaycastOperation(centerRayStart - wheelWidth / 2f * GlobalBasis.X, centerRayEnd - wheelWidth / 2f * GlobalBasis.X, excludes, collisionMask),
			new Util.RaycastOperation(centerRayStart + frontRayOffset, centerRayEnd + frontRayOffset, excludes, collisionMask),
			new Util.RaycastOperation(centerRayStart + backRayOffset, centerRayEnd + backRayOffset, excludes, collisionMask),
		};

		// Spring raycast
		Godot.Collections.Dictionary[] results = new Godot.Collections.Dictionary[rays.Length];
		bool anyRayHit = false;
		for (int i = 0; i < rays.Length; i++)
		{
			// Perform the raycast and get results
			results[i] = rays[i].PerformOperation(this);

			// DEBUGGING
			if (showDebug)
				this.ShowLine((float)delta, rays[i].start, results[i].Count > 0 ? (Vector3)results[i]["position"] : rays[i].end, 0.01f, results[i].Count > 0 ? Colors.Orange : Colors.Yellow);

			if (results[i].Count > 0) anyRayHit = true;
		}
		// Update grounded state
		WasOnFloor = OnFloor;
		OnFloor = anyRayHit;

		// Process raycast results to produce the approximated spring state
		springState = ProcessSpringRayResults(rays, results, delta);

		if (OnFloor)
		{
			Vector3 surfaceNormal = recentRayResults["normal"].As<Vector3>();
			float springToSurfaceDot = Mathf.Clamp(surfaceNormal.Dot(-springState.vector.Normalized()), 0f, 1f);
			PhysicsDirectBodyState3D rbState = PhysicsServer3D.BodyGetDirectState(rb.GetRid());
			Vector3 contactPointLocalToRigidbody = springState.EndPoint - rb.GlobalPosition;

			// Spring force
			float springStrength = springState.Compression(springLength) * springStiffness - springState.velocity * springDamping;  // Hooke's law
			springStrength *= springToSurfaceDot;
			// Modulates spring strength based on its angle relative to the ground normal. This is done to emulate the interaction between a real wheel entity and a rigid spring joint.

			Vector3 springForce = GlobalBasis.Y * springStrength;

			// Collision force
			Vector3 collisionForce = -rbState.GetVelocityAtLocalPosition(contactPointLocalToRigidbody).Project(surfaceNormal) * rb.Mass * Mathf.Abs(rb.GetGravity().Y);
			collisionForce *= 1f - springToSurfaceDot;

			// Apply forces
			rb.ApplyForce(springForce, rb.GlobalBasis * Position);  // You have to use the global delta position...
			rb.ApplyForce(collisionForce, contactPointLocalToRigidbody);

			// STORE APPLIED FORCE FOR THE PUBLIC INTERFACE
			LastAppliedSpringForce = springForce;
			LastAppliedCollisionForce = collisionForce;

			// Impulse response for suspension bottoming out
			// TODO: This does not feel correct. Find out if it IS correct!
			if (springState.vector.Length() <= wheelRadius)
			{
				Vector3 localSpringPoint = rb.GlobalBasis * Position;
				Vector3 springImpulse = GlobalBasis.Y * -oldSpringState.velocity * rb.Mass * (float)delta;
				// NOTE: Old spring state is used because the current state's velocity is computed at the limit of the spring's travel range; its velocity may be inaccurate.
				rb.ApplyImpulse(springImpulse, localSpringPoint);
			}

			// Terrain evaluation
			var terrain = recentRayResults["collider"].As<Node3D>();
			if (terrain is GeometryInstance3D geom && terrain.TryGetNode(out TerrainData tData))
			{
				int terrainIdx = -1;
				if (tData.useTexture)	// Get the terrain profile from the associated terrain texture
				{
					Vector2? uv = geom.UVCoordsFromPoint(springState.EndPoint);

					// if (Engine.GetPhysicsFrames() % 60 == 0)
					// 	GD.Print($"({Name}) UV: {uv}");

					Color terrainPixel;
					if (uv.HasValue && tData.IdImage.IsValid())
					{
						var idWidth = tData.IdImage.GetWidth();
						var idHeight = tData.IdImage.GetHeight();

						Vector2I pixelCoord = new Vector2I(
							Mathf.Clamp(Mathf.FloorToInt(uv.Value.X * idWidth), 0, idWidth - 1),
							Mathf.Clamp(Mathf.FloorToInt(uv.Value.Y * idHeight), 0, idHeight - 1)
						);
						terrainPixel = tData.IdImage.GetPixelv(pixelCoord);

						// if (Engine.GetPhysicsFrames() % 10 == 0)
						// 	GD.Print($"Terrain ID Color: {terrainPixel}");
					}
					else
					{
						GD.PushWarning("No valid UV was computed, or no terrain texture is set.");
						terrainPixel = Colors.Black;
					}

					// Use terrain data to select a terrain profile
					// TODO: Blend terrain profiles in case of partial color channel saturation.
					int highestProfileIdx = 0;
					float highestProfileValue = 0.5f;
					for (int i = 0; i < Mathf.Min(terrainProfiles.Length - 1, 4); i++)
					{
						// If the color channel (associated with the profile) is higher than the previous, select this profile.
						if (terrainPixel[i] >= highestProfileValue)
						{
							highestProfileValue = terrainPixel[i];
							highestProfileIdx = i + 1;
						}
					}
					
					terrainIdx = highestProfileIdx;
				}
				else 	// Get the terrain profile that is directly configured on the terrain data node.
				{
					terrainIdx = (int)tData.terrainType;
				}

				latestTerrainProfileIdx = terrainIdx;
			}
			else
			{
				// No terrain data is available. Use the first (default) terrain profile.
				// GD.PushWarning("No terrain data is available!");
				latestTerrainProfileIdx = 0;
			}
		}
	}

	/// <summary>
	/// Processes the set of rays used to approximate the spring (and tire's) state as a result of terrain collision.
	/// </summary>
	/// <returns>The approximated spring state.</returns>
	private SpringState ProcessSpringRayResults(Util.RaycastOperation[] rays, Godot.Collections.Dictionary[] results, double delta)
	{
		// Determine the shortest ray from the raycasts performed
		float shortestRayDistance = float.MaxValue;
		int shortestRayHitIdx = -1;
		for (int i = 0; i < results.Length; i++)
		{
			// Determine the shortest ray (from start to hit point)
			if (results[i].Count > 0)
			{
				var distFromOrigin = ((Vector3)results[i]["position"] - rays[i].start).Length();
				if (distFromOrigin < shortestRayDistance)
				{
					shortestRayDistance = distFromOrigin;
					shortestRayHitIdx = i;
				}
			}
		}

		if (shortestRayHitIdx == -1)
		{
			// "Extend" spring to equilibrium (because no rays hit anything)
			return new SpringState(GlobalPosition, GlobalBasis.Y * -springLength, 0f);
		}
		else
		{
			// Compute the spring state based on the raycast results (with the shortest ray as the primary contributor)
			Vector3 springOrigin = GlobalPosition;  // ("Ray origin" - "Delta from center ray origin") i.e. node position
			Vector3 springVector = (Vector3)results[shortestRayHitIdx]["position"] - rays[shortestRayHitIdx].start;
			float springVelocity = (springVector.Length() - oldSpringState.vector.Length()) / (float)delta;

			recentRayResults = results[shortestRayHitIdx];  // DEBUGGING (Maybe)

			return new SpringState(springOrigin, springVector, springVelocity);
		}
	}

	private void TireDynamics(double delta)
	{
		if (!OnFloor)
		{
			return;
		}

		// Tire characteristics
		Vector3 velocity = ContactVelocity;
		Vector3 localVelocity = GlobalBasis.Inverse() * velocity;
		float slipRatio = localVelocity.X / (Mathf.Abs(localVelocity.X) + Mathf.Abs(localVelocity.Z));
		float tireLoad = (springState.Compression(springLength) * springStiffness - springState.velocity * springDamping);
		// TODO: \/
		// NOTE: With tire deformation aside, tire load (and thus the tire forces) is directly proportianal to the force of the spring.
		//     - However, to emulate the deformation of the tire (which can be simplified to another spring), the suspension spring's...
		//		...velocity can be used as an indicator for the state of the tire spring. Obviously a tire is very stiff meaning the...
		//		...tire's spring would be too. Therefore, this pseudo tire spring would have low influence on the estimated tire load.

		// Edge case: numerical instability at very low velocities resulting in a near-zero division
		if (float.IsNaN(slipRatio) || float.IsInfinity(slipRatio))
		{
			slipRatio = 0f;
		}

		// Edge case: Quantized slip ratio as a result of numerical instability at low velocities.
		float thresh = 1f;
		if (velocity.Length() < thresh)
			slipRatio = Mathf.Lerp(1f, slipRatio, velocity.Length() / thresh);

		LatSlipRatio = slipRatio;  // TODO: Merge these variables

		// ==== Compute and apply tire forces ====

		// == Camber ==
		Vector3 localNormal = GlobalBasis.Inverse() * recentRayResults["normal"].As<Vector3>();
		float camberAngle = -Vector3.Up.SignedAngleTo(localNormal, Vector3.Forward);    // Sign is reversed such that: positive camber => more "grip" (grip is a loose term)
		camberAngle /= Mathf.Pi / 2f;   // Done to achieve a normalized angle value in [-1, 1] instead of [-HalfPi, HalfPi]
										// NOTE: Variable naming, and its modified value range, may sow confusion if used elsewhere.

		// == Slip ==
		float latSlip = CurrentTerrainProfile.lateralSlipCurve.Sample(Mathf.Abs(slipRatio)) * lateralSlipScaling;

		float speedFromAngVel = -AngularVelocity * wheelRadius; // Positive is forward
		float speed = -localVelocity.Z;                         // Positive is forward

		float longSlipRatio = (speed - speedFromAngVel) / Mathf.Abs(speed);
		if (float.IsNaN(longSlipRatio))
			longSlipRatio = 1f;
		float longSlip = CurrentTerrainProfile.longitudinalSlipCurve.Sample(Mathf.Abs(longSlipRatio)) * longitudinalSlipScaling;

		LongSlipRatio = longSlipRatio;

		// == Longitudinal ==
		Vector3 groundBasisZ = GlobalBasis.Z.Slide(recentRayResults["normal"].As<Vector3>().Normalized()).Normalized();
		float longForce = (speedFromAngVel - speed) * longSlip * tireLoad;
		rb.ApplyForce(-groundBasisZ * longForce, springState.EndPoint - rb.GlobalPosition);

		// == Lateral ==
		// NOTE: The lateral force computed here is for steady-state, static friction.
		float lateralMultiplier = Mathf.Clamp(-localVelocity.X, -normalizedFrictionMaxLat, normalizedFrictionMaxLat);	// Scales the lateral force based on local velocity. Clamped to emulate friction force limits.
		float lateralForce = lateralMultiplier * latSlip * tireLoad;
		lateralForce = lateralForce / (1f + Mathf.Abs(longSlipRatio) * longitudinalInfluenceOnLateral); // Enforce dropping lateral force as a result of longitudinal slip
		lateralForce += camberAngle * camberInfluence * tireLoad;   // Camber influence
		rb.ApplyForce(GlobalBasis.X * lateralForce, springState.EndPoint - rb.GlobalPosition);

		// Affect the wheel's angular velocity on the basis of the longitudinal slip ratio
		AngularVelocity = -Mathf.Lerp(speedFromAngVel, speed, longSlip) / wheelRadius;

		// DEBUGGING!
		DebugOverlay.ShowTextAtNode(
			$"{Name}\n" + $"Long: {longSlip:0.00}\n" + $"Lat: {latSlip:0.00}",
			wheelMesh,
			Color.FromHsv(120f / 360f, 1f, 0.5f, 1f)
		);
	}

	private void UpdateMesh()
	{
		Vector3 localMeshPos;
		Vector3 localMeshRotation = wheelMesh.Rotation;
		if (interpolateSpringState)
		{
			localMeshPos = ToLocal(betweenState.EndPoint) + Vector3.Up * wheelRadius;

			localMeshRotation += Vector3.Right * AngularVelocity * (float)GetProcessDeltaTime();    // This part is/should be called in _Process()
			localMeshRotation.X = localMeshRotation.X % Mathf.Tau;
		}
		else
		{
			localMeshPos = ToLocal(springState.EndPoint) + Vector3.Up * wheelRadius;

			localMeshRotation += Vector3.Right * AngularVelocity * (float)GetPhysicsProcessDeltaTime(); // This part is/should be called in _PhysicsProcess();
			localMeshRotation.X = localMeshRotation.X % Mathf.Tau;

			// === FOR ROTATING MESH BASED ON DISTANCE TRAVELLED ===
			// float distanceCovered = (ToLocal(springState.EndPoint) - ToLocal(oldSpringState.EndPoint)).Z;
			// localMeshRotation += Vector3.Right * distanceCovered / wheelRadius;
			// localMeshRotation.X = localMeshRotation.X % Mathf.Tau;
		}

		wheelMesh.Position = localMeshPos;
		wheelMesh.Rotation = localMeshRotation;
	}

	public void ApplyTorque(float amount)
	{
		AngularVelocity += amount / Inertia;    // Divide by inertia to get the correct amount
	}

	private void PerformSteering(double delta)
	{
		float targetSteeringAngle;
		float t = Mathf.Abs(steeringDirection);
		if (steeringDirection > 0f)
		{
			targetSteeringAngle = Mathf.Lerp(0f, SteeringInterval.X, t);
		}
		else
		{
			targetSteeringAngle = Mathf.Lerp(0f, SteeringInterval.Y, t);
		}

		Vector3 targetRotation = Vector3.Up * Mathf.DegToRad(targetSteeringAngle);
		Rotation = Util.ExpDecay(Rotation, targetRotation, 10f / steeringTime, (float)delta);
	}

	public void SetSteeringDirection(float direction)
	{
		steeringDirection = direction;
	}

	/// <summary>
	/// Resets all state variables to their default value.
	/// Avoids unwanted behaviour when significantly altering the physical state of the vehicle.
	/// </summary>
	public void ResetState()
	{
		springState = new SpringState(GlobalPosition, GlobalBasis.Y * -springLength, 0f);
		oldSpringState = springState;

		queueReset = true;
	}
}