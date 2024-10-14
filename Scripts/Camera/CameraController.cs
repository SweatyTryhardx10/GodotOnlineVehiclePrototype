using Godot;
using System;

public partial class CameraController : Camera3D
{
	public static CameraController Instance { get; private set; }

	[Export] public Node3D target;
	private Transform3D[] targetTransformHistory = new Transform3D[3];
	[Export] private float stiffness = 20f;
	[Export] private Vector3 offset = new Vector3(0f, 1f, 3f);
	[Export] private Vector3 rotOffset = new Vector3(20f, 0f, 0f);

	[Export] private float inputDecay = 10;
	private Vector2 inputLook;
	private Vector2 inputLookSmoothed;
	private Vector2 inputOffset;   // Used to steer camera

	[Export(PropertyHint.Range, "0,1")] private float tAccelPosFactor = 0.1f;
	[Export(PropertyHint.Range, "0,0.1")] private float tAccelRotFactor = 0.001f;
	[Export(PropertyHint.Range, "0,1")] private float tAccelFovFactor = 1f;
	private float defaultFov;
	private Vector3 TargetVelocity => (targetTransformHistory[0].Origin - targetTransformHistory[1].Origin) / (float)GetPhysicsProcessDeltaTime();
	private Vector3 smoothedLocalTargetAcceleration;

	private ulong lastLookInputTime;
	private float TimeSinceLookInput => (Time.GetTicksMsec() - lastLookInputTime) / 1000f;

	[ExportGroup("Collision Detection")]
	[Export] private bool checkForCollisions = true;
	[Export] private float collisionMargin = 0.05f;
	[Export(PropertyHint.Layers3DPhysics)] private uint collisionMask = 1;

	[ExportGroup("Free Cam Settings")]
	[Export] private float freeCamSpeed = 10f;

	public static bool FreeCamEnabled {get; private set; } = false;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;

		Instance = this;

