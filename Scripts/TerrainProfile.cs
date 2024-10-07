using Godot;
using System;

[GlobalClass]
public partial class TerrainProfile : Node
{
    [Export(PropertyHint.Range, "0,1,0.05")] public float associatedValue { get; private set; } = 1f;
    // This value determines where this profile is present on the terrain gradient.
    // It defines the ceiling of its accepted range; the floor of this range is defined by another terrain profile whose value is lower.
    // (In the context of a terrain profile list).

    [ExportGroup("Tire Characteristics")]
    [Export] public Curve lateralSlipCurve { get; private set; }
    [Export] public Curve longitudinalSlipCurve { get; private set; }

    [ExportGroup("Audio and Effects")]
    [ExportSubgroup("Effect")]
    [Export] public Node3D[] particles { get; private set; } = new Node3D[0];
    [Export(PropertyHint.Range, "0,1")] private float slipActivationThreshold = 0.25f;
    [Export] private float velocityActivationThresholdSquared = 8f;
    [ExportSubgroup("")]
    [Export] public AudioStreamPlayer3D audioPlayerContinuous { get; private set; }
    [Export] public AudioStreamPlayer3D audioPlayerImpact { get; private set; }

    private WheelJoint wheel;

    public override void _Ready()
    {
        this.TryGetParent<WheelJoint>(out wheel);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (wheel.IsValid())
        {
            if (wheel.CurrentTerrainProfile == this)
            {
                // PARTICLES
                if (wheel.OnFloor)
                {
                    if (Mathf.Abs(wheel.LongSlipRatio) > slipActivationThreshold && wheel.ContactVelocity.LengthSquared() > velocityActivationThresholdSquared)
                    {
                        foreach (var p in particles)
                        {
                            if (!p.IsValid())
                                continue;

                            // NOTE: Supports both particle node types
                            if (!p.Get("emitting").As<bool>())
                                p.Set("emitting", true);

                            // Position and orient the effect
                            p.GlobalPosition = wheel.ContactPoint;

                            Vector3 lookDir = wheel.ContactVelocity + wheel.GlobalBasis.Z * wheel.LongSlipRatio;
                            if (lookDir.LengthSquared() > 1f)
                                p.Rotation = Vector3.Up * Vector3.Forward.SignedAngleTo(lookDir, Vector3.Up);
                        }
                    }
                    else
                    {
                        foreach (var p in particles)
                        {
                            // NOTE: Supports both particle node types
                            if (p.IsValid() && p.Get("emitting").As<bool>())
                                p.Set("emitting", false);
                        }
                    }
                }

                // AUDIO
                if (wheel.OnFloor)
                {
                    if (audioPlayerContinuous.IsValid())
                    {
                        audioPlayerContinuous.GlobalPosition = wheel.ContactPoint;

                        if (!audioPlayerContinuous.Playing)
                            audioPlayerContinuous.Play();
                    }
                }
                else
                {
                    if (audioPlayerContinuous.IsValid())
                    {
                        if (audioPlayerContinuous.Playing)
                            audioPlayerContinuous.Stop();
                    }
                }

                if (wheel.JustOnFloor)
                {
                    if (audioPlayerImpact.IsValid())
                    {
                        audioPlayerImpact.GlobalPosition = wheel.ContactPoint;
                        audioPlayerImpact.Play();
                    }
                }
            }
            else
            {
                if (audioPlayerContinuous.IsValid())
                {
                    if (audioPlayerContinuous.Playing)
                        audioPlayerContinuous.Stop();
                }

                foreach (var p in particles)
                {
                    // NOTE: Supports both particle node types
                    if (p.IsValid() && p.Get("emitting").As<bool>())
                        p.Set("emitting", false);
                }
            }
        }
    }
}