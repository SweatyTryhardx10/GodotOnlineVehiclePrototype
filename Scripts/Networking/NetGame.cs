using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class NetGame : Node3D
{
    private Dictionary<long, Node> playerObjects = new();

    [Export] private Node3D[] spawns = new Node3D[0];
    private List<int> availableSpawns;

    [Export] private AnimationPlayer cinematicAnimation;
    [Export] private string animationString;    // TODO: The animation player should just KNOW what animation to play.
    [Export] private bool skipCinematic;

    public override void _Ready()
    {
        // Calls the RPC Method with a specified peer ID
        //  - Tells the server that this peer has loaded.
        // Q: Why is it '1' and not a dynamic value?
        // A: '1' is, apparently, the server
        var error = GetNode<NetLobby>("/root/Lobby").RpcId(1, NetLobby.MethodName.PlayerLoaded);

        GD.Print($"Error: {error}");

        // // FOR DEBUGGING & DEVELOPMENT ONLY!
        // if (GetTree().GetMultiplayer().HasMultiplayerPeer() && GetTree().GetMultiplayer().GetPeers().Length == 0)
        // {
        //     SpawnPlayerObject(1);
        // }

        // TODO: Move launch arguments to a relevant class
        string[] args = OS.GetCmdlineArgs();
        foreach (string a in args)
        {
            string key = string.Empty;
            string value = string.Empty;
            if (a.Contains(' ') || a.Contains('='))
            {
                var split = a.Split(' ', '=');
                key = split[0];
                value = split[1];
            }
            else
            {
                key = a;
            }

            GD.Print($"Cmd-key: {key}");
            GD.Print($"Cmd-value: {value}");

            if (key == "-mute")
            {
                AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(0f));
            }
        }
    }

    public override void _EnterTree()
    {
        var lobby = GetNode<NetLobby>("/root/Lobby");
        lobby.PlayerDisconnected += RemovePlayerObject;
        lobby.ServerDisconnected += OnServerDisconnected;
    }

    public override void _ExitTree()
    {
        var lobby = GetNode<NetLobby>("/root/Lobby");
        lobby.PlayerDisconnected -= RemovePlayerObject;
        lobby.ServerDisconnected -= OnServerDisconnected;
    }

    public void StartGame()
    {
        if (this.TreeMP().IsServer())
        {
            GameLoop();
        }
    }

    // TODO: Merge this method with StartGame() via a conditional expression
    public void RestartGame()
    {
        // Revert player objects to their initial state
        foreach (var po in playerObjects)
        {
            Health[] healthNodes = po.Value.GetAllNodes<Health>(true);

            for (int i = 0; i < healthNodes.Length; i++)
            {
                healthNodes[i].Reset();
            }
        }

        StartGame();
    }

    public async void GameLoop()
    {
        // Initialize available spawn list
        availableSpawns = new List<int>(spawns.Length);
        for (int i = 0; i < spawns.Length; i++)
        {
            availableSpawns.Add(i);
        }

        // Spawn the player objects for all the connected players
        SpawnPlayerObjects();
        GD.Print($"{Engine.GetPhysicsFrames()} " + "Player objects spawned!");
        SetPlayerInputAll(false);   // Disable all client input

        // Play cinematic and wait for it to finish
        if (!skipCinematic)
            await StartAndWaitForCinematic();

        // TODO: Wait for the cinematic to finish for all peers (i.e. wait for signals from all peers).
        //      This will ensure that players, who are far apart, see the player-view at the same time.

        // Wait 3 seconds
        var timer1 = GetTree().CreateTimer(3f);
        await ToSignal(timer1, Timer.SignalName.Timeout);

        // Enable all client input
        SetPlayerInputAll(true);

        GD.Print($"{Engine.GetPhysicsFrames()} " + "Waiting for winner to emerge!");

        // DEBUG/TEST CONDITION (the game should not end if only one player is in the match)
        var lobby = GetNode<NetLobby>("/root/Lobby");
        while (lobby.players.Count == 1)
        {
            await Task.Delay(1000);
        }

        // Wait for all but one player to die (or for the game timer to run out)
        long[] playersAlive = await WaitForWinner();

        // Focus camera on first winning player
        if (playerObjects[playersAlive[0]] is Node3D p3d)
            CameraController.Instance.target = p3d.GetNode<Node3D>("MeshObject");   // TODO: Please don't retrieve the mesh node like this.
        // NOTE: It's possible that the player object(s) is never not a node3D - but its still good to verify.

        // Show end screen (host has options to end or restart the session)
        Rpc(MethodName.ShowEndScreen);
    }

    private void SpawnPlayerObjects()
    {
        // Spawn every player vehicle for every client
        var lobby = GetNode<NetLobby>("/root/Lobby");
        foreach (var p in lobby.players)
        {
            Transform3D spawnTx = UseRandomSpawn();
            Rpc(MethodName.SpawnPlayerObject, p.Key, spawnTx);
        }

        // TODO: Await "object created"-message from peers
    }

    private async Task StartAndWaitForCinematic()
    {
        if (!cinematicAnimation.IsValid())
            return;

        Rpc(MethodName.SetCinematic, true);

        // // Wait for the animation to start (if it hasn't). Failing to wait for this may skip the next await-signal. (NOPE)
        // if (!cinematicAnimation.IsPlaying())
        //     await ToSignal(cinematicAnimation, AnimationPlayer.SignalName.AnimationStarted);

        await ToSignal(cinematicAnimation, AnimationPlayer.SignalName.AnimationFinished);
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SetCinematic(bool play)
    {
        // TODO: Needs thorough testing; what happens if the cinematic is stopped before finishing?

        if (play)
            cinematicAnimation.Play(animationString);
        else
            cinematicAnimation.Stop();
    }

    private async Task<long[]> WaitForWinner()
    {
        // TODO: Modify alive player count when player(s) disconnect/reconnect.

        // Wait for all but one player to die
        int playersAlive = playerObjects.Count;
        while (playersAlive > 1)
        {
            await Task.Delay(1000);

            // Re-evaluate the alive player count
            // TODO: Only evaluate this when the OnDeath signal on the player's health is emitted.
            playersAlive = playerObjects.Where(p =>
            {
                if (p.Value.TryGetNode(out Health h))
                {
                    if (h.Value > 0)
                        return true;
                    else
                        return false;
                }
                else
                {
                    return false;
                }
            }).ToArray().Length;

            GD.Print($"Players alive: {playersAlive}");
        }

        long[] alivePlayerIDs = playerObjects.Where(p =>
        {
            if (p.Value.TryGetNode(out Health h) && h.Value > 0f)
                return true;
            else
                return false;
        }).Select(p => p.Key).ToArray();

        return alivePlayerIDs;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ShowEndScreen()
    {
        if (this.TreeMP().IsServer())
        {
            // Show server-side end screen
            HUD.ShowScreen(HUD.HUDMode.EndScreenServer);
        }
        else
        {
            // Show client end screen
            HUD.ShowScreen(HUD.HUDMode.EndScreen);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SpawnPlayerObject(long id, Transform3D initialTransform)
    {
        var lobby = GetNode<NetLobby>("/root/Lobby");

        Node3D pObj;
        if (playerObjects.ContainsKey(id))  // If an object for the player already exists, use that object instead
        {
            // TODO: This is not good code. The procedure used here is illogical.
            //      Please use the transform parameter: set the reset transform for the object, and THEN reset it.
            if (playerObjects[id].HasMethod(VehicleController.MethodName.QueueReset))
            {
                playerObjects[id].Call(VehicleController.MethodName.QueueReset);
            }
            else
            {
                ((Node3D)playerObjects[id]).GlobalTransform = initialTransform;
            }
            pObj = (Node3D)playerObjects[id];
        }
        else                                // Create the player object
        {
            // Spawn object
            string[] vehicleTypes = Util.GetVehicleTypes();
            int vehicleIdx = (int)lobby.players[id]["Vehicle"];
            var vehicleScene = ResourceLoader.Load<PackedScene>($"res://Prefabs/Vehicle_{vehicleTypes[vehicleIdx]}.tscn");
            pObj = vehicleScene.Instantiate<Node3D>();
            pObj.TreeEntered += () =>
            {
                // Place object at one of the spawn locations
                pObj.GlobalTransform = initialTransform;
            };
            AddChild(pObj);
            playerObjects.Add(id, pObj);

            // Name the object (to comply with RPC routing)
            pObj.Name = $"Player_Vehicle_{id}";

            // Setup network configuration for object
            // TODO/NOTE: Perhaps an "IReplicated"-interface is better - although, at this scale, testing for type is probably fine.
            var pNodes = pObj.GetAllNodes<Node>(true);
            foreach (var n in pNodes)
            {
                if (n is DamageSource ds)
                {
                    ds.PeerID = id;
                }
                if (n is Health h)
                {
                    h.PeerID = id;
                }
            }

            var inputObj = new ClientInput(id);         // Input synchronization node
            pObj.AddChild(inputObj);
            inputObj.Name = nameof(ClientInput);

            var netTransform = new NetTransform(id);    // Transform synchronization node
            pObj.AddChild(netTransform);
            netTransform.Name = nameof(NetTransform);
        }

        // Bind camera (if it's the client)
        // TODO: Perform null checks and request the mesh object from the vehicle rather than "finding" it via GetNode()
        var mp = GetTree().GetMultiplayer();
        if (mp.GetUniqueId() == id)
        {
            Node3D camFollowNode = pObj.GetNode<Node3D>("MeshObject");  // TODO: Please don't get the mesh node like this!

            if (camFollowNode.IsValid())
                CameraController.Instance.target = camFollowNode;

            // Show HUD
            HUD.ShowScreen(HUD.HUDMode.Default);
        }
    }

    public void RemovePlayerObject(long id)
    {
        // TODO: Wait for player to reconnect (delayed removal)

        if (playerObjects.ContainsKey(id))
        {
            playerObjects[id].TreeExited += () => { playerObjects.Remove(id); };    // Remove from list
            playerObjects[id].QueueFree();  // Remove from game world
        }
    }

    public void SetPlayerInputAll(bool enable)
    {
        foreach (var po in playerObjects)
        {
            Rpc(MethodName.SetPlayerInput, po.Key, enable);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SetPlayerInput(long id, bool enable)
    {
        if (this.TreeMP().GetUniqueId() == id && playerObjects.ContainsKey(id))
        {
            if (playerObjects[id].TryGetNode(out ClientInput ci))
            {
                ci.SetProcess(enable);
                ci.SetPhysicsProcess(enable);
            }
        }
    }

    private void OnServerDisconnected()
    {
        GetTree().ChangeSceneToFile("res://Scenes/menu.tscn");
    }

    private Transform3D UseRandomSpawn()
    {
        if (spawns == null || spawns.Length == 0)
        {
            return new Transform3D(Basis.Identity, Vector3.Up * 5f);
        }

        int spawnIdx;
        if (availableSpawns.Count > 0)
        {
            // Get a random (valid) spawn index
            spawnIdx = availableSpawns[GD.RandRange(0, availableSpawns.Count - 1)];
            availableSpawns.Remove(spawnIdx);
        }
        else
        {
            GD.PushWarning("Ran out of valid spawns! Using spawn 0 instead.");
            spawnIdx = 0;
        }

        // Use and return spawn transform
        return spawns[spawnIdx].GlobalTransform;
    }
}