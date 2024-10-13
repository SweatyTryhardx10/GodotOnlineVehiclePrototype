using Godot;
using System;

[GlobalClass]
public partial class EngineRealistic : VehicleSystem.Engine
{
    [Signal] public delegate void OnGearChangedEventHandler(int gear);

    public const float INERTIA = 0.15f;
    public const float DRIVETRAIN_INERTIA = 0.3f + INERTIA;

    [Export] private float maxVehicleSpeed = 20f;
    // [Export] private float accelerationStrength = 5f;
    /// <summary>Unit: RPM/s</summary>
    [Export] private float displacementSpeed = 1000f;
    [Export] private Curve torqueCurve;
    [Export] private float brakeStrength = 20f;
    [Export(PropertyHint.Range, "0,1")] private float engineBrakeStrength = 1f;

    [ExportGroup("Gear Settings")]
    [Export] private float[] gearRatios = new float[] { 2f, 1f };
    [Export] private float gearChangeTime = 0.5f;
    private const float GEAR_BORDER_MARGIN = 0.02f;
    [Export] private Vector2 rpmRange = new Vector2(1000f, 3000f);

    private int targetGear = 1;     // Used to produce a delayed gear switch mechanic (might affect gameplay)
    private int currentGear = 1;    // 1-indexed: first gear is one (zero is reverse)

    private float TimeSinceClutchRelease => (Time.GetTicksMsec() - clutchReleaseTime) / 1000f;
    private ulong clutchReleaseTime;

    /// <summary>The RPM normalized to the defined RPM range.</summary>
    public float NormRPM => (RPM - rpmRange.X) / (rpmRange.Y - rpmRange.X);

    /// <summary>Returns the differential ratio needed to achieve a desired max speed for a given wheel radius in the last gear.</summary>
    public float GetDifferentialForWheelRadius(float radius)
    {
        float speedFromMaxGearAndRPM = rpmRange.Y * Util.RPM_TO_RAD_PER_SEC / gearRatios[gearRatios.Length - 1] * radius;
        float variableDifferentialRatio = speedFromMaxGearAndRPM / maxVehicleSpeed;
        return variableDifferentialRatio;
    }

    public override void _Process(double delta)
    {
        DebugOverlay.ShowTextAtNode($"RPM: {RPM}", GetParent<Node3D>());
        DebugOverlay.ShowTextOnScreen($"Gear: {(currentGear == 0 ? "R" : currentGear)}", new Vector2(0.7f, 0.85f), "Gear-meter");
    }

    public override void _PhysicsProcess(double delta)
    {
        RunEngineSolver(delta);

        // Clamp RPM
        if (currentGear == 1 || currentGear == 0)
            RPM = Mathf.Clamp(RPM, 0, rpmRange.Y);
        else
            RPM = Mathf.Clamp(RPM, rpmRange.X, rpmRange.Y);

        bool isClutchReleased = TimeSinceClutchRelease < gearChangeTime;

        if (!isClutchReleased)
        {
            // Switch to target gear
            if (currentGear != targetGear)
            {
                currentGear = targetGear;
                EmitSignal(SignalName.OnGearChanged, currentGear);
            }

            // Automatic gear shifting
            // TODO: Please fix this overly specialized piece of code!
            float averageAngularVelocity = 0f;
            float averageWheelRadius = 0f;
            foreach (var j in joints)
            {
                averageAngularVelocity += j.AngularVelocity;
                averageWheelRadius += j.wheelRadius;
            }
            averageAngularVelocity /= joints.Length;
            averageWheelRadius /= joints.Length;

            if (currentGear > 0)
            {
                float drivetrainRatio = gearRatios[currentGear] * GetDifferentialForWheelRadius(averageAngularVelocity);
                float targetRPS = -(RPM * Util.RPM_TO_RAD_PER_SEC / drivetrainRatio);
                if (NormRPM > 0.8f && Throttle > 0f && averageAngularVelocity < targetRPS * 0.8f)
                {
                    QueueGearChange(currentGear + 1);
                }
                if (NormRPM < 0.2f && Throttle < 0f)
                {
                    QueueGearChange(currentGear - 1);
                }
            }
            else
            {
                if (Throttle > 0f)
                    QueueGearChange(1);
            }
        }
    }

