using Godot;
using System;

public partial class VehicleEngineSound : AudioStreamPlayer3D
{
	[Export] private VehicleSystem.Engine engine;

	[Export] private Vector2 pitchInterval = new Vector2(1f, 3f);

	[ExportGroup("Health-specific Settings")]
	[Export(PropertyHint.Range, "0,1")] private float normalizedHealthCutoff = 0f;

	public override void _PhysicsProcess(double delta)
	{
		if (!engine.IsValid())
			return;
		
		float targetPitch = 1f;
		if (engine is EngineRealistic er)
			targetPitch = Mathf.Lerp(pitchInterval.X, pitchInterval.Y, Mathf.Clamp(er.NormRPM, 0f, 1f));	// Should the normalized RPM be clamped here or before you get it?
		else if (engine is EngineSimple es)
			targetPitch = Mathf.Lerp(pitchInterval.X, pitchInterval.Y, Mathf.Clamp(Mathf.Abs(es.Utilization), 0f, 1f));

		float newPitch = Util.ExpDecay(PitchScale, targetPitch, 10, (float)delta);
		PitchScale = newPitch;
	}

	private void OnHealthChanged(Godot.Collections.Dictionary data)
	{
		float health = data[(int)Health.SignalValue.Health].As<float>();
		float maxHealth = data[(int)Health.SignalValue.Max].As<float>();
		float normHealth = health / maxHealth;

		GD.Print($"Engine sound => Checking norm health: {normHealth} ({health} / {maxHealth})");
	
		if (normHealth <= normalizedHealthCutoff)
		{
			if (Playing)
			{
				Stop();
				GD.Print("Audio stopped!");
			}
		}
		else
		{
			if (!Playing)
			{
				Play();
				GD.Print("Audio started!");
			}
		}
	}
}
