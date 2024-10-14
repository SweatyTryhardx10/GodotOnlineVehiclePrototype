using Godot;
using System;

/// <summary>
/// This engine class is only applicable for objects that use a rigidbody to move forward.
/// In other words, it cannot be used to power a windmill or otherwise.
/// </summary>
namespace VehicleSystem
{
	[GlobalClass]
	public partial class Engine : Node
	{
		/// <summary>Rounds per minute.</summary>
		public float RPM { get; protected set; }
		/// <summary>Radians per second.</summary>
		public float RPS => RPM * Util.RPM_TO_RAD_PER_SEC;

		/// <summary>A value in the range of [-1, 1] where a negative value is reversing/braking, and a positive is accelerating.</summary>
		public float Throttle { get; set; }

		protected RigidBody3D rb;
		protected WheelJoint[] joints;

		protected bool JointsGrounded
		{
			get
			{
				int groundedJoints = 0;
				foreach (var j in joints)
				{
					if (j.OnFloor)
						groundedJoints++;
				}
				
				if (groundedJoints >= joints.Length / 2)
					return true;
				else
					return false;
			}
		}

		public void BindBody(RigidBody3D rb)
		{
			this.rb = rb;
		}

		public void BindJoints(WheelJoint[] joints)
		{
			this.joints = joints;
		}

		public override void _PhysicsProcess(double delta)
		{
			RunEngineSolver(delta);
		}
		protected virtual void RunEngineSolver(double delta) { }

		public virtual void Reset() { }
	}
}