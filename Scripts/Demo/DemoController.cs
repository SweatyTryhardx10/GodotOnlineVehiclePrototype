using Godot;
using System;
using System.ComponentModel.DataAnnotations;

public partial class DemoController : Node
{
	[Export] private PackedScene[] playerObjectList = new PackedScene[0];
	[Export] private Node3D spawnNode;

	private Node3D currentPlayerObject;
	private int currentObjectIndex;     // Used to remember the source object's index in the object list

	public override void _Ready()
	{
		ChangePlayerObject(0);

		HUD.ShowScreen(HUD.HUDMode.Demo);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey iek && iek.Pressed && !iek.IsEcho())
		{
			switch (iek.Keycode)
			{
				case Key.Key1:
					ChangePlayerObject(0);
					break;
				case Key.Key2:
					ChangePlayerObject(1);
					break;
				case Key.Key3:
					ChangePlayerObject(2);
					break;
				case Key.Key4:
					ChangePlayerObject(3);
					break;
				case Key.Key5:
					ChangePlayerObject(4);
					break;
				case Key.Key6:
					ChangePlayerObject(5);
					break;
				case Key.Key7:
					ChangePlayerObject(6);
					break;
				case Key.Key8:
					ChangePlayerObject(7);
					break;
				case Key.Key9:
					ChangePlayerObject(8);
					break;

				// case Key.R:
				// 	ChangePlayerObject(currentObjectIndex, true);
				// 	break;

				case Key.M:
					CameraController.FreeCamEnabled = !CameraController.FreeCamEnabled;
					
					if (currentPlayerObject.TryGetNode(out ClientInput ci))
					{
						ci.SetProcess(!CameraController.FreeCamEnabled);
						ci.SetPhysicsProcess(!CameraController.FreeCamEnabled);
					}
					
					if (CameraController.FreeCamEnabled)
						DebugOverlay.ShowTextOnScreen("Free Cam Enabled!", new Vector2(0.5f, 0.8f), "Free cam status");
					else
						DebugOverlay.ShowTextOnScreen(string.Empty, new Vector2(0.5f, 0.8f), "Free cam status");
					break;

				default:
					break;
			}
		}
	}

	private async void ChangePlayerObject(int idx, bool placeAtSpawn = false)
	{
		if (idx >= 0 && idx < playerObjectList.Length)
		{
			if (!playerObjectList[idx].IsValid())
				return;
			
			// ============== Remove current player object ==============
			Transform3D currentPlayerTransform = spawnNode.IsValid() ? spawnNode.GlobalTransform : Transform3D.Identity;
			if (currentPlayerObject.IsValid())
			{
				if (!placeAtSpawn)
					currentPlayerTransform = new Transform3D(currentPlayerObject.GlobalTransform.Basis, currentPlayerObject.GlobalTransform.Origin + Vector3.Up * 2f);

				GD.Print($"Current player origin: {currentPlayerTransform.Origin}");
				
				currentPlayerObject.QueueFree();
				await ToSignal(currentPlayerObject, Node.SignalName.TreeExited);
			}
			// ==========================================================

			// Instantiate the new player object
			var newPlayerObject = playerObjectList[idx].Instantiate<Node3D>();

			// Add client input
			var inputNode = new ClientInput(-1);
			newPlayerObject.AddChild(inputNode);

			// Replicate the transform of the previous object (upon entering the tree)
			newPlayerObject.TreeEntered += () => { newPlayerObject.GlobalTransform = currentPlayerTransform; GD.Print($"new position: {currentPlayerTransform.Origin}"); };

			// Add the new player object to the tree
			AddChild(newPlayerObject);

			// Set the new player object as the camera target
			CameraController.Instance.target = newPlayerObject;

			currentPlayerObject = newPlayerObject;
			currentObjectIndex = idx;
		}
	}
}
