using Godot;
using System;

public partial class HydraulicSystem : Node3D
{
	private HydraulicJoint[] joints;

	public override void _Ready()
	{
		joints = this.GetAllNodes<HydraulicJoint>();
	}

	public void UseB()
	{

	}

	private void SetB()
	{
		
	}
}
