#if ENABLE_UNITYMESHSIMPLIFIER
using Unity.AutoLOD;
using UnityEngine;
using UnityMeshSimplifier;
using Mesh = Unity.AutoLOD.WorkingMesh;
#endif

#if UNITY_2017_3_OR_NEWER
[assembly: Unity.AutoLOD.OptionalDependency("UnityMeshSimplifier.MeshSimplifier", "ENABLE_UNITYMESHSIMPLIFIER")]
#endif

#if ENABLE_UNITYMESHSIMPLIFIER
namespace Unity.AutoLOD
{
    public class QuadricMeshSimplifier : IMeshSimplifier
    {
        public void Simplify(Mesh inputMesh, Mesh outputMesh, float quality)
        {
            var meshSimplifier = new MeshSimplifier();
            meshSimplifier.Vertices = inputMesh.vertices;
            meshSimplifier.Normals = inputMesh.normals;
            meshSimplifier.Tangents = inputMesh.tangents;
            meshSimplifier.UV1 = inputMesh.uv;
            meshSimplifier.UV2 = inputMesh.uv2;
            meshSimplifier.UV3 = inputMesh.uv3;
            meshSimplifier.UV4 = inputMesh.uv4;
            meshSimplifier.Colors = inputMesh.colors;

            var triangles = new int[inputMesh.subMeshCount][];
            for (var submesh = 0; submesh < inputMesh.subMeshCount; submesh++)
            {
                triangles[submesh] = inputMesh.GetTriangles(submesh);
            }
            meshSimplifier.AddSubMeshTriangles(triangles);

            meshSimplifier.SimplifyMesh(quality);

            outputMesh.vertices = meshSimplifier.Vertices;
            outputMesh.normals = meshSimplifier.Normals;
            outputMesh.tangents = meshSimplifier.Tangents;
            outputMesh.uv = meshSimplifier.UV1;
            outputMesh.uv2 = meshSimplifier.UV2;
            outputMesh.uv3 = meshSimplifier.UV3;
            outputMesh.uv4 = meshSimplifier.UV4;
            outputMesh.colors = meshSimplifier.Colors;
            outputMesh.subMeshCount = meshSimplifier.SubMeshCount;
            for (var submesh = 0; submesh < outputMesh.subMeshCount; submesh++)
            {
                outputMesh.SetTriangles(meshSimplifier.GetSubMeshTriangles(submesh), submesh);
            }
        }

    }
}
#endif