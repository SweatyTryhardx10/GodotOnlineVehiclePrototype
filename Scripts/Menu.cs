using Godot;
using System;

public partial class Menu : Control
{
	[Export] private MenuButton quitButton;
	[Export] private PopupPanel quitPopup;

	private const string MULTIPLAYER_GAME_SCENE_PATH = "res://Scenes/Level_02.tscn";

	public override void _Ready()
	{
		// Make the mouse visible (in case it was hidden after leaving a game)
		Input.MouseMode = Input.MouseModeEnum.Visible;

		// Subscribe to the Hunt-mocking quit button
		var popup = quitButton.GetPopup();
		popup.IdPressed += (id) =>
		{
			if (id == 0)
			{
				quitPopup.Show();
			}
		};
	}

	#region Hunt-mocking UI methods

	private void OnQuitAccepted()
	{
		GetTree().Quit();
	}
	
	private void OnQuitDeclined()
	{
		quitPopup.Hide();
	}

	#endregion

	public void OnPlayDemoButtonPressed()
	{
		string demoScenePath = "res://Scenes/Level_Demo.tscn";
		GetTree().ChangeSceneToFile(demoScenePath);
	}

	public void OnCreateGameButtonPressed()
	{
		GetNode<NetLobby>("/root/Lobby").CreateGame();

		// var timer = GetTree().CreateTimer(1.0);
		// await ToSignal(timer, Timer.SignalName.Timeout);

		// Show Server Overview (Player list, Start game button etc.)
		GetNode<ServerMenu>("Server Panel").Show();
		GetNode<Control>("Button Panel").Hide();
	}

	public async void OnJoinGameButtonPressed()
	{
		// Show ip address menu
		GetNode<Control>("Address Panel").Show();
		GetNode<Control>("Button Panel").Hide();
	}

	public void OnConnectButtonPressed()
	{
		var address = GetNode<TextEdit>("Address Panel/Text Field").Text;
		var error = GetNode<NetLobby>("/root/Lobby").JoinGame(address);

		if (error == Error.Ok)
		{
			// Show server overview (in client mode)
			GetNode<ServerMenu>("Server Panel").Show();
			GetNode<Control>("Address Panel").Hide();
		}
		else
		{
			GD.PushError($"Connection error: {error}");
			GetNode<Control>("Button Panel").Show();
			GetNode<Control>("Address Panel").Hide();
		}
	}

	public void OnStartGameButtonPressed()
	{
		if (GetTree().GetMultiplayer().IsServer())
		{
			// Load all players into the game scene
			GetNode<NetLobby>("/root/Lobby").Rpc(NetLobby.MethodName.LoadGame, MULTIPLAYER_GAME_SCENE_PATH);
		}
	}
}
