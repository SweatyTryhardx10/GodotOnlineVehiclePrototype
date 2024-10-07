using Godot;
using System;

public partial class AudioWheel : AudioStreamPlayer3D
{
	private enum WheelPoint
	{
		ContactPoint,
		SpringPoint,
		WheelCenter
	}
	
	[Export] private bool playOnLanding;
	[Export] private WheelPoint audioPosition;

	private WheelJoint wheel;

    public override void _Ready()
    {
        this.TryGetParent<WheelJoint>(out wheel);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (wheel.IsValid())
		{
            // TODO: Other audio effects that are modulated by the wheel state.
			
			// Position audio player
            switch (audioPosition)
            {
                case WheelPoint.ContactPoint:
					GlobalPosition = wheel.ContactPoint;
                    break;
                case WheelPoint.SpringPoint:
					GlobalPosition = wheel.GlobalPosition;
                    break;
                case WheelPoint.WheelCenter:
					// TODO: Implement a getter for the spring state in the WheelJoint class.
                    break;
            }

			// Play audio
            if (playOnLanding && wheel.JustOnFloor)
			{
				Play();
			}
        }
    }
}
