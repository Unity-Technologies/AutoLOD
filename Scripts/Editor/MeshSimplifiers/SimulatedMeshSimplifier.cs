using System;
using System.Collections.Generic;
using UnityEngine;
using Mesh = Unity.AutoLOD.WorkingMesh;

namespace Unity.AutoLOD
{
    public class SimulatedMeshSimplifier : IMeshSimplifier
    {
        public void Simplify(Mesh inputMesh, Mesh outputMesh, float quality)
        {
            Vector3[] vertices = inputMesh.vertices;
            Vector2[] uv = inputMesh.uv;
            Vector2[] uv2 = inputMesh.uv2;
            Color[] colors = inputMesh.colors;
            Vector3[] normals = inputMesh.normals;
            Vector4[] tangents = inputMesh.tangents;

            var usedVertices = new Dictionary<int, Vector3>();
            var submeshTriangles = new Dictionary<int, List<int>>();
            for (int i = 0; i < inputMesh.subMeshCount; i++)
            {
                var triangles = new List<int>(inputMesh.GetTriangles(i));
                var targetCount = Mathf.FloorToInt(triangles.Count * quality);
                targetCount = Mathf.Max(0, targetCount - targetCount % 3);

                var random = new System.Random();
                while (triangles.Count > targetCount)
                {
                    var randomTriangle = Mathf.CeilToInt((float)random.NextDouble() * (triangles.Count - 1));
                    randomTriangle -= randomTriangle % 3;

                    triangles.RemoveRange(randomTriangle, 3);
                }

                for (int t = 0; t < triangles.Count; t++)
                {
                    var index = triangles[t];
                    usedVertices[index] = vertices[index];
                }

                submeshTriangles[i] = triangles;
            }

            var trimmedVertices = new List<Vector3>();
            var trimmedUVs = new List<Vector2>();
            var trimmedUV2s = new List<Vector2>();
            var trimmedColors = new List<Color>();
            var trimmedNormals = new List<Vector3>();
            var trimmedTangents = new List<Vector4>();

            var vertexRemap = new Dictionary<int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex;
                if (usedVertices.TryGetValue(i, out vertex))
                {
                    vertexRemap[i] = trimmedVertices.Count;
                    trimmedVertices.Add(vertex);

                    if (uv.Length > 0)
                        trimmedUVs.Add(uv[i]);

                    if (uv2.Length > 0)
                        trimmedUV2s.Add(uv2[i]);

                    if (colors.Length > 0)
                        trimmedColors.Add(colors[i]);

                    if (normals.Length > 0)
                        trimmedNormals.Add(normals[i]);

                    if (tangents.Length > 0)
                        trimmedTangents.Add(tangents[i]);
                }
            }

            outputMesh.vertices = trimmedVertices.ToArray();

            for (int i = 0; i < inputMesh.subMeshCount; i++)
            {
                var triangles = submeshTriangles[i];

                for (int t = 0; t < triangles.Count; t++)
                {
                    triangles[t] = vertexRemap[triangles[t]];
                }

                outputMesh.SetTriangles(triangles.ToArray(), i);
            }

            outputMesh.uv = trimmedUVs.ToArray();
            outputMesh.uv2 = trimmedUV2s.ToArray();
            outputMesh.colors = trimmedColors.ToArray();
            outputMesh.normals = trimmedNormals.ToArray();
            outputMesh.tangents = trimmedTangents.ToArray();

            outputMesh.RecalculateBounds();
            outputMesh.RecalculateNormals();
            outputMesh.RecalculateTangents();
        }
    }
}
