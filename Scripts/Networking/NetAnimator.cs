using Godot;
using System;

[GlobalClass]
public partial class NetAnimator : AnimationPlayer
{
	[Export] private string animationToPlay;
	[Export] private bool autoplay = false;

	private int clientID { get => this.TreeMP().GetUniqueId(); }
	private string autoAnimKey;

	public override void _EnterTree()
	{
		if (autoplay)
		{
			var game = GetNode<NetGame>("/root/Game");
			game.RoundStarted += OnRoundStarted;
		}
	}
	public override void _ExitTree()
	{
		if (autoplay)
		{
			var game = GetNode<NetGame>("/root/Game");
			game.RoundStarted -= OnRoundStarted;
		}
	}

	public override void _Ready()
	{
		if (Autoplay != string.Empty)
		{
			Stop();
		}
	}

	/// <summary>
	/// Plays the animation configured in the NetAnimator.<br />
	/// Dedicated to signal use.
	/// </summary>
	public void RemotePlay()
	{
		Rpc(MethodName.PlayAnimation);
	}

	private void OnRoundStarted(float rtt)
	{
		Rpc(MethodName.PlayAnimation);

		// TODO: Fast-forward the animation by the duration of RTT
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void PlayAnimation()
	{
		Play(animationToPlay);
	}
}
