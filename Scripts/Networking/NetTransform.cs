using Godot;
using System;

public partial class NetTransform : Node
{
	public NetTransform(long peerID)
	{
		this.peerID = peerID;
	}

	private MultiplayerApi mp;

	private long peerID;
	private bool isLocal;

	private RigidBody3D body;

	// // The physics configuration received from another clients
	// private Transform3D netTransform;
	// private Vector3 netVelocity;
	// private Vector3 netAngularVelocity;

	public override void _Ready()
	{
		// Determine if this node is for the local client
		mp = GetTree().GetMultiplayer();
		if (mp.GetUniqueId() == peerID)
		{
			isLocal = true;
		}

		// Retrieve a reference to the physics body
		Node3D p = GetParent<Node3D>();
		if (p is RigidBody3D rb)
		{
			body = rb;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (isLocal)
		{
			if (mp.IsValid())
				Rpc(MethodName.ApplyPhysicsConfiguration, body.Transform, body.LinearVelocity, body.AngularVelocity);
		}
	}

	// NOTE: Change to "Authority Only" at a later date when server-authoritative physics is desired
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	public void ApplyPhysicsConfiguration(Transform3D tx, Vector3 linearVelocity, Vector3 angularVelocity)
	{
		// netTransform = tx;
		// netVelocity = linearVelocity; 
		// netAngularVelocity = angularVelocity;
		
		// Generic method invocation is used to support future classes
		if (body.HasMethod("QueuePhysicsState"))
			body.Call("QueuePhysicsState", tx, linearVelocity, angularVelocity);
	}
}
