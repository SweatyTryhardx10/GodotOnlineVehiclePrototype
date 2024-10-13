using Godot;
using System;

[GlobalClass]
public partial class EngineSimple : VehicleSystem.Engine
{
    [Export] private float maxVehicleSpeed = 20f;
    [Export] private float torque = 20f;
    [Export] private float brakeTorque = 100f;

    private float VehicleSpeed => -(rb.GlobalBasis.Inverse() * rb.LinearVelocity).Z;    // Positive is forward
    /// <summary>A metric [-1, 1] that describes how close the vehicle's speed is to the engine's max speed.</summary>
    public float Utilization => VehicleSpeed / maxVehicleSpeed;

    protected override void RunEngineSolver(double delta)
    {
        if (!Mathf.IsZeroApprox(Throttle))
        {
            if (Mathf.Sign(Throttle) == Mathf.Sign(VehicleSpeed) || Mathf.Abs(VehicleSpeed) < 1f)
            {
                // Accelerate all powered joints towards a maximum
                foreach (var j in joints)
                {
                    if (!j.PoweredByEngine)
                        continue;

                    float maxRPS = maxVehicleSpeed / j.wheelRadius;

                    float newAngVel = j.AngularVelocity - Throttle * torque * (float)delta;
                    newAngVel = Mathf.Clamp(newAngVel, -maxRPS, maxRPS);

                    j.AngularVelocity = newAngVel;
                }
            }
            else
            {
                // Brake all joints towards zero
                foreach (var j in joints)
                {
                    float newAngVel = Mathf.MoveToward(j.AngularVelocity, 0f, Mathf.Abs(Throttle) * brakeTorque * (float)delta);
                    j.AngularVelocity = newAngVel;
                }
            }
        }
    }
}
