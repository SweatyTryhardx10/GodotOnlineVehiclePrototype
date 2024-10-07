using Godot;
using System;
using System.Collections.Generic;

public partial class HUD : Node
{
	private static HUD Instance;

	public enum HUDMode
	{
		Default,
		PauseScreen,
		Scoreboard,
		EndScreen,
		EndScreenServer,
		Options,
		None    // This should always be the last value
	}

	// NOTE: Each element in this array corresponds to a specific screen based on index (see enum above).
	[Export] private Control[] panelValues;

	private HUDMode currentMode;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		ShowScreen(HUDMode.None);

		// Hide this HUD when the server is closed.
		var lobby = GetNode<NetLobby>("/root/Lobby");
		lobby.ServerDisconnected += () => ShowScreen(HUDMode.None);
	}

	public override void _Input(InputEvent @event)
	{
		// Pause screen input
		if (@event is InputEventKey iek && iek.Keycode == Key.Escape && iek.IsPressed())
		{
			if (currentMode == HUDMode.PauseScreen)
			{
				ShowScreen(HUDMode.Default);
			}
			else
			{
				ShowScreen(HUDMode.PauseScreen);
			}
		}
	}

	public static void ShowScreen(HUDMode screen)
	{
		if (!Instance.IsValid())
			return;

		for (int i = 0; i < Instance.panelValues.Length; i++)
		{
			if (i == (int)screen)
			{
				if (Instance.panelValues[i].IsValid())
				{
					Instance.panelValues[i].Visible = true;
					Instance.currentMode = screen;
				}
			}
			else
			{
				if (Instance.panelValues[i].IsValid())
				{
					Instance.panelValues[i].Visible = false;
				}
			}
		}

		// Screen-specific mouse mode
		if (screen == HUDMode.EndScreenServer || screen == HUDMode.PauseScreen || screen == HUDMode.Options)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
		else
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}

		// GD.PushWarning($"The {screen.ToString()} HUD element has not been accounted for when changing screen!");
	}

	#region BUTTON CALLBACKS

	private void OnPauseButtonResumePressed()
	{
		ShowScreen(HUDMode.Default);
	}

	private void OnPauseButtonOptionsPressed()
	{
		ShowScreen(HUDMode.Options);
	}

	private void OnPauseButtonDisconnectPressed()
	{
		this.TreeMP().MultiplayerPeer.Close();	// I suppose this is how to close the connection.
		
		ShowScreen(HUDMode.None);
	}

	private void OnEndButtonRematchPressed()
	{
		var game = GetNode<NetGame>("/root/Game");
		game.RestartGame();

		ShowScreen(HUDMode.Default);
	}

	private void OnEndButtonShutDownPressed()
	{
		this.TreeMP().MultiplayerPeer.Close();	// I suppose this is how to close the server as well.

		ShowScreen(HUDMode.None);
	}

	#endregion
}