    protected override void RunEngineSolver(double delta)
    {
        // TODO: Compute the torque acting on the end points of the drive train
        // 	* Engine torque on wheels
        //		* Wheel torque on wheel (in the wheel joint class).
        //	* Wheel torque on engine (In order for this to work, the engine, or the drivetrain as a whole, must have inertia).
        //		* Torque is divided by the total inertia of the drivetrain (e.g. wheel inertia + engine inertia + gear inertia).

        // Solver Steps:
        //	1. Compute torque acting on the engine (displacement + wheel counter-torque).
        //	2. Compute torque acting on each wheel (engine torque output + wheeel counter-torque).
        //		2.1 Counter-torque for the wheel is applied in its own class.
        //	3. Apply net torque to the engine RPM.
        //	4. Apply engine torque to the wheels.

        // Net engine torque
        float torqueEngineNet = 0f;
        torqueEngineNet += displacementSpeed * Throttle * Util.RPM_TO_RAD_PER_SEC;  // RPS/s

        torqueEngineNet *= Mathf.Sign(gearRatios[currentGear]);

        // Apply net engine torque to engine RPM
        RPM += torqueEngineNet * Util.RAD_PER_SEC_TO_RPM * (float)delta;

        // Wheel torque (compute and apply)
        foreach (var j in joints)
        {
            if (!j.PoweredByEngine)
                continue;
            
            float drivetrainRatio = gearRatios[currentGear] * GetDifferentialForWheelRadius(j.wheelRadius);
            float targetAngVel = -RPS / drivetrainRatio;
            float angVelDiff = targetAngVel - j.AngularVelocity;

            // The difference in angular velocity is used to modulate the torque amount while the torque determines resulting acceleration.
            float torqueToApply = Mathf.Clamp(angVelDiff, -1, 1f) * torqueCurve.Sample(NormRPM) * drivetrainRatio * (float)delta;
            j.ApplyTorque(torqueToApply);
            // NOTE: Not an accurate way of integrating the drivetrain's RPM (a.k.a angular velocity), but it works.
        }
    }

    private void QueueGearChange(int gear)
    {
        if (gear < 0 || gear > gearRatios.Length - 1)
            return;

        clutchReleaseTime = Time.GetTicksMsec();
        targetGear = gear;

        int gearDiff = targetGear - currentGear;
        if (gearDiff > 0)
            RPM = rpmRange.X;
        else
            RPM = rpmRange.Y;
    }

    public override void Reset()
    {
        RPM = 0f;
        currentGear = 1;
    }

    [Obsolete]
    private void SpeedBasedEngineCode()
    {
        // DEV-NOTE: Tying the target angular velocity to the current speed of the vehicle...
        //		...is not viable because wheel slip is configurable and thus require vastly...
        //      ...different angular velocities to achieve the same speed. Moreover, the car...
        //		...loses speed when turning which affects the engine. The engine should be...
        //		...influencing the wheels, not the other way around.
        //		As a result, use an RPM-based solution instead.

        // // This engine code does not function without a valid rigidbody reference
        // if (!rb.IsValid())
        // 	return;

        // // Vehicle state
        // float vehicleSpeed = -(rb.GlobalBasis.Inverse() * rb.LinearVelocity).Z;  // Positive is forward
        // float normSpeed = vehicleSpeed / maxVehicleSpeed;

        // // "Gear state"
        // Vector2 gearSpeedRange = new Vector2(
        // 	currentGear == 0 ? 0f : normGearSwitchBorders[currentGear] * maxVehicleSpeed,
        // 	normGearSwitchBorders[currentGear] * maxVehicleSpeed
        // );
        // float gearProgress = (Mathf.Abs(vehicleSpeed) - gearSpeedRange.X) / (gearSpeedRange.Y - gearSpeedRange.X);
        // gearProgress = Mathf.Clamp(gearProgress, 0f, 1f);

        // // Set joint speed based on wheel radius
        // if (throttle != 0f)     // Perhaps this should instead be done externally by controlling the clutch engagement?
        // {
        // 	foreach (var j in joints)
        // 	{
        // 		if (!j.IsValid())
        // 			continue;

        // 		float SPEED_TO_ANGVEL_FACTOR = 1f / j.wheelRadius; // m/s to rad/s

        // 		float angVelForCurrentSpeed = -vehicleSpeed * SPEED_TO_ANGVEL_FACTOR;
        // 		float newAngVel;
        // 		if (throttle > 0f)  // Accelerate
        // 			newAngVel = angVelForCurrentSpeed + Mathf.Sign(angVelForCurrentSpeed) * accelerationCurve.Sample(gearProgress) * throttle * accelerationStrength;
        // 		else                // Brake
        // 			newAngVel = angVelForCurrentSpeed + Mathf.Sign(angVelForCurrentSpeed) * throttle * brakeStrength;

        // 		// Enforce speed limit
        // 		float maxAngularVelocity = maxVehicleSpeed * SPEED_TO_ANGVEL_FACTOR;
        // 		newAngVel = Mathf.Clamp(newAngVel, -maxAngularVelocity, maxAngularVelocity);

        // 		j.AngularVelocity = newAngVel;
        // 	}
        // }
        // // NOTE: Depending on your needs, this either limits or enhances the possibility for creative vehicle designs

        // // Automatic gear switch
        // bool notLastGear = currentGear < normGearSwitchBorders.Length - 1;
        // bool notFirstGear = currentGear > 0;
        // if (notLastGear && normSpeed > normGearSwitchBorders[currentGear] + GEAR_BORDER_MARGIN)
        // {
        // 	QueueGearChange(currentGear + 1);
        // }
        // if (notFirstGear && normSpeed < normGearSwitchBorders[currentGear - 1] - GEAR_BORDER_MARGIN)
        // {
        // 	QueueGearChange(currentGear - 1);
        // }
    }
}
