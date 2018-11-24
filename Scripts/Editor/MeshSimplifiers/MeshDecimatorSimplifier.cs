#if ENABLE_MESHDECIMATOR
// Pull from https://github.com/Whinarn/MeshDecimator and copy UnityExample/Assets/Plugins/MeshDecimator into your AutoLOD/Packages directory
using MeshDecimator;
using MeshDecimator.Algorithms;
using MeshDecimator.Math;
using UnityEngine;
using Unity.AutoLOD;
using DMesh = MeshDecimator.Mesh;
using DVector2 = MeshDecimator.Math.Vector2;
using DVector3 = MeshDecimator.Math.Vector3;
using DVector4 = MeshDecimator.Math.Vector4;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using WMesh = Unity.AutoLOD.WorkingMesh;
#endif

#if UNITY_2017_3_OR_NEWER
[assembly: Unity.AutoLOD.OptionalDependency("MeshDecimator.MeshDecimation", "ENABLE_MESHDECIMATOR")]
#endif

#if ENABLE_MESHDECIMATOR
namespace Unity.AutoLOD
{
    public class MeshDecimatorSimplifier : IMeshSimplifier
    {
        public void Simplify(WMesh inputMesh, WMesh outputMesh, float quality)
        {
            var enableLogging = false;
            int totalTriangleCount;
            var sourceMesh = ToMeshDecimatorMesh(inputMesh, out totalTriangleCount);
            int targetTriangleCount = Mathf.CeilToInt(totalTriangleCount * quality);

            var algorithm = MeshDecimation.CreateAlgorithm(Algorithm.Default);
            algorithm.KeepLinkedVertices = false;

            DecimationAlgorithm.StatusReportCallback statusCallback = (iteration, tris, currentTris, targetTris) =>
            {
                Debug.LogFormat("Iteration {0}: tris {1} current {2} target {3}", iteration, tris, currentTris, targetTris);
            };

            if (enableLogging)
                algorithm.StatusReport += statusCallback;

            var destMesh = MeshDecimation.DecimateMesh(algorithm, sourceMesh, targetTriangleCount);

            if (enableLogging)
                algorithm.StatusReport -= statusCallback;

            FromMeshDecimatorMesh(destMesh, false, ref outputMesh);
        }

        static Vector3d[] ToVector3d(Vector3[] inputVectors)
        {
            var vectors = new Vector3d[inputVectors.Length];
            for (int i = 0; i < inputVectors.Length; i++)
            {
                var v = inputVectors[i];
                vectors[i] = new Vector3d(v.x, v.y, v.z);
            }

            return vectors;
        }

        static DVector2[] ToVector2(Vector2[] inputVectors)
        {
            var vectors = new DVector2[inputVectors.Length];
            for (int i = 0; i < inputVectors.Length; i++)
            {
                var v = inputVectors[i];
                vectors[i] = new DVector2(v.x, v.y);
            }

            return vectors;
        }

        static DVector3[] ToVector3(Vector3[] inputVectors)
        {
            var vectors = new DVector3[inputVectors.Length];
            for (int i = 0; i < inputVectors.Length; i++)
            {
                var v = inputVectors[i];
                vectors[i] = new DVector3(v.x, v.y, v.z);
            }

            return vectors;
        }

        static DVector4[] ToVector4(Vector4[] inputVectors)
        {
            var vectors = new DVector4[inputVectors.Length];
            for (int i = 0; i < inputVectors.Length; i++)
            {
                var v = inputVectors[i];
                vectors[i] = new DVector4(v.x, v.y, v.z, v.w);
            }

            return vectors;
        }

        static DVector4[] ToVector4(Color[] inputVectors)
        {
            var vectors = new DVector4[inputVectors.Length];
            for (int i = 0; i < inputVectors.Length; i++)
            {
                var v = inputVectors[i];
                vectors[i] = new DVector4(v.r, v.g, v.b, v.a);
            }

            return vectors;
        }

        static Vector3[] FromVector3d(Vector3d[] inputVectors)
        {
            Vector3[] vectors = null;
            if (inputVectors != null)
            {
                vectors = new Vector3[inputVectors.Length];
                for (int i = 0; i < inputVectors.Length; i++)
                {
                    var v = inputVectors[i];
                    vectors[i] = new Vector3((float)v.x, (float)v.y, (float)v.z);
                }
            }

            return vectors;
        }

        static Vector2[] FromVector2(DVector2[] inputVectors)
        {
            Vector2[] vectors = null;
            if (inputVectors != null)
            {
                vectors = new Vector2[inputVectors.Length];
                for (int i = 0; i < inputVectors.Length; i++)
                {
                    var v = inputVectors[i];
                    vectors[i] = new Vector2(v.x, v.y);
                }
            }

            return vectors;
        }

