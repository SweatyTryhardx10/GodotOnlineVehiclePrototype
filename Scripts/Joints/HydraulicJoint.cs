using Godot;
using System;

public partial class HydraulicJoint : Node3D
{
	[Export] private Node3D bodyA;
	[Export] private Node3D bodyB;

	[ExportGroup("Hydraulic Settings")]
	[Export] private float minLength = 0f;
	[Export] private float maxLength = 1f;
	[Export] private float displacementSpeed = 2f;
	public float Length { get; set; }
	private float currentLength;

	[ExportGroup("CTRL Settings")]
	[Export] private bool invertX;
	[Export] private bool invertY;

	private Shape3D shapeData;
	private CollisionShape3D shape;

	public override void _Ready()
	{
		// // Configure new collision shape
		// shapeData = new CapsuleShape3D() { Height = Length, Radius = 0.1f };
		// shape = new CollisionShape3D() { Shape = shapeData };

		// // Add as sibling (after parent is done building the node tree)
		// GetParent().CallDeferred(MethodName.AddChild, shape);
	}

	public override void _PhysicsProcess(double delta)
	{
		Node3D body = bodyA.IsValid() ? bodyA : bodyB;
		if (body.IsValid())                         // Anchor behaviour
		{
			currentLength = Mathf.MoveToward(currentLength, Length, displacementSpeed * (float)delta);
			Vector3 bodyPosition = GlobalPosition - GlobalBasis.Y * currentLength;

			body.GlobalPosition = bodyPosition;
		}
		else                                        // Rod behaviour
		{
			throw new NotImplementedException("Rod behaviour has not yet been implemented!");
		}

		UpdateShape();
	}

	private void UpdateShape()
	{
		if (shapeData is CapsuleShape3D cs)
		{
			cs.Height = Length;
		}
	}

	public void UseB()
	{
		// TODO: Retrieve input from a static class to avoid excessive work on renaming
		float inputX = Input.GetAxis("Steer Left", "Steer Right");
		float inputY = Input.GetAxis("Steer Down", "Steer Up");

		float xMod = Mathf.Max(0f, inputX * (invertX ? -1f : 1f));
		float yMod = Mathf.Max(0f, inputY * (invertY ? -1f : 1f));
		float inputMod = Mathf.Max(xMod, yMod);
		float newLength = inputMod * maxLength;

		GD.Print($"Length: {newLength}");

		if (newLength != Length)
		{
			Rpc(MethodName.SetB, newLength);
		}
	}

	// NOTE: Set to unreliable transfer mode if granular input is desired.
	//   ...However, the last Rpc call (in a sequence) should always be reliable - otherwise desync might occur!
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetB(float newLength)
	{
		Length = newLength;
	}
}
