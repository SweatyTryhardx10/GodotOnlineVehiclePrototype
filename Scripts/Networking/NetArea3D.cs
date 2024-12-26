using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Implements network synchronization for an Area3D.
/// </summary>
public partial class NetArea3D : Area3D
{
	[Signal] public delegate void NetBodyEnteredEventHandler(Node3D n);
	[Signal] public delegate void NetBodyExitedEventHandler(Node3D n);
	
	private List<string> bodiesInArea = new();    // Tracks bodies that have entered but not exited. Used to filter incoming RPC calls
	// NOTE: Currently, the path to the body is used since instance IDs do not match between clients.
	// 		There should be no problem with this approach but care must be taken when other systems start to create new instances; ...
	//		...the path for a single instance must match for all clients on the network (including the server).

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
    }

	private void OnBodyEntered(Node3D n)
	{
		Rpc(MethodName.RegisterNetBody, n.GetPath());
	}
	
	private void OnBodyExited(Node3D n)
	{
		Rpc(MethodName.DeregisterNetBody, n.GetPath());
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RegisterNetBody(string instancePath)
	{
		if (!bodiesInArea.Contains(instancePath))
		{
			bodiesInArea.Add(instancePath);
			
			// Emit signal with the node on the provided path as an argument
			Node n = GetNode(instancePath);
			EmitSignal(SignalName.NetBodyEntered, n);
		}
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void DeregisterNetBody(string instancePath)
	{
		if (bodiesInArea.Contains(instancePath))
		{
			bodiesInArea.Remove(instancePath);
			
			// Emit signal with the node on the provided path as an argument
			Node n = GetNode(instancePath);
			EmitSignal(SignalName.NetBodyExited, n);
		}
	}
}
