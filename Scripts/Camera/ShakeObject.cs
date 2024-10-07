using Godot;
using System;
using System.Collections.Generic;

public partial class ShakeObject : Node3D
{
	public static List<ShakeObject> All = new();

	public ShakeObject() { }

	public ShakeObject(float duration, float frequency, float amplitude) : this()
	{
		this.duration = duration;
		this.noiseFrequency = frequency;
		this.noiseAmplitude = amplitude;
	}

	/// <summary>
	/// Creates and adds a shake object to the scene tree.
	/// </summary>
	/// <param name="node">The node whose tree the new shake object should be added.</param>
	/// <param name="duration">The lifetime of the new shake. Will be infinite if under or equal to 0.</param>
	/// <param name="frequency">The frequency of the noise used for the shake.</param>
	/// <param name="amplitude">The amplitude of the noise used for the shake.</param>
	public static void Create(Node node, float duration, float frequency, float amplitude)
	{
		ShakeObject sh = new ShakeObject(duration, frequency, amplitude);
		node.GetTree().CurrentScene.AddChild(sh);
	}

	/// <summary>
	/// Creates and adds a shake object to the scene tree.
	/// </summary>
	/// <param name="node">The node whose tree the new shake object should be added.</param>
	/// <param name="duration">The lifetime of the new shake. Will be infinite if under or equal to 0.</param>
	/// <param name="frequency">The frequency of the noise used for the shake.</param>
	/// <param name="amplitude">The amplitude of the noise used for the shake.</param>
	/// <param name="position">The position of the new shake object (in world space).</param>
	/// <param name="range">The falloff range for the noise.</param>
	public static void Create(Node node, float duration, float frequency, float amplitude, Vector3 position, float range)
	{
		ShakeObject sh = new ShakeObject(duration, frequency, amplitude);

		sh.isSpatial = true;
		sh.falloffRange = range;
		sh.TreeEntered += () => { sh.GlobalPosition = position; };

		node.GetTree().CurrentScene.AddChild(sh);
	}

	[Export] private float duration = 1f;
	[Export] private Vector3 translationScale = Vector3.One;
	[Export] private Vector3 rotationScale = Vector3.One;
	[ExportGroup("Noise Settings")]
	[Export] private float noiseFrequency = Mathf.Pi;
	[Export] private float noiseAmplitude = 1f;
	[ExportGroup("Spatial Settings")]
	[Export] private bool isSpatial;
	[Export] private float falloffRange = 5f;
	[Export] private bool shakeSelf;
	[Export(PropertyHint.Range, "0,10")] private float selfFactor = 1f;
	private Transform3D initTransform;

	private FastNoiseLite noise = new FastNoiseLite() { FractalLacunarity = 2f }; // Set a lower-than-default lacunarity for softer noise

	public ulong startTime;
	public float Lifetime { get => (Time.GetTicksMsec() - startTime) / 1000f; }

	public override void _EnterTree()
	{
		All.Add(this);
	}
	public override void _ExitTree()
	{
		All.Remove(this);
	}

	public override void _Ready()
	{
		initTransform = GlobalTransform;
		noise.Frequency = noiseFrequency;

		startTime = Time.GetTicksMsec();
	}

	public override void _Process(double delta)
	{
		if (duration > 0f)  // Is finite
		{
			// Free the shake object
			if (Lifetime >= duration)
			{
				QueueFree();
			}
		}

		if (shakeSelf)
		{
			GlobalPosition = initTransform.Origin + GetShakeTranslation(this) * selfFactor;
			GlobalBasis = Basis.FromEuler(initTransform.Basis.GetEuler() + GetShakeRotationEuler(this) * selfFactor);
		}
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="n">The affected 3D node (e.g. camera).</param>
	/// <returns>The position offset-vector created by the shake.</returns>
	public Vector3 GetShakeTranslation(Node3D n)
	{	
		// Evaluate the 3D noise (with scaling);
		float translationX = noise.GetNoise3D(Lifetime, 0f, 0f) * translationScale.X;
		float translationY = noise.GetNoise3D(0f, Lifetime, 0f) * translationScale.Y;
		float translationZ = noise.GetNoise3D(0f, 0f, Lifetime) * translationScale.Z;

		// Return the modulated translation factor
		return new Vector3(translationX, translationY, translationZ) * noiseAmplitude * LifetimeModulation() * FalloffModulation(n);
	}

	/// <returns>Euler rotation in radians.</returns>
	public Vector3 GetShakeRotationEuler(Node3D n)
	{
		// Evaluate the 3D noise (with scaling)
		// TODO: Improve the workflow for configuring rotational shake.
		float rotationX = noise.GetNoise3D(Lifetime, 100f, 0f) * (Mathf.Pi / 8f) * rotationScale.X;
		float rotationY = noise.GetNoise3D(100f, Lifetime, 0f) * (Mathf.Pi / 8f) * rotationScale.Y;
		float rotationZ = noise.GetNoise3D(100f, 0f, Lifetime) * (Mathf.Pi / 8f) * rotationScale.Z;
		// NOTE: A different sampling point (relative to translation noise) is used to avoid phase/seed overlap in the two noise types.

		// Return the modulated translation factor
		return new Vector3(rotationX, rotationY, rotationZ) * noiseAmplitude * LifetimeModulation() * FalloffModulation(n);
	}

	public Basis GetShakeRotation(Node3D n)
	{
		var euler = GetShakeRotationEuler(n);
		return Basis.FromEuler(euler);
	}

	private float LifetimeModulation()
	{
		float mod = 1f;
		if (duration > 0f)
		{
			float t = (duration - Lifetime) / duration;
			mod = t * t;
		}
		return mod;
	}

	private float FalloffModulation(Node3D n)
	{
		float normFalloff = 1f;
		if (isSpatial)
		{
			normFalloff = Mathf.Max(0f, 1f - (n.GlobalPosition - GlobalPosition).Length() / falloffRange);
			normFalloff *= normFalloff;
		}
		return normFalloff;
	}
}
