using Godot;
using System;

public partial class DebugLabel : Label
{
	public DebugLabel() {
		ProcessPriority = 100;
	}

	public DebugLabel(string text, Vector3 position) : this()
	{
		Mode = FollowMode.Position;

		Text = text;
		PositionToFollow = position;
	}
	
	public DebugLabel(string text, Node3D node) : this()
	{
		Mode = FollowMode.Node;
		
		Text = text;
		NodeToFollow = node;
	}

	public enum FollowMode
	{
		None,
		Position,
		Node
	}

	private const float MAX_VISIBLE_RANGE = 100f;

	public FollowMode Mode { get; set; }
	public Vector3 PositionToFollow { get; set; }
	public Node3D NodeToFollow { get; set; }

	public override void _Process(double delta)
	{
		Vector3 followPos = Vector3.Zero;

        switch (Mode)
        {
            case FollowMode.Position:
				followPos = PositionToFollow;
                break;
            case FollowMode.Node:
				if (NodeToFollow.IsValid() && NodeToFollow.IsInsideTree())
					followPos = NodeToFollow.GlobalPosition;
                break;
            default:
				followPos = Vector3.Zero;
				break;
        }

        if (Mode != FollowMode.None)
		{
			Camera3D cam = GetViewport().GetCamera3D();

			if (!cam.IsValid())
			{
				// GD.Print($"{Name}: The camera is not valid.");
				QueueFree();
				return;
			}

			Vector2 viewportSize = GetViewportRect().Size;

			// float borderWidth = 30f;
			Vector2 uiPos = cam.UnprojectPosition(followPos);
			float x = uiPos.X - Size.X / 2f;
			float y = uiPos.Y - Size.Y / 2f;

			// Flip position if target is behind camera
			if (cam.IsPositionBehind(followPos))
			{
				x += ((viewportSize.X / 2f) - x) * 2f;
				y += ((viewportSize.Y / 2f) - y) * 2f;

				// Hide element
				Modulate = Color.FromHsv(0f, 0f, 1f, 0f);
			}
			else
			{
				// Distance Fade
				Vector3 toTarget = followPos - cam.GlobalPosition;
				float alpha = Mathf.Clamp(MAX_VISIBLE_RANGE - toTarget.Length(), 0f, 1f);
				Modulate = Color.FromHsv(0f, 0f, 1f, alpha);
			}

			// Set position
			Position = new Vector2(x, y);
		}
    }
}
