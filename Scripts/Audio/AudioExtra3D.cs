using Godot;
using System;

/// <summary>
/// A wrapper for the native AudioStreamPlayer3D class in Godot.
/// </summary>
public partial class AudioExtra3D : AudioStreamPlayer3D
{	
	#region Callback responses
	private void OnGearChanged(int gear)
	{
		Play();
	}
	#endregion
}
