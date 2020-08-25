using System;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.AutoLOD
{
    public static class MeshExtensions
    {
        public static int GetTriangleCount(this Mesh mesh)
        {
            var triangleCount = 0;
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                triangleCount += (int)mesh.GetIndexCount(i);
            }

            return triangleCount;
        }

        public static WorkingMesh ToWorkingMesh(this Mesh mesh, Allocator allocator)
        {
            var bindposes = mesh.bindposes;
            var wm = new WorkingMesh(allocator, mesh.vertexCount, mesh.GetTriangleCount(), mesh.subMeshCount, bindposes.Length);
            mesh.ApplyToWorkingMesh(ref wm, bindposes);

            return wm;
        }

        // Taking bindposes optional parameter is ugly, but saves an additional array allocation if it was already
        // accessed to get the length
        public static void ApplyToWorkingMesh(this Mesh mesh, ref WorkingMesh wm, Matrix4x4[] bindposes = null)
        {
            wm.indexFormat = mesh.indexFormat;
            wm.vertices = mesh.vertices;
            wm.normals = mesh.normals;
            wm.tangents = mesh.tangents;
            wm.uv = mesh.uv;
            wm.uv2 = mesh.uv2;
            wm.uv3 = mesh.uv3;
            wm.uv4 = mesh.uv4;
            wm.colors = mesh.colors;
            wm.boneWeights = mesh.boneWeights;
            wm.bindposes = bindposes ?? mesh.bindposes;
            wm.subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                wm.SetTriangles(mesh.GetTriangles(i), i);
            }
            wm.name = mesh.name;
            wm.bounds = mesh.bounds;
        }
    }

    public struct WorkingMesh : IDisposable
    {
        enum Channel
        {
            Vertices,
            Normals,
            Tangents,
            UV,
            UV2,
            UV3,
            UV4,
            Colors,
            BoneWeights,
            Bindposes,
            Triangles,
            SubmeshOffset
        }

        const int k_MaxNameSize = 128;

        public Vector3[] vertices
        {
            get
            {
                return m_Vertices.Slice(0, vertexCount).ToArray();
            }
            set
            {
                vertexCount = value.Length;
                m_Vertices.Slice(0, vertexCount).CopyFrom(value);
            }
        }
        NativeArray<Vector3> m_Vertices;

        public int vertexCount
        {
            get { return m_Counts[(int)Channel.Vertices]; }
            private set { m_Counts[(int)Channel.Vertices] = value; }
        }

        public int[] triangles
        {
            get { return m_Triangles.Slice(0, trianglesCount).ToArray(); }
            set
            {
                subMeshCount = 1;
                trianglesCount = value.Length;
                SetTriangles(value, 0);
            }
        }
        NativeArray<int> m_Triangles;

        int trianglesCount
        {
            get { return m_Counts[(int)Channel.Triangles]; }
            set { m_Counts[(int)Channel.Triangles] = value; }
        }

        public Vector3[] normals
        {
            get { return m_Normals.Slice(0, normalsCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    normalsCount = 0;
                }
                else
                {
                    normalsCount = value.Length;
                    m_Normals.Slice(0, normalsCount).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector3> m_Normals;

        int normalsCount
        {
            get { return m_Counts[(int)Channel.Normals]; }
            set { m_Counts[(int)Channel.Normals] = value; }
        }

        public Vector4[] tangents
        {
            get { return m_Tangents.Slice(0, tangentsCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    tangentsCount = 0;
                }
                else
                {
                    tangentsCount = value.Length;
                    m_Tangents.Slice(0, tangentsCount).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector4> m_Tangents;

        int tangentsCount
        {
            get { return m_Counts[(int)Channel.Tangents]; }
            set { m_Counts[(int)Channel.Tangents] = value; }
        }

        public Vector2[] uv
        {
            get { return m_UV.Slice(0, uvCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    uvCount = 0;
                }
                else
                {
                    uvCount = value.Length;
                    m_UV.Slice(0, uvCount).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector2> m_UV;

        int uvCount
        {
            get { return m_Counts[(int)Channel.UV]; }
            set { m_Counts[(int)Channel.UV] = value; }
        }

        public Vector2[] uv2
        {
            get { return m_UV2.Slice(0, uv2Count).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    uv2Count = 0;
                }
                else
                {
                    uv2Count = value.Length;
                    m_UV2.Slice(0, uv2Count).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector2> m_UV2;

        int uv2Count
        {
            get { return m_Counts[(int)Channel.UV2]; }
            set { m_Counts[(int)Channel.UV2] = value; }
        }

        public Vector2[] uv3
        {
            get { return m_UV3.Slice(0, uv3Count).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    uv3Count = 0;
                }
                else
                {
                    uv3Count = value.Length;
                    m_UV3.Slice(0, uv3Count).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector2> m_UV3;

        int uv3Count
        {
            get { return m_Counts[(int)Channel.UV3]; }
            set { m_Counts[(int)Channel.UV3] = value; }
        }

        public Vector2[] uv4
        {
            get { return m_UV4.Slice(0, uv4Count).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    uv4Count = 0;
                }
                else
                {
                    uv4Count = value.Length;
                    m_UV4.Slice(0, uv4Count).CopyFrom(value);
                }
            }
        }
        NativeArray<Vector2> m_UV4;

        int uv4Count
        {
            get { return m_Counts[(int)Channel.UV4]; }
            set { m_Counts[(int)Channel.UV4] = value; }
        }

        public Color[] colors
        {
            get { return m_Colors.Slice(0, colorsCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    colorsCount = 0;
                }
                else
                {
                    colorsCount = value.Length;
                    m_Colors.Slice(0, colorsCount).CopyFrom(value);
                }
            }
        }
        NativeArray<Color> m_Colors;

        int colorsCount
        {
            get { return m_Counts[(int)Channel.Colors]; }
            set { m_Counts[(int)Channel.Colors] = value; }
        }

        public Color32[] colors32
        {
            get { return colors != null ? colors.Select(c => (Color32)c).ToArray() : null; }
            set { colors = value != null ? value.Select(c => (Color)c).ToArray() : null; }
        }

        public BoneWeight[] boneWeights
        {
            get { return m_BoneWeights.Slice(0, boneWeightsCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    boneWeightsCount = 0;
                }
                else
                {
                    boneWeightsCount = value.Length;
                    m_BoneWeights.Slice(0, boneWeightsCount).CopyFrom(value);
                }
            }
        }
        NativeArray<BoneWeight> m_BoneWeights;

        int boneWeightsCount
        {
            get { return m_Counts[(int)Channel.BoneWeights]; }
            set { m_Counts[(int)Channel.BoneWeights] = value; }
        }

        public Matrix4x4[] bindposes
        {
            get { return m_Bindposes.Slice(0, bindposesCount).ToArray(); }
            set
            {
                if (value == null || value.Length == 0)
                {
                    bindposesCount = 0;
                }
                else
                {
                    bindposesCount = value.Length;
                    m_Bindposes.Slice(0, bindposesCount).CopyFrom(value);
                }
            }
        }
        NativeArray<Matrix4x4> m_Bindposes;

        int bindposesCount
        {
            get { return m_Counts[(int)Channel.Bindposes]; }
            set { m_Counts[(int)Channel.Bindposes] = value; }
        }

        public int subMeshCount
        {
            get { return submeshOffsetCount; }
            set
            {
                if (submeshOffsetCount == value)
                    return;

                var previousCount = submeshOffsetCount;
                submeshOffsetCount = value;
                for (var i = previousCount; i < submeshOffsetCount; i++)
                {
                    // Initialize these offsets to be invalid, so we don't use stale values
                    m_SubmeshOffset[i] = -1;
                }
            }
        }
        NativeArray<int> m_SubmeshOffset;

        int submeshOffsetCount
        {
            get { return m_Counts[(int)Channel.SubmeshOffset]; }
            set { m_Counts[(int)Channel.SubmeshOffset] = value; }
        }

        public string name
        {
            get { return Encoding.UTF8.GetString(m_Name.ToArray()); }
            set
            {
                if (value == null)
                    value = string.Empty;

                var bytes = Encoding.UTF8.GetBytes(value);
                var length = Mathf.Min(bytes.Length, k_MaxNameSize);
                m_Name.Slice(0, length).CopyFrom(bytes);
            }
        }
        NativeArray<byte> m_Name;

        // This data does not cross the job threshold, so if it needs to be read back, then it will need to be
        // in a NativeArray or some other type of NativeContainer
        public IndexFormat indexFormat { get; set; }
        public Bounds bounds { get; set; }

        NativeArray<int> m_Counts;

        // These are stubbed out for API completeness, but obviously don't do anything
        public void RecalculateBounds() { }
        public void RecalculateNormals() { }
        public void RecalculateTangents() { }

        public void SetTriangles(int[] triangles, int submesh)
        {
            if (submesh >= subMeshCount)
                subMeshCount = submesh + 1;

            var preSliceLength = m_SubmeshOffset[submesh];
            if (preSliceLength < 0)
            {
                if (submesh > 0)
                {
                    m_SubmeshOffset[submesh] = trianglesCount;
                    preSliceLength = trianglesCount;
                }
                else
                {
                    m_SubmeshOffset[submesh] = 0;
                    preSliceLength = 0;
                }
            }
            var totalCount = preSliceLength; // count prior to submesh
            totalCount += triangles.Length; // new submesh triangle count

            var postSliceOffset = 0;
            var postSliceLength = 0;
            if (submesh < subMeshCount - 2) // count of all triangles after submesh
            {
                postSliceOffset = m_SubmeshOffset[submesh + 1];
                if (postSliceOffset >= 0)
                {
                    postSliceLength = trianglesCount - postSliceOffset;
                    totalCount += postSliceLength;
                }
            }

            trianglesCount = totalCount;

            // Shift other following triangles up/down
            if (postSliceOffset > 0)
            {
                var offset = preSliceLength + triangles.Length;
                m_SubmeshOffset[submesh + 1] = offset;
                var sourceSlice = new NativeSlice<int>(m_Triangles, postSliceOffset, postSliceLength);
                var destSlice = new NativeSlice<int>(m_Triangles, offset, postSliceLength);
                destSlice.CopyFrom(sourceSlice);
            }

            m_Triangles.Slice(preSliceLength, triangles.Length).CopyFrom(triangles);
        }

        public int[] GetTriangles(int submesh)
        {
            if (submesh < m_SubmeshOffset.Length)
            {
                var start = 0;
                var stop = 0;
                GetTriangleRange(submesh, out start, out stop);
                var length = stop - start;

                var slice = new NativeSlice<int>(m_Triangles, start, length);
                return slice.ToArray();
            }

            return new int[0];
        }

        void GetTriangleRange(int submesh, out int start, out int stop)
        {
            if (submesh < m_SubmeshOffset.Length)
            {
                start = m_SubmeshOffset[submesh];
                stop = trianglesCount;
                if (submesh < m_SubmeshOffset.Length - 1)
                    stop = m_SubmeshOffset[submesh + 1];

                return;
            }

            start = -1;
            stop = -1;
            return;
        }

        public WorkingMesh(Allocator allocator, int maxVertices, int maxTriangles, int maxSubmeshes, int maxBindposes) : this()
        {
            m_Counts = new NativeArray<int>(Enum.GetValues(typeof(Channel)).Length, allocator);
            m_Vertices = new NativeArray<Vector3>(maxVertices, allocator);
            m_Normals = new NativeArray<Vector3>(maxVertices, allocator);
            m_Tangents = new NativeArray<Vector4>(maxVertices, allocator);
            m_UV = new NativeArray<Vector2>(maxVertices, allocator);
            m_UV2 = new NativeArray<Vector2>(maxVertices, allocator);
            m_UV3 = new NativeArray<Vector2>(maxVertices, allocator);
            m_UV4 = new NativeArray<Vector2>(maxVertices, allocator);
            m_Colors = new NativeArray<Color>(maxVertices, allocator);
            m_BoneWeights = new NativeArray<BoneWeight>(maxVertices, allocator);
            m_Bindposes = new NativeArray<Matrix4x4>(maxBindposes, allocator);
            m_Name = new NativeArray<byte>(k_MaxNameSize, allocator);
            m_SubmeshOffset = new NativeArray<int>(maxSubmeshes, allocator);
            m_Triangles = new NativeArray<int>(maxTriangles, allocator);
        }

        public void Dispose()
        {
            if (m_Counts.IsCreated)
                m_Counts.Dispose();

            if (m_Vertices.IsCreated)
                m_Vertices.Dispose();

            if (m_Normals.IsCreated)
                m_Normals.Dispose();

            if (m_Tangents.IsCreated)
                m_Tangents.Dispose();

            if (m_UV.IsCreated)
                m_UV.Dispose();

            if (m_UV2.IsCreated)
                m_UV2.Dispose();

            if (m_UV3.IsCreated)
                m_UV3.Dispose();

            if (m_UV4.IsCreated)
                m_UV4.Dispose();

            if (m_Colors.IsCreated)
                m_Colors.Dispose();

            if (m_BoneWeights.IsCreated)
                m_BoneWeights.Dispose();

            if (m_Bindposes.IsCreated)
                m_Bindposes.Dispose();

            if (m_Name.IsCreated)
                m_Name.Dispose();

            if (m_SubmeshOffset.IsCreated)
                m_SubmeshOffset.Dispose();

            if (m_Triangles.IsCreated)
                m_Triangles.Dispose();
        }

        public void ApplyToMesh(Mesh mesh)
        {
            mesh.indexFormat = indexFormat;
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
