using Godot;
using System;
using System.Collections.Generic;

using RPCMode = Godot.MultiplayerApi.RpcMode;
using TranMode = Godot.MultiplayerPeer.TransferModeEnum;

public partial class NetLobby : Node
{
	[Signal] public delegate void PlayerConnectedEventHandler(long playerID, Godot.Collections.Dictionary info);
	[Signal] public delegate void PlayerDisconnectedEventHandler(long playerID);
	[Signal] public delegate void ServerDisconnectedEventHandler();

	const int PORT = 7000;
	const string DEFAULT_SERVER_IP = "127.0.0.1";
	const int MAX_CONNECTIONS = 20;
	
	/// <summary>Don't use this!</summary>
	[Obsolete]
	const string GAME_NODE_PATH = "/root/Game";

	// Contains player info with the player ID as key
	public Dictionary<long, Godot.Collections.Dictionary> players { get; private set; } = new();

	private Godot.Collections.Dictionary playerInfo = new Godot.Collections.Dictionary() {
		{"Name", "John Doe"},
		{"Vehicle", 0}
	};

	private int playersLoaded = 0;

	public override void _Ready()
	{
		var mp = GetTree().GetMultiplayer();

		mp.PeerConnected += OnPlayerConnected;
		mp.PeerDisconnected += OnPlayerDisconnected;
		mp.ConnectedToServer += OnConnectedOk;
		mp.ConnectionFailed += OnConnectedFail;
		mp.ServerDisconnected += OnServerDisconnected;
	}

	public Error JoinGame(string address)
	{
		if (string.IsNullOrEmpty(address))
			address = DEFAULT_SERVER_IP;

		var peer = new ENetMultiplayerPeer();
		var error = peer.CreateClient(address, PORT);

		if (error == Error.Ok)
		{
			var mp = GetTree().GetMultiplayer();
			mp.MultiplayerPeer = peer;
		}

		return error;
	}

	public Error CreateGame()
	{

		var peer = new ENetMultiplayerPeer();
		var error = peer.CreateServer(PORT, MAX_CONNECTIONS);

		if (error == Error.Ok)
		{
			var mp = GetTree().GetMultiplayer();
			mp.MultiplayerPeer = peer;

			players[1] = playerInfo;
			EmitSignal(SignalName.PlayerConnected, 1, playerInfo);
		}

		return error;
	}

	// THIS IS NOT USED. WTF
	public void RemoveMultiplayerPeer()
	{
		var mp = GetTree().GetMultiplayer();
		mp.MultiplayerPeer = null;
	}

	#region RPC Methods

	[Rpc(CallLocal = true, TransferMode = TranMode.Reliable)]
	public void LoadGame(string gameScenePath)
	{
		GetTree().ChangeSceneToFile(gameScenePath);
	}

	// Received from all connected clients (players).
	// The game is started once all players have loaded into the map.
	[Rpc(RPCMode.AnyPeer, CallLocal = true, TransferMode = TranMode.Reliable)]
	public void PlayerLoaded()
	{
		if (GetTree().GetMultiplayer().IsServer())
		{
			playersLoaded += 1;

			GD.Print("Player has loaded!");

			if (playersLoaded == players.Count)
			{
				GetNode<NetGame>(GAME_NODE_PATH).StartGame();
				playersLoaded = 0;
			}
		}
	}

	[Rpc(RPCMode.AnyPeer, TransferMode = TranMode.Reliable)]
	public void RegisterPlayer(Godot.Collections.Dictionary newPlayerInfo)
	{
		var mp = GetTree().GetMultiplayer();		
		int newPlayerId = mp.GetRemoteSenderId();
		players[newPlayerId] = newPlayerInfo;
		
		EmitSignal(SignalName.PlayerConnected, newPlayerId, newPlayerInfo);
		
		GD.Print($"({mp.GetUniqueId()}) Registering player (ID: {newPlayerId})");
	}

	#endregion

	#region Callback Methods

	private void OnPlayerConnected(long id)
	{	
		RpcId(id, MethodName.RegisterPlayer, playerInfo);
	}

	private void OnPlayerDisconnected(long id)
	{
		players.Remove(id);
		EmitSignal(SignalName.PlayerDisconnected, id);
	}

	private void OnConnectedOk()
	{
		var peerID = GetTree().GetMultiplayer().GetUniqueId();
		players[peerID] = playerInfo;
		EmitSignal(SignalName.PlayerConnected, peerID, playerInfo);
		
		GD.Print("Connected OK!");
	}

	private void OnConnectedFail()
	{
		GetTree().GetMultiplayer().MultiplayerPeer = null;
		GD.PushWarning("Failed connection attempt logic needs to be implemented!");
	}

	private void OnServerDisconnected()
	{
		GetTree().GetMultiplayer().MultiplayerPeer = null;
		players.Clear();
		EmitSignal(SignalName.ServerDisconnected);
		GD.PushWarning("Server disconnected logic needs to be implemented!");
	}
	
	#endregion
}
