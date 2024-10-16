using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public static class Util
{
	public const string ICONPATH = "res://Textures/Icons/";

	/// <summary>
	/// A Node3D extension that simplifies the usage of raycasts.<br />
	/// NOTE: Automatically excludes this node from the raycast if no excludes are provided.
	/// </summary>
	/// <param name="n"></param>
	/// <param name="start"></param>
	/// <param name="end"></param>
	/// <param name="excludes">Specific objects to exclude from the raycast</param>
	/// <param name="collisionMask">The collision layers to test against.</param>
	/// <returns>The results dictionary.</returns>
	public static Godot.Collections.Dictionary Raycast(this Node3D n, Vector3 start, Vector3 end, Rid[] excludes = null, uint collisionMask = uint.MaxValue)
	{
		// Create a raycast query
		var spaceState = n.GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(start, end);

		// Exclude target RIDs from the raycast
		if (n is CollisionObject3D && excludes == null)
			query.Exclude = new Godot.Collections.Array<Rid>() { (n as CollisionObject3D).GetRid() };
		else if (excludes != null)
			query.Exclude = new Godot.Collections.Array<Rid>(excludes);

		// Setup collision mask
		query.CollisionMask = collisionMask;

		var result = spaceState.IntersectRay(query);

		return result;
	}
	public static Godot.Collections.Dictionary Raycast(this Node3D n, Vector3 start, Vector3 end, float margin, Rid[] excludes = null, uint collisionMask = uint.MaxValue)
	{
		// Perform raycast
		var result = n.Raycast(start, end, excludes, collisionMask);

		// Modify position in accordance with the margin.
		if (result.ContainsKey("position"))
		{
			Vector3 dir = (end - start).Normalized();
			result["position"] = Variant.From(((Vector3)result["position"]) - dir * margin);
		}

		// Return modified result
		return result;
	}

	/// <summary>
	/// A Node3D extension that simplifies the usage of shapecasts.<br />
	/// NOTE: Automatically excludes this node from the shapecast.
	/// </summary>
	/// <param name="n">A node fromt the target 3DWorld.</param>
	/// <param name="shape">The shape used for the shapecast.</param>
	/// <param name="start">The start point.</param>
	/// <param name="end">The end point.</param>
	/// <param name="excludes">An array of specific objects to exclude from the collision query.</param>
	/// <param name="collisionMask">The physics layers that the collision query tests against.</param>
	/// <returns></returns>
	public static Godot.Collections.Array<Godot.Collections.Dictionary> Shapecast(this Node3D n, Shape3D shape, Vector3 start, Rid[] excludes = null, uint collisionMask = uint.MaxValue)
	{
		return Shapecast(n, shape.GetRid(), start, excludes, collisionMask);
	}

	/// <summary>
	/// A Node3D extension that simplifies the usage of shapecasts.<br />
	/// NOTE: Automatically excludes this node from the shapecast.
	/// </summary>
	/// <param name="n">A node fromt the target 3DWorld.</param>
	/// <param name="shape">The Rid of the shape used for the shapecast.</param>
	/// <param name="start">The start point.</param>
	/// <param name="end">The end point.</param>
	/// <param name="excludes">An array of specific objects to exclude from the collision query.</param>
	/// <param name="collisionMask">The physics layers that the collision query tests against.</param>
	/// <returns></returns>
	public static Godot.Collections.Array<Godot.Collections.Dictionary> Shapecast(this Node3D n, Rid shapeRid, Vector3 start, Rid[] excludes = null, uint collisionMask = uint.MaxValue)
	{
		// Create a raycast query
		var spaceState = n.GetWorld3D().DirectSpaceState;
		var query = new PhysicsShapeQueryParameters3D()
		{
			ShapeRid = shapeRid,
			CollideWithBodies = true,
			Transform = new Transform3D(Basis.Identity, start),
			Exclude = new Godot.Collections.Array<Rid>(excludes ?? new Rid[0]),
			CollisionMask = collisionMask,
			// Motion = end - start,       // Not used for IntersectShape (why is this even here?)
		};

		// Exclude this object from the raycast
		if (n is CollisionObject3D co3d)
			query.Exclude.Add(co3d.GetRid());

		var result = spaceState.IntersectShape(query);

		return result;
	}

	public static Godot.Collections.Dictionary ShapecastRest(this Node3D n, Shape3D shape, Vector3 pos, Rid[] excludes = null, uint collisionMask = uint.MaxValue)
	{
		// Create a raycast query
		var spaceState = n.GetWorld3D().DirectSpaceState;
		var query = new PhysicsShapeQueryParameters3D()
		{
			Shape = shape,
			CollideWithBodies = true,
			Transform = new Transform3D(Basis.Identity, pos),
			Exclude = new Godot.Collections.Array<Rid>(excludes ?? new Rid[0]),
			CollisionMask = collisionMask,
		};

		// Exclude this object from the raycast
		if (n is CollisionObject3D)
			query.Exclude.Add((n as CollisionObject3D).GetRid());

		var result = spaceState.GetRestInfo(query);

		return result;
	}

	/// <summary>Returns this vector with the Z-component set to 0.</summary>
	public static Vector3 XY(this Vector3 v) => new Vector3(v.X, v.Y, 0f);
	/// <summary>Returns this vector with the X-component set to 0.</summary>
	public static Vector3 YZ(this Vector3 v) => new Vector3(0f, v.Y, v.Z);
	/// <summary>Returns this vector with the Y-component set to 0.</summary>
	public static Vector3 XZ(this Vector3 v) => new Vector3(v.X, 0f, v.Z);

	public static T FindNodeOrNull<T>(this Node n) where T : Node
	{
		for (int i = 0; i < n.GetChildCount(); i++)
		{
			// GD.Print($"Checking {n.GetChild(i).Name}...");
			if (n.GetChild(i) is T)
			{
				// GD.Print($"Found the {typeof(T)} node!");
				return (T)n.GetChild(i);
			}
		}

		return null;
	}

	/// <summary>
	/// Attempt to find a node of type, T, in this node's children.
	/// </summary>
	/// <typeparam name="T">Node type.</typeparam>
	/// <param name="node">The found node.</param>
	/// <param name="recursive">If enabled, performs this operation recursively to perform a depth-first search.<br />NOTE: Should probably be breadth-first.</param>
	/// <returns>Whether a node was found.</returns>
	public static bool TryGetNode<T>(this Node n, out T node, bool recursive = false) where T : Node
	{
		for (int i = 0; i < n.GetChildCount(); i++)
		{
			// GD.Print($"Checking {n.GetChild(i).Name}...");
			if (n.GetChild(i) is T)
			{
				// GD.Print($"Found the {typeof(T)} node!");
				node = (T)n.GetChild(i);
				return true;
			}
			else
			{
				if (recursive)
				{
					if (n.GetChild(i).TryGetNode(out T recurseResult, recursive))
					{
						node = recurseResult;
						return true;
					}
				}
			}
		}

		node = null;
		return false;
	}

	/// <summary>
	/// Finds all nodes of type T amongst this node's children.
	/// </summary>
	/// <typeparam name="T">The target node type.</typeparam>
	/// <param name="recursive">If enabled, performs this operation recursively to perform a depth-first search.<br />NOTE: Should probably be breadth-first.</param>
	/// <returns>All children of type T.</returns>
	public static T[] GetAllNodes<T>(this Node n, bool recursive = false) where T : Node
	{
		List<T> nodes = new();
		for (int i = 0; i < n.GetChildCount(); i++)
		{
			if (n.GetChild(i) is T nt)
			{
				nodes.Add(nt);
			}

			if (recursive)
			{
				T[] childResult = n.GetChild(i).GetAllNodes<T>(recursive);
				nodes.AddRange(childResult);
			}
		}

		return nodes.ToArray();
	}

	public static bool TryGetParent<T>(this Node n, out T node, bool recursive = false) where T : Node
	{
		Node p = n.GetParent();
		if (p.IsValid())
		{
			if (p is T)
			{
				node = p as T;
				return true;
			}
			else
			{
				if (recursive)
					return p.TryGetParent<T>(out node);
			}
		}

		node = null;
		return false;
	}

	// NOTE: Is this bad practice?
	/// <summary>
	/// An extension function for damaging health nodes.<br />
	/// </summary>
	public static void Dmg(this Node n, Health.DamageData data)
	{
		if (n.TryGetNode(out Health h))
		{
			h.Damage(data);
		}
	}

	public static bool IsValid(this GodotObject go)
	{
		return Godot.GodotObject.IsInstanceValid(go);
	}
	
	/// <summary>
	/// Determines the contact index for a specific node by comparing its id with that of all current contact objects.
	/// </summary>
	/// <param name="rid">The node's id.</param>
	/// <returns>The contact index for the node id.</returns>
	public static int GetContactIndexFromNode(this PhysicsDirectBodyState3D state, ulong id)
	{
		int contactCount = state.GetContactCount();
		if (contactCount > 0)
		{
			int idx = -1;
			for (int i = 0; i < contactCount; i++)
			{
				if (state.GetContactColliderId(i) == id)
					idx = i;
			}
			
			return idx;
		}
		else
		{
			return -1;
		}
	}

	public static void ShowLine(this Node node, float time, Vector3 p1, Vector3 p2, float thickness = 0.1f, Color color = default)
	{
		var debugGeo = node.GetNode("/root/DebugGeo");
		debugGeo.Call("draw_debug_line", time, p1, p2, thickness, color);
	}

	public static void ShowCylinder(this Node node, float time, Vector3 p1, Vector3 p2, float radius, int lon = 8, bool b_caps = true, Color color = default, bool b_triangles = false)
	{
		var debugGeo = node.GetNode("/root/DebugGeo");
		debugGeo.Call("draw_debug_cylinder", time, p1, p2, radius, lon, b_caps, color, b_triangles);
	}
	
	public static void ShowSphere(this Node node, float time, Vector3 p, float radius = 0.5f, int lon = 8, int lat = 8, Color c = default, bool b_triangles = false)
	{
		var debugGeo = node.GetNode("/root/DebugGeo");
		debugGeo.Call("draw_debug_sphere", time, p, lon, lat, radius, c, b_triangles);
	}

	// Credit: Freya Holmér
	public static float ExpDecay(float a, float b, float decay, float delta)
	{
		return b + (a - b) * Mathf.Exp(-decay * delta);
	}
	// Credit: Freya Holmér
	public static Vector2 ExpDecay(Vector2 a, Vector2 b, float decay, float delta)
	{
		return b + (a - b) * Mathf.Exp(-decay * delta);
	}
	// Credit: Freya Holmér
	public static Vector3 ExpDecay(Vector3 a, Vector3 b, float decay, float delta)
	{
		return b + (a - b) * Mathf.Exp(-decay * delta);
	}

	#region GAME SPECIFIC

	public static string[] GetVehicleTypes()
	{
		string[] targetFilePaths = DirAccess.GetFilesAt("res://Prefabs/Vehicles").Where(s => s.Contains("Vehicle")).ToArray();

		string[] types = new string[targetFilePaths.Length];
		for (int i = 0; i < targetFilePaths.Length; i++)
		{
			string formatted = targetFilePaths[i].Split('_').Last();
			formatted = formatted.Split('.').First();

			types[i] = formatted;
		}

		return types;
	}

	public const float RPM_TO_RAD_PER_SEC = 1f / (60f * Mathf.Tau);
	public const float RAD_PER_SEC_TO_RPM = 60f * Mathf.Tau;

	/// <summary>
	/// Gets the MultiplayerApi instance for the node's tree.<br />
	/// This is faster than writing: GetTree().GetMultiplayer() .
	/// </summary>
	public static MultiplayerApi TreeMP(this Node n)
	{
		return n.GetTree().GetMultiplayer();
	}

	#endregion

	#region UTILITY Structs

	public struct RaycastOperation
	{
		public readonly Vector3 start;
		public readonly Vector3 end;
		public readonly Rid[] excludes;
		public readonly uint collisionMask;

		public RaycastOperation(Vector3 start, Vector3 end, Rid[] excludes = null, uint collisionMask = uint.MaxValue)
		{
			this.start = start;
			this.end = end;
			this.excludes = excludes;
			this.collisionMask = collisionMask;
		}

		public Godot.Collections.Dictionary PerformOperation(Node3D node)
		{
			return node.Raycast(start, end, excludes, collisionMask);
		}
	}

	#endregion
}