		defaultFov = Fov;
	}

	public override void _Process(double delta)
	{
		if (!target.IsValid())
			return;

		// Revert to default rotation after X seconds of no look input
		if (TimeSinceLookInput > 5f)
		{
			float t = (float)delta * stiffness;
			float x = Mathf.LerpAngle(inputOffset.X, 0f, t);
			float y = Mathf.LerpAngle(inputOffset.Y, Vector3.Forward.SignedAngleTo(TargetVelocity.XZ(), Vector3.Up), t);
			y = TargetVelocity.XZ().Length() > 1f ? y : inputOffset.Y;	// Do not utilize the target's velocity if it is too low.
				
			inputOffset = new Vector2(x, y);
		}
		else
		{
			// inputLookSmoothed = inputLookSmoothed.Lerp(inputLook, 1f - 0.99f * INPUTSMOOTHING);
			inputLookSmoothed = Util.ExpDecay(inputLookSmoothed, inputLook, inputDecay, (float)delta);

			// Apply look input
			inputOffset -= new Vector2(inputLookSmoothed.Y, inputLookSmoothed.X) * Mathf.DegToRad(1f);
		}
		float xClamped = Mathf.Clamp(inputOffset.X, -Mathf.Pi / 4f, Mathf.Pi / 2f);
		inputOffset.X = xClamped;

		// Update transform history for the target
		for (int i = targetTransformHistory.Length - 1; i >= 0; i--)
		{
			if (i == 0)
				targetTransformHistory[i] = target.GlobalTransform;
			else
				targetTransformHistory[i] = targetTransformHistory[i - 1];
		}

		Vector3 targetVelocity0 = (targetTransformHistory[0].Origin - targetTransformHistory[1].Origin) / (float)delta;
		Vector3 targetVelocity1 = (targetTransformHistory[1].Origin - targetTransformHistory[2].Origin) / (float)delta;
		Vector3 targetAcceleration = target.GlobalBasis.Inverse() * (targetVelocity0 - targetVelocity1) / (float)delta;
		smoothedLocalTargetAcceleration = smoothedLocalTargetAcceleration.Lerp(targetAcceleration, (float)delta);
		smoothedLocalTargetAcceleration = smoothedLocalTargetAcceleration.LimitLength(50f);

		// Compute the camera transform "stack"
		Vector3 newPosition = Vector3.Zero;
		Vector3 newRotation = Vector3.Zero;
		if (FreeCamEnabled)
			ProcessFreeCam(delta, ref newPosition, ref newRotation);
		else
			ProcessCamera(delta, ref newPosition, ref newRotation);	// #1
		ProcessShakeObjects(ref newPosition, ref newRotation);	// #2

		// Set position and rotation of camera
		GlobalPosition = newPosition;
		GlobalRotation = newRotation;

		// TODO: FOV (Disabled until a proper interpolation solution is created.)
		// Fov = defaultFov - smoothedLocalTargetAcceleration.Z * tAccelFovFactor;

		// Reset look
		inputLook = Vector2.Zero;
	}

	private void ProcessCamera(double delta, ref Vector3 position, ref Vector3 rotation)
	{
		if (!target.IsValid())
			return;

		// Compute the target transform
		Vector3 targetPosition = target.GlobalPosition + Basis.FromEuler(new Vector3(inputOffset.X, inputOffset.Y, 0f)) * offset;
		Vector3 targetRotation = (rotOffset * Mathf.DegToRad(1f)) + new Vector3(inputOffset.X, inputOffset.Y, 0f);

		// Target acceleration influence
		// targetPosition += smoothedLocalTargetAcceleration * tAccelPosFactor;
		// targetRotation -= new Vector3(smoothedLocalTargetAcceleration.Z, 0f, -smoothedLocalTargetAcceleration.X) * tAccelRotFactor;

		// Collision detection
		if (checkForCollisions)
		{
			// Ignore the target object (if it's a physics body)
			Rid[] excludes;
			if (target is PhysicsBody3D pb)
				excludes = new Rid[] { pb.GetRid() };
			else
				excludes = new Rid[0];
			
			// Perform the raycast
			var result = this.Raycast(target.GlobalPosition, targetPosition, excludes, collisionMask);
			
			if (result.Count > 0) // If ray hit something
			{
				// Set the target position to the collision point (minus a specified margin)
				Vector3 rayVector = targetPosition - target.GlobalPosition;
				targetPosition = result["position"].As<Vector3>() - collisionMargin * rayVector.Normalized();
			}
		}

		position = targetPosition;
		rotation = targetRotation;
	}

	private void ProcessFreeCam(double delta, ref Vector3 position, ref Vector3 rotation)
	{
		// TODO: Improve the way input actions are retrieved. Using a set of strings to retrieve an input vector in several scripts is not maintainable.
		Vector2 moveInput = Input.GetVector("Steer Left", "Steer Right", "Brake", "Accelerate");
		float moveVertical = (Input.IsKeyPressed(Key.Ctrl) ? -1f : 0f) + (Input.IsKeyPressed(Key.Space) ? 1f : 0f);
		Vector3 moveVector = GlobalBasis * new Vector3(
			moveInput.X,
			moveVertical,
			-moveInput.Y
		);
		
		Vector3 targetPosition = GlobalPosition + moveVector * freeCamSpeed * (Input.IsKeyPressed(Key.Shift) ? 2f : 1f) * (float)delta;
		Vector3 targetRotation = GlobalRotation - new Vector3(inputLookSmoothed.Y, inputLookSmoothed.X, 0f) * (float)delta;
		
		position = targetPosition;
		rotation = targetRotation;
	}

	private void ProcessShakeObjects(ref Vector3 position, ref Vector3 rotation)
	{
		Vector3 posOffsetSum = Vector3.Zero;
		Basis rotOffsetSum = Basis.Identity;
		foreach (var sh in ShakeObject.All)
		{
			// Use the target as reference point for shake objects
			posOffsetSum += sh.GetShakeTranslation(target.IsValid() ? target : this);
			rotOffsetSum *= sh.GetShakeRotation(target.IsValid() ? target : this);
		}
		
		position += posOffsetSum;
		rotation *= rotOffsetSum;
	}

	public static void SetFreeCam(bool enable)
	{
		FreeCamEnabled = enable;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
		{
			if (motion.Relative != Vector2.Zero && Input.MouseMode != Input.MouseModeEnum.Visible)
			{
				lastLookInputTime = Time.GetTicksMsec();
				inputLook = motion.Relative;
			}
		}
	}
}
