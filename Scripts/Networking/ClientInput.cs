using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ClientInput : Node
{
	// Constructor
	public ClientInput(long peerID)
	{
		this.peerID = peerID;
	}

	private MultiplayerApi mp;

	private long peerID;
	private bool isLocal;
	private VehicleController vehicle;
	private List<Node> controlNodes = new(); // Nodes that can be controlled w. input

	private const string INPUTLEFT = "Steer Left";
	private const string INPUTRIGHT = "Steer Right";
	private const string INPUTFORWARD = "Accelerate";
	private const string INPUTBACK = "Brake";

	// The input received from another client
	private float netInputThrottle;
	private float netInputSteering;
	private float netInputControlA;

	private bool inFocus = true;    // Used to disable input (from any source) when the window is not in focus.

	public override void _Ready()
	{
		// Determine if this node is for the local client
		mp = GetTree().GetMultiplayer();
		if (mp.GetUniqueId() == peerID)
		{
			isLocal = true;
		}

		// Get a reference to the vehicle controller
		if (GetParent() is VehicleController vc)
		{
			vehicle = vc;
		}

		// Get references to all control nodes (e.g. helicopter joint, hydraulics etc.)
		// NOTE: Polymorphism via explicit function calls and object naming.
		controlNodes = GetParent().GetAllNodes<Node>(true).Where(n => ((string)n.Name).Contains("CTRL_")).ToList();
		
		string msg = string.Empty;
		foreach (Node n in controlNodes)
		{
			msg += $"Node: {n.Name}\n";
		}
		GD.Print(msg);
	}

	public override void _PhysicsProcess(double delta)
	{
		// Disable input when window is not in focus
		if (!inFocus)
			return;

		// Control the vehicle if this is determined to be owned by the local client
		if (isLocal || peerID == -1)	// NOTE: a peer ID of -1 means the client is not considered part of a network.
		{
			float throttle = Input.GetAxis(INPUTBACK, INPUTFORWARD);
			float steering = Input.GetAxis(INPUTLEFT, INPUTRIGHT);
			bool ctrlA = Input.IsActionJustPressed("Control A");
			bool ctrlB = Input.IsActionJustPressed("Control B");

			vehicle.throttle = throttle;
			vehicle.steering = steering;

			// Send local vehicle input to other peers
			if (mp.IsValid() && peerID != -1)
				Rpc(MethodName.PerformInput, steering, throttle);

			// Send control action to other peers
			if (ctrlA)
			{
				// Rpc(MethodName.PerformControlInputA);
				PerformControlInputA();
			}
			if (ctrlB)
			{
				PerformControlInputB();
			}
		}
		else
		{
			// TODO: Apply input data received from packets
			vehicle.throttle = netInputThrottle;
			vehicle.steering = netInputSteering;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (inFocus)
		{
			if (@event.IsActionPressed("Reset") && (isLocal || peerID == -1))
			{
				vehicle.QueueReset();
			}
			if (@event is InputEventKey iek && iek.Keycode == Key.R && iek.Pressed && !iek.IsEcho())
			{
				Transform3D newTx = new Transform3D(
					Basis.Identity,
					vehicle.GlobalPosition + Vector3.Up * 3f
				);
				vehicle.QueuePhysicsState(newTx, Vector3.Zero, Vector3.Zero);
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	public void PerformInput(float x, float y)
	{
		netInputThrottle = y;
		netInputSteering = x;
	}

	// [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	// Remote Procedure Calls are to be performed by the controls themselves (I think).
	//  + This allows controls to handle the local/remote peer distinction themselves
	//  % Netcode is introduced into each and every node that should be controlled by the player.
	public void PerformControlInputA()
	{
		foreach (Node n in controlNodes)
		{
			if (n.HasMethod("UseA"))
				n.Call("UseA");
		}
	}

	public void PerformControlInputB()
	{
		foreach (Node n in controlNodes)
		{
			if (n.HasMethod("UseB"))
				n.Call("UseB");
		}
	}

	public override void _Notification(int what)
	{
		if (what is (int)NotificationApplicationFocusIn)
		{
			inFocus = true;
		}
		if (what is (int)NotificationApplicationFocusOut)
		{
			inFocus = false;
		}
	}
}
