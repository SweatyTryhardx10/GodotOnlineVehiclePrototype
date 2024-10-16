using Godot;
using System;
using System.Collections.Generic;

public partial class ServerMenu : Control
{
	// [Export] private PackedScene playerListEntryPrefab;
	[Export] private Control playerListContainer;
	[Export] private Label vehicleTypeLabel;

	[ExportGroup("References")]
	[Export] private PackedScene playerListEntryPrefab;

	private Dictionary<long, Node> playerInfoElements = new();

	public override void _Ready()
	{
		var lobby = GetNode<NetLobby>("/root/Lobby");

		lobby.PlayerConnected += OnPlayerConnected;
		lobby.PlayerDisconnected += OnPlayerDisconnected;
		lobby.ServerDisconnected += OnServerDisconnected;
	}

	public override void _ExitTree()
	{
		var lobby = GetNode<NetLobby>("/root/Lobby");

		lobby.PlayerConnected -= OnPlayerConnected;
		lobby.PlayerDisconnected -= OnPlayerDisconnected;
		lobby.ServerDisconnected -= OnServerDisconnected;
	}

	private void OnPlayerConnected(long playerID, Godot.Collections.Dictionary info)
	{
		var clientID = GetTree().GetMultiplayer().GetUniqueId();
		
		var entry = playerListEntryPrefab.Instantiate<Control>();
		
		entry.GetNode<Label>("MarginContainer/HBoxContainer/Name").Text = info["Name"].As<string>();
		entry.GetNode<Label>("MarginContainer/HBoxContainer/ID").Text = playerID == 1 ? "Host" : $"#{playerID}";
		entry.GetNode<Label>("MarginContainer/HBoxContainer/Connection").Text = "connected";
		entry.GetNode<Label>("MarginContainer/HBoxContainer/Connection").AddThemeColorOverride("font_color", Colors.Green);

		playerListContainer.AddChild(entry);
		playerInfoElements.Add(playerID, entry);

		// Initialize menu configuration
		if (playerID == clientID)
		{
			var lobby = GetNode<NetLobby>("/root/Lobby");
			if (lobby.players[clientID].TryGetValue("Vehicle", out var vIdx))
			vehicleTypeLabel.Text = Util.GetVehicleTypes()[(int)vIdx];
		}
	}

	private void OnPlayerDisconnected(long playerID)
	{
		if (playerInfoElements.ContainsKey(playerID))
		{
			playerInfoElements[playerID].QueueFree();
		}
	}

	private void OnServerDisconnected()
	{
		GetTree().ReloadCurrentScene();
	}

	// =============================================================
	// ======== THE METHODS BELOW NEED SEVERE REFACTORING! =========
	// =============================================================

	/// <summary>NOTE: NEEDS REFACTORING!</summary>
	private void OnChangeVehicleTypeNextButtonPressed()
	{
		var mp = GetTree().GetMultiplayer();
		var lobby = GetNode<NetLobby>("/root/Lobby");

		Godot.Collections.Dictionary info = lobby.players[mp.GetUniqueId()];

		if (!info.ContainsKey("Vehicle"))
			info.Add("Vehicle", 0);

		string[] vehicleTypes = Util.GetVehicleTypes();
		
		int newValue = Mathf.PosMod((int)info["Vehicle"] + 1, vehicleTypes.Length);
		Rpc(MethodName.SyncProperty, mp.GetUniqueId(), "Vehicle", newValue);
	}

	/// <summary>NOTE: NEEDS REFACTORING!</summary>
	private void OnChangeVehicleTypePreviousButtonPressed()
	{
		var mp = GetTree().GetMultiplayer();
		var lobby = GetNode<NetLobby>("/root/Lobby");

		Godot.Collections.Dictionary info = lobby.players[mp.GetUniqueId()];

		if (!info.ContainsKey("Vehicle"))
			info.Add("Vehicle", 0);

		string[] vehicleTypes = Util.GetVehicleTypes();

		int newValue = Mathf.PosMod((int)info["Vehicle"] - 1, vehicleTypes.Length);
		Rpc(MethodName.SyncProperty, mp.GetUniqueId(), "Vehicle", newValue);
	}

	/// <summary>NOTE: NEEDS REFACTORING!</summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SyncProperty(long peerID, Variant key, Variant value)
	{
		var mp = GetTree().GetMultiplayer();
		var lobby = GetNode<NetLobby>("/root/Lobby");

		if (key.As<string>() == "Vehicle" && peerID == mp.GetUniqueId())
			vehicleTypeLabel.Text = Util.GetVehicleTypes()[(int)value];

		lobby.players[peerID][key] = value;
	}
}
