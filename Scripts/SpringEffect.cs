using Godot;
using System;

[Obsolete]
public partial class SpringEffect : GeometryInstance3D
{
	private WheelJoint wheel;

	[Export(PropertyHint.Range, "0,1")] private float slipActivationThreshold = 0.5f;
	[Export] private Vector3 contactOffset = Vector3.Up * 0.05f;

	public override void _Ready()
	{
		this.TryGetParent<WheelJoint>(out wheel, recursive: true);
	}

	public override void _Process(double delta)
	{
		if (wheel.IsValid() && Mathf.Abs(wheel.LatSlipRatio) > slipActivationThreshold && wheel.OnFloor && wheel.ContactVelocity.LengthSquared() > 8f)
		{
			// // NOTE: Supports both particle node types
			if (!this.Get("emitting").As<bool>())
				this.Set("emitting", true);

			// Position and orient the effect
			// GlobalPosition = wheel.ContactPoint + contactOffset;

			Vector3 lookDir = wheel.ContactVelocity;
			if (lookDir.LengthSquared() > 1f)
				Rotation = Vector3.Up * Vector3.Forward.SignedAngleTo(lookDir, Vector3.Up);
		}
		else
		{
			// NOTE: Supports both particle node types
			if (this.Get("emitting").As<bool>())
				this.Set("emitting", false); 
		}
	}
}