        static Vector3[] FromVector3(DVector3[] inputVectors)
        {
            Vector3[] vectors = null;
            if (inputVectors != null)
            {
                vectors = new Vector3[inputVectors.Length];
                for (int i = 0; i < inputVectors.Length; i++)
                {
                    var v = inputVectors[i];
                    vectors[i] = new Vector3(v.x, v.y, v.z);
                }
            }

            return vectors;
        }

        static Vector4[] FromVector4(DVector4[] inputVectors)
        {
            Vector4[] vectors = null;
            if (inputVectors != null)
            {
                vectors = new Vector4[inputVectors.Length];
                for (int i = 0; i < inputVectors.Length; i++)
                {
                    var v = inputVectors[i];
                    vectors[i] = new Vector4(v.x, v.y, v.z, v.w);
                }
            }

            return vectors;
        }

        static Color[] FromColor(DVector4[] inputVectors)
        {
            Color[] vectors = null;
            if (inputVectors != null)
            {
                vectors = new Color[inputVectors.Length];
                for (int i = 0; i < inputVectors.Length; i++)
                {
                    var v = inputVectors[i];
                    vectors[i] = new Color(v.x, v.y, v.z, v.w);
                }
            }

            return vectors;
        }


        DMesh ToMeshDecimatorMesh(WMesh mesh, out int totalTriangleCount)
        {
            var vertices = ToVector3d(mesh.vertices);

            int subMeshCount = mesh.subMeshCount;
            var meshNormals = mesh.normals;
            var meshTangents = mesh.tangents;
            var meshUV1 = mesh.uv;
            var meshUV2 = mesh.uv2;
            var meshUV3 = mesh.uv3;
            var meshUV4 = mesh.uv4;
            var meshColors = mesh.colors;
            //var meshBoneWeights = mesh.boneWeights;
            //var meshBindposes = mesh.bindposes;

            totalTriangleCount = 0;
            var meshIndices = new int[subMeshCount][];
            for (int i = 0; i < subMeshCount; i++)
            {
                meshIndices[i] = mesh.GetTriangles(i);
                totalTriangleCount += meshIndices[i].Length / 3;
            }

            var dmesh = new DMesh(vertices, meshIndices);

            if (meshNormals != null && meshNormals.Length > 0)
                dmesh.Normals = ToVector3(meshNormals);

            if (meshTangents != null && meshTangents.Length > 0)
                dmesh.Tangents = ToVector4(meshTangents);

            if (meshUV1 != null && meshUV1.Length > 0)
                dmesh.UV1 = ToVector2(meshUV1);

            if (meshUV2 != null && meshUV2.Length > 0)
                dmesh.UV2 = ToVector2(meshUV2);

            if (meshUV3 != null && meshUV3.Length > 0)
                dmesh.UV3 = ToVector2(meshUV3);

            if (meshUV4 != null && meshUV4.Length > 0)
                dmesh.UV4 = ToVector2(meshUV4);

            if (meshColors != null && meshColors.Length > 0)
                dmesh.Colors = ToVector4(meshColors);

            //if (meshBoneWeights != null && meshBoneWeights.Length > 0)
            //    dmesh.BoneWeights = ToSimplifyBoneWeights(meshBoneWeights);

            return dmesh;
        }

        static void FromMeshDecimatorMesh(DMesh mesh, bool recalculateNormals, ref WMesh destMesh)
        {
            if (recalculateNormals)
            {
                // If we recalculate the normals, we also recalculate the tangents
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
            }

            int subMeshCount = mesh.SubMeshCount;
            var newNormals = FromVector3(mesh.Normals);
            var newTangents = FromVector4(mesh.Tangents);
            var newUV1 = FromVector2(mesh.UV1);
            var newUV2 = FromVector2(mesh.UV2);
            var newUV3 = FromVector2(mesh.UV3);
            var newUV4 = FromVector2(mesh.UV4);
            var newColors = FromColor(mesh.Colors);
            //var newBoneWeights = FromSimplifyBoneWeights(mesh.BoneWeights);

            //if (bindposes != null) newMesh.bindposes = bindposes;
            destMesh.subMeshCount = subMeshCount;
            destMesh.vertices = FromVector3d(mesh.Vertices);
            if (newNormals != null)
                destMesh.normals = newNormals;

            if (newTangents != null)
                destMesh.tangents = newTangents;

            if (newUV1 != null)
                destMesh.uv = newUV1;

            if (newUV2 != null)
                destMesh.uv2 = newUV2;

            if (newUV3 != null)
                destMesh.uv3 = newUV3;

            if (newUV4 != null)
                destMesh.uv4 = newUV4;

            if (newColors != null)
                destMesh.colors = newColors;

            //if (newBoneWeights != null)
            //    newMesh.boneWeights = newBoneWeights;

            for (int i = 0; i < subMeshCount; i++)
            {
                var subMeshIndices = mesh.GetIndices(i);
                destMesh.SetTriangles(subMeshIndices, i);
            }

            destMesh.RecalculateBounds();
        }
    }
}
#endif