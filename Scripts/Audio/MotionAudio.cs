using Godot;
using System;

public partial class MotionAudio : AudioStreamPlayer3D
{
	[Export] private RigidBody3D Body { get; set; }
	
	[ExportGroup("Velocity settings")]
	[Export] private float linearVelocityInfluence = 0f;
	[Export] private Vector2 linearVelocityRange = Vector2.Down;
	[Export] private float angularVelocityInfluence = 0f;
	[Export] private Vector2 angularVelocityRange = Vector2.Down;
	
	[ExportGroup("Effect settings")]
	[Export] private Vector2 pitchRange = Vector2.One;
	[Export] private Vector2 volumeRange = new Vector2(-10f, 0f);
	[Export] private bool factorInBaseVolume = true;

	private float baseVolume;

    public override void _Ready()
    {
        if (!Body.IsValid())
		{
			if (GetParent<Node3D>() is RigidBody3D pb)
				Body = pb;
		}
		
		baseVolume = VolumeDb;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Body.IsValid())
			return;
			
		// Change audio settings based on velocities
		float linVelNorm = 0f;
		float angVelNorm = 0f;
		
		if (linearVelocityInfluence > 0f)
		{
			float linSpeed = Body.LinearVelocity.Length() * linearVelocityInfluence;
			linVelNorm = (linSpeed - linearVelocityRange[0]) / Mathf.Abs(linearVelocityRange[1] - linearVelocityRange[0]);
			linVelNorm = Mathf.Clamp(linVelNorm, 0f, 1f);
		}
		
		if (angularVelocityInfluence > 0f)
		{
			float angSpeed = Body.AngularVelocity.Length() * angularVelocityInfluence;
			angVelNorm = (angSpeed - angularVelocityRange[0]) / Mathf.Abs(angularVelocityRange[1] - angularVelocityRange[0]);
			angVelNorm = Mathf.Clamp(angVelNorm, 0f, 1f);
		}
		
		float aggregateVelNorm = linVelNorm + angVelNorm;
		aggregateVelNorm = Mathf.Clamp(aggregateVelNorm, 0f, 1f);
		
		// Pitch
		PitchScale = Mathf.Lerp(pitchRange[0], pitchRange[1], aggregateVelNorm);
		
		// Volume
		Vector2 linearVolumeRange = new Vector2(Mathf.DbToLinear(volumeRange[0]), Mathf.DbToLinear(volumeRange[1]));	// Convert dB range to linear ([0, 1]) range
		float volumeLinear = Mathf.Lerp(linearVolumeRange[0], linearVolumeRange[1], aggregateVelNorm) * (factorInBaseVolume ? Mathf.DbToLinear(baseVolume) : 1f);
		VolumeDb = Mathf.LinearToDb(volumeLinear);
    }
}
