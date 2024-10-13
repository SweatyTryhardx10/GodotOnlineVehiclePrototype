using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public partial class DebugOverlay : Control
{
	public static DebugOverlay Instance { get; private set; }

	private Dictionary<ulong, Label> labels = new();
	private Dictionary<string, Label> screenLabels = new();

	private static LabelSettings labelSettings;
	private static LabelSettings labelSettingsBig;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey iek && iek.Pressed && iek.Keycode == Key.O)
		{
			Visible = !Visible;
		}
	}

	public override void _Ready()
	{
		Font font = ResourceLoader.Load<Font>("res://Fonts/CONSOLA.TTF");
		labelSettings = new LabelSettings()
		{
			Font = font,
			FontSize = 9,
			LineSpacing = 0.1f,
			ShadowColor = new Color(Colors.Black, 0.5f),
			ShadowOffset = new Vector2(1f, 2f),
		};

		labelSettingsBig = new LabelSettings()
		{
			Font = font,
			FontSize = 16,
			LineSpacing = 0.1f,
			ShadowColor = new Color(Colors.Black, 0.5f),
			ShadowOffset = new Vector2(1f, 2f),
		};
		
		// Maximize overlay to viewport
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>
	/// NOTE: Do not use this to display text that tracks 3D object.
	/// </summary>
	/// <param name="screenPosition">The normalized screen position for the text.</param>
	public static async void ShowTextOnScreen(string text, Vector2 screenPosition, string id)
	{
		if (!Instance.IsValid())
			return;

		var label = AssertScreenLabelExistence(id);

		label.Text = text;
		
		// Await for the Debug Overlay's layout to be updated (as a result of the label's text-change)
		// NOTE: Failing to wait for the update will invalidate the 'label.size' variable; it will be 0.
		await Instance.ToSignal(Instance, SignalName.Draw);
		
		// label.Position = new Vector2(screenPosition.X * Instance.Size.X, screenPosition.Y * Instance.Size.Y);
		label.Position = screenPosition * Instance.Size - label.Size / 2f;
	}

	public static void ShowTextAtPosition(string text, Vector3 globalPosition, ulong id)
	{
		if (!Instance.IsValid())
			return;

		var label = AssertLabelExistenceForID(id);

		label.Text = text;
		((DebugLabel)label).Mode = DebugLabel.FollowMode.Position;
		((DebugLabel)label).PositionToFollow = globalPosition;
	}

	public static void ShowTextAtNode(string text, Node3D node)
	{
		if (!Instance.IsValid())
			return;

		var label = AssertLabelExistenceForID(node.GetInstanceId());

		label.Text = text;
		((DebugLabel)label).Mode = DebugLabel.FollowMode.Node;
		((DebugLabel)label).NodeToFollow = node;
	}

	public static void ShowTextAtNode(string text, Node3D node, Color color)
	{
		if (!Instance.IsValid())
			return;

		var label = AssertLabelExistenceForID(node.GetInstanceId());

		label.Text = text;
		((DebugLabel)label).Modulate = color;
		((DebugLabel)label).Mode = DebugLabel.FollowMode.Node;
		((DebugLabel)label).NodeToFollow = node;
	}

	private static Label AssertLabelExistenceForID(ulong id)
	{
		// Assert <id, label> pair; create new label if not valid.
		Label label;
		if (!Instance.labels.ContainsKey(id) || !Instance.labels[id].IsValid())
		{
			label = new DebugLabel();
			label.LabelSettings = labelSettings;

			Instance.AddChild(label);

			if (Instance.labels.ContainsKey(id))
				Instance.labels[id] = label;
			else
				Instance.labels.Add(id, label);
		}
		else
		{
			label = Instance.labels[id];
		}

		return label;
	}

	private static Label AssertScreenLabelExistence(string id)
	{
		Label label;
		if (!Instance.screenLabels.ContainsKey(id) || !Instance.screenLabels[id].IsValid())
		{
			label = new DebugLabel();
			label.LabelSettings = labelSettingsBig;

			Instance.AddChild(label);

			if (Instance.screenLabels.ContainsKey(id))
				Instance.screenLabels[id] = label;
			else
				Instance.screenLabels.Add(id, label);
		}
		else
		{
			label = Instance.screenLabels[id];
		}

		return label;
	}
}
