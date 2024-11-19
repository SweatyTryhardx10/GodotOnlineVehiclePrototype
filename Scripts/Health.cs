using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Health : Node
{
	public long PeerID { get; set; }

	/// <summary>Used as keys (casted to int) for the data received from one or more of Health's signals.</summary>
	public enum SignalValue
	{
		Health,
		Max,
		Damage,
		Position,
		Normal
	}

	public struct DamageData
	{
		public long peerID;

		public float damage;
		public Vector3 position;
		public Vector3 normal;

		public DamageModifier[] modifiers;

		public DamageData(long peerID, float damage, Vector3 position, Vector3? normal = null, params DamageModifier[] modifiers)
		{
			this.peerID = peerID;

			this.damage = damage;
			this.position = position;
			this.normal = normal.HasValue ? normal.Value : Vector3.Zero;

			this.modifiers = modifiers;
		}

		/// <summary>
		/// Makes a Godot-dictionary containing all relevant data.<br />
		/// This method is strictly used to interface with Godot's native functionality (e.g. signals).
		/// </summary>
		public Godot.Collections.Dictionary MakeDictionary()
		{
			var dict = new Godot.Collections.Dictionary()
			{
				{ (int)SignalValue.Damage, damage },
				{ (int)SignalValue.Position, position }
			};

			if (normal != Vector3.Zero)
				dict.Add((int)SignalValue.Normal, normal);

			foreach (var mod in modifiers)
			{
				foreach(var entry in mod.MakeDictionary())
				{
					dict.Add(entry.Key, entry.Value);
				}
			}

			return dict;
		}
	}

	/// <summary>
	/// The abstract base for which all damage modifiers are derived.
	/// </summary>
	public abstract class DamageModifier
	{
		/// <summary>
		/// Run the behaviour for the modifier.
		/// </summary>
		/// <param name="n">The node using the modifier.</param>
		public abstract void UseModifier(Node n);

		/// <summary>
		/// Makes a dictionary containing relevant data for the modifier.
		/// </summary>
		public abstract Godot.Collections.Dictionary MakeDictionary();
	}

	// TODO: Improve the architecture for damage modifiers.
	public class KnockbackModifier : DamageModifier
	{
		private Vector3 knockbackForce;
		private Vector3? globalPosition;

		public KnockbackModifier(Vector3 force, Vector3? position = null)
		{
			knockbackForce = force;
			globalPosition = position;
		}
		public KnockbackModifier(Vector3 direction, float magnitude, Vector3? position = null)
		{
			knockbackForce = direction * magnitude;
			globalPosition = position;
		}

		public override void UseModifier(Node n)
		{
			if (n.GetParent() is RigidBody3D rb)
			{
				rb.ApplyForce(knockbackForce, globalPosition);
			}
		}

		public override Dictionary MakeDictionary()
		{
			var dict = new Godot.Collections.Dictionary()
			{
				{ "knockbackForce", knockbackForce },
				{ "knockbackPosition", globalPosition.HasValue ? globalPosition.Value : Vector3.Zero }
				// TODO: A nullable type cannot be added to the dictionary. A utility entry should be added indicating whether
				// 		the position-value should be used over the receiving body's global position.
			};

			return dict;
		}
	}

	[Signal]
	public delegate void OnDeathEventHandler();
	[Signal]
	public delegate void OnHealthChangedEventHandler(Godot.Collections.Dictionary data); // Should contain: health, maxHealth, damage, position, and normal.

	[Export] protected int startHealth { get; private set; } = 10;
	[Export] protected float minTimeBetweenDamage { get; private set; } = 1f;
	[Export] protected bool invulnerable = false;

	private ulong lastDamageStamp;
	protected float TimeSinceDamage => (Time.GetTicksMsec() - lastDamageStamp) / 1000f;

	private float value;
	public float Value
	{
		get => value;
		protected set
		{
			this.value = value;
		}
	}

	public override void _Ready()
	{
		Reset();
	}

	/// <summary>Resets all state-related variables</summary>
	public virtual void Reset()
	{
		Value = startHealth;
	}

	/// <summary>
	/// Alters the health value by the amount given.
	/// </summary>
	/// <param name="data">Data about the damage event (damage, position, and normal)</param>
	public virtual void Damage(DamageData data)
	{
		if (Mathf.IsZeroApprox(data.damage))
		{
			return;
		}

		// DEBUGGING
		float time = 3f;
		this.ShowSphere(time, data.position, 0.2f, c: Colors.OrangeRed);

		if (!invulnerable)
		{
			// Damage Callback
			if (data.damage != 0f && TimeSinceDamage >= minTimeBetweenDamage)
			{
				lastDamageStamp = Time.GetTicksMsec();
				var signalData = data.MakeDictionary();
				signalData.Add((int)SignalValue.Health, Value - data.damage);
				signalData.Add((int)SignalValue.Max, startHealth);

				Rpc(MethodName.SetHealth, signalData);
			}

			// Death Callback
			if (Value <= 0)
			{
				EmitSignal(SignalName.OnDeath);
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetHealth(Godot.Collections.Dictionary data)
	{
		Value = (float)data[(int)SignalValue.Health];
		EmitSignal(SignalName.OnHealthChanged, data);

		// Damage Modifiers
		if (data.TryGetValue("knockbackForce", out Variant force))
		{
			data.TryGetValue("knockbackPosition", out Variant position);
			
			// TODO: Please, please - PLEEAASE find a different way of adding modifiers.
			// 		This can't be the best way of doing it through the network!
			new KnockbackModifier(force.As<Vector3>(), position.As<Vector3>()).UseModifier(this);
		}

		// DEBUGGING
		GD.Print($"{this.TreeMP().GetUniqueId()} => {GetParent().Name} health: {Value}");
	}
}
