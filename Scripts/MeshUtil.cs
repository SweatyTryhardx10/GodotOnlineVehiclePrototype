using Godot;
using System;
using System.Collections.Generic;

public static class MeshUtil
{
	#region UV Computation
	// A dictionary of mesh data that grows whenever a new instance ID in UVCoordsFromPoint is detected.
	private static Dictionary<ulong, Godot.Collections.Array> meshDatabase = new(); // TODO: Wipe this when scene is changed!

	// Code in this region is adapted from a Git Repo (for some reason, the author decided to transform the entire vertex array instead of the point-parameter).
	// Source: https://github.com/alfredbaudisch/GodotRuntimeTextureSplatMapPainting/blob/master/Scripts/VertexPositionMapper.gd
	public static Vector2? UVCoordsFromPoint(this GeometryInstance3D geom, Vector3 globalPoint)
	{
		// NOTE: This extension method is on the GeometryInstance3D class because all visual nodes in 3D derive from it.
		Mesh mesh;
		if (geom is MeshInstance3D mi)
		{
			mesh = mi.Mesh;
		}
		else if (geom is CsgShape3D csg)
		{
			if (csg.IsRootShape())
			{
				mesh = csg.GetMeshes()[1].As<Mesh>();  // Returns a dictionary => { Transform3D, Mesh }
			}
			else
			{
				// TODO: Travel up the CSG hierarchy (determine if this is needed).
				return null;
			}
		}
		else
		{
			return null;
		}

		// Compute the UV for the provided point.
		Vector2[] uvs;
		if (!meshDatabase.ContainsKey(geom.GetInstanceId()))
		{
			meshDatabase.Add(geom.GetInstanceId(), mesh.SurfaceGetArrays(0));
		}
		uvs = meshDatabase[geom.GetInstanceId()][(int)Mesh.ArrayType.TexUV].As<Vector2[]>();
		// TODO: Perhaps the mesh data should not be in global scope. Consider storing it locally where needed, and then passing it to this method.

		Vector3 localPoint = geom.ToLocal(globalPoint);

		var face = mesh.GetFace(localPoint);
		if (face == default)
			return null;

		// TODO: Determine that the vertex indeces from GetFaces() mirror those used in the array(s) from SurfaceGetArrays().
		Vector2 uv0 = uvs[face.idx * 3 + 0];
		Vector2 uv1 = uvs[face.idx * 3 + 1];
		Vector2 uv2 = uvs[face.idx * 3 + 2];

		// if (Engine.GetPhysicsFrames() % 30 == 0)
		// 	GD.Print($"bary: {face.bary}\n" + $"uv (0, 1, 2): {uv0} | {uv1} | {uv2}");

		Vector2 uvPoint = (uv0 * face.bary.X) + (uv1 * face.bary.Y) + (uv2 * face.bary.Z);
		return uvPoint;
	}

	public static Vector3 CartesianToBarycentric(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
	{
		var v0 = b - a;
		var v1 = c - a;
		var v2 = p - a;

		var d00 = v0.Dot(v0);
		var d01 = v0.Dot(v1);
		var d11 = v1.Dot(v1);
		var d20 = v2.Dot(v0);
		var d21 = v2.Dot(v1);
		var denom = d00 * d11 - d01 * d01;

		var v = (d11 * d20 - d01 * d21) / denom;
		var w = (d00 * d21 - d01 * d20) / denom;
		var u = 1f - v - w;

		return new Vector3(u, v, w);
	}

	/// <summary>Determines if a point, local to the mesh, is within a specific triangle.</summary>
	/// <returns>The barycentric coordinates, for that triangle, where the point is located.</returns>
	public static Vector3? IsPointInTriangle(Vector3 point, Vector3 v1, Vector3 v2, Vector3 v3)
	{
		var bc = CartesianToBarycentric(point, v1, v2, v3);

		if ((bc.X < 0 || bc.X > 1) || (bc.Y < 0 || bc.Y > 1) || (bc.Z < 0 || bc.Z > 1))
			return null;

		return bc;
	}

	// DEV-NOTE: I really dislike tuples but this is what the source code uses, and I am not in the mood to rewrite it as a struct.
	public static (int idx, Vector3[] vertices, Vector3 bary) GetFace(this Mesh mesh, Vector3 point)
	{
		Vector3[] faceVertices = mesh.GetFaces();   // Length: face count * 3
		for (int i = 0; i < faceVertices.Length / 3; i++)
		{
			// Vertices
			int faceIdx = i * 3;
			Vector3 v0 = faceVertices[faceIdx + 0];
			Vector3 v1 = faceVertices[faceIdx + 1];
			Vector3 v2 = faceVertices[faceIdx + 2];

			// // Reject a face whose normal deviates from the normal-parameter (e.g. from a raycast)
			// Vector3 v0Tov1 = v1 - v0;
			// Vector3 v0Tov2 = v2 - v0;
			// Vector3 faceNormal = v0Tov1.Cross(v0Tov2).Normalized();
			// if ((faceNormal - normal).IsZeroApprox())
			// 	continue;

			// // === Reject a face if the volume of the virtual tetrahedron, built from the face vertices and the point, is too large. ===
			// // NOTE: Adapted from Master's Thesis codebase.

			// // "Volume" Matrix
			// Vector3 column0 = v0 - point;
			// Vector3 column1 = v1 - point;
			// Vector3 column2 = v2 - point;

			// // Determinant computation
			// float det0 = (column1.Y * column2.Z - column2.Y * column1.Z);
			// float det1 = (column0.Y * column2.Z - column2.Y * column0.Z);
			// float det2 = (column0.Y * column1.Z - column1.Y * column0.Z);
			// float volume = column0.X * det0 - column1.X * det1 - column2.X * det2;

			// if (volume > 1f)  // In the most extreme case, the point is directly on the surface of the triangle and the volume would thus be 0.0
			// 	continue;
			// // ==========================================================================================================================

			// Check whether the point is inside the face (verified with barycentric coordinates)
			Vector3? bc = IsPointInTriangle(point, v0, v1, v2);

			if (bc != null)
				return (i, new Vector3[] { v0, v1, v2 }, bc.Value);
		}

		return default;
	}

	#endregion
}
