using Godot;
using System;

public partial class AutoMover : Node
{
	[Export] private Vector3 linearVelocity;
	[Export] private Vector3 angularVelocity;

	[Export] private bool usePhysicsRate = false;
	[Export] private bool useLocalSpace;

	private Node3D target;

	public override void _Ready()
	{
		target = GetParent<Node3D>();
	}

	public override void _Process(double delta)
	{
		if (!usePhysicsRate)
		{
			MoveTarget(delta);
		}
	}

    public override void _PhysicsProcess(double delta)
    {
        if (usePhysicsRate)
		{
			MoveTarget(delta);
		}
    }

    private void MoveTarget(double delta)
	{
		Vector3 deltaPos = linearVelocity;
		Vector3 deltaRot = angularVelocity;

		if (useLocalSpace)
		{
			deltaPos = target.GlobalTransform.Inverse() * deltaPos;
			deltaRot = target.GlobalTransform.Inverse() * deltaRot;
		}

		// Perform transformation changes
		target.GlobalPosition += deltaPos * (float)delta;
		target.GlobalRotation += deltaRot * (float)delta;
	}
}
