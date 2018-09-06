using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.AutoLOD
{
    public static class MeshExtensions
    {
        public static WorkingMesh ToWorkingMesh(this Mesh mesh)
        {
            var wm = new WorkingMesh();
            mesh.ApplyToWorkingMesh(wm);
            return wm;
        }

        public static void ApplyToWorkingMesh(this Mesh mesh, WorkingMesh wm)
        {
#if UNITY_2017_3_OR_NEWER
            wm.indexFormat = mesh.indexFormat;
#endif
            wm.vertices = mesh.vertices;
            wm.normals = mesh.normals;
            wm.tangents = mesh.tangents;
            wm.uv = mesh.uv;
            wm.uv2 = mesh.uv2;
            wm.uv3 = mesh.uv3;
            wm.uv4 = mesh.uv4;
            wm.colors = mesh.colors;
            wm.boneWeights = mesh.boneWeights;
            wm.bindposes = mesh.bindposes;
            wm.subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                wm.SetTriangles(mesh.GetTriangles(i), i);
            }
            wm.name = mesh.name;
            wm.bounds = mesh.bounds;
        }
    }

    public class WorkingMesh
    {
#if UNITY_2017_3_OR_NEWER
        public IndexFormat indexFormat { get; set; }
#endif
        public Vector3[] vertices { get; set; }

        public int[] triangles
        {
            get
            {
                var tris = new List<int>();
                foreach (var submeshTris in submeshTriangles)
                {
                    if (submeshTris != null)
                        tris.AddRange(submeshTris);

                }
                return tris.ToArray();
            }
            set
            {
                Array.Resize(ref submeshTriangles, 1);
                submeshTriangles[0] = value;
            }
        }

        public Vector3[] normals { get; set; }
        public Vector4[] tangents { get; set; }
        public Vector2[] uv { get; set; }
        public Vector2[] uv2 { get; set; }
        public Vector2[] uv3 { get; set; }
        public Vector2[] uv4 { get; set; }
        public Color[] colors { get; set; }

        public Color32[] colors32
        {
            get { return colors != null ? colors.Select(c => (Color32)c).ToArray() : null; }
            set { colors = value != null ? value.Select(c => (Color)c).ToArray() : null; }
        }

        public BoneWeight[] boneWeights { get; set; }
        public Matrix4x4[] bindposes { get; set; }

        public int subMeshCount
        {
            get { return submeshTriangles.Length; }
            set { Array.Resize(ref submeshTriangles, value); }
        }

        public string name { get; set; }
        public Bounds bounds { get; set; }

        int[][] submeshTriangles = { new int[0] };

        public void SetTriangles(int[] triangles, int submesh)
        {
            if (submesh >= submeshTriangles.Length)
                Array.Resize(ref submeshTriangles, submesh + 1);
            submeshTriangles[submesh] = triangles;
        }

        public int[] GetTriangles(int submesh)
        {
            return submeshTriangles[submesh];
        }

        public void RecalculateBounds() { }

        public void RecalculateNormals() { }

        public void RecalculateTangents() { }

        public void ApplyToMesh(Mesh mesh)
        {
#if UNITY_2017_3_OR_NEWER
            mesh.indexFormat = indexFormat;
#endif
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uv;
            mesh.uv2 = uv2;
            mesh.uv3 = uv3;
            mesh.uv4 = uv4;
            mesh.colors = colors;
            mesh.boneWeights = boneWeights;
            mesh.bindposes = bindposes;
            mesh.subMeshCount = subMeshCount;
            for (int i = 0; i < subMeshCount; i++)
            {
                mesh.SetTriangles(GetTriangles(i), i);
            }
            mesh.name = name;
            mesh.bounds = bounds;
        }
    }
}
