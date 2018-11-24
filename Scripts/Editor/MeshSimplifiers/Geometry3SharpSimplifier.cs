#if ENABLE_GEOMETRY3SHARP
using System;
using System.Collections.Generic;
using System.Linq;
using g3;
using UnityEngine;
using Mesh = Unity.AutoLOD.WorkingMesh;
#endif

#if UNITY_2017_3_OR_NEWER
[assembly: Unity.AutoLOD.OptionalDependency("g3.DMesh3", "ENABLE_GEOMETRY3SHARP")]
[assembly: Unity.AutoLOD.OptionalDependency("g3.MeshGenerator", "G3_USING_UNITY")]
#endif

#if ENABLE_GEOMETRY3SHARP
namespace Unity.AutoLOD
{
    public class Geometry3SharpSimplifier : IMeshSimplifier
    {
        public void Simplify(Mesh inputMesh, Mesh outputMesh, float quality)
        {
            var inputVertices = inputMesh.vertices;
            var inputNormals = inputMesh.normals;
            var inputUV = inputMesh.uv;
            var inputColors = inputMesh.colors;

            var hasNormals = inputNormals.Length > 0;
            var hasUV = inputUV.Length > 0;
            var hasColors = inputColors.Length > 0;

            var dMesh3 = new DMesh3(hasNormals, hasColors, hasUV, true);
            for (var v = 0; v < inputVertices.Length; v++)
            {
                dMesh3.AppendVertex(inputVertices[v]);

                if (hasNormals)
                    dMesh3.SetVertexNormal(v, inputNormals[v]);

                if (hasUV)
                    dMesh3.SetVertexUV(v, inputUV[v]);

                if (hasColors)
                    dMesh3.SetVertexColor(v, inputColors[v]);
            }

            var triangleCount = 0;
            var triangleGroups = new int[inputMesh.subMeshCount];
            for (var submesh = 0; submesh < inputMesh.subMeshCount; submesh++)
            {
                var gID = dMesh3.AllocateTriangleGroup();
                triangleGroups[submesh] = gID;

                var triangles = inputMesh.GetTriangles(submesh);
                for (var t = 0; t < triangles.Length; t += 3)
                    dMesh3.AppendTriangle(triangles[t], triangles[t + 1], triangles[t + 2], gID);

                triangleCount += triangles.Length;
            }
            triangleCount /= 3;

            if (!dMesh3.CheckValidity(true))
                return;

            var reducer = new Reducer(dMesh3);
            reducer.ReduceToTriangleCount(Mathf.CeilToInt(triangleCount * quality));

            dMesh3 = reducer.Mesh;

            var vertices = new Vector3[dMesh3.VertexCount];
            var normals = new Vector3[dMesh3.HasVertexNormals ? vertices.Length : 0];
            var uv = new Vector2[dMesh3.HasVertexUVs ? vertices.Length : 0];
            var colors = new Color[dMesh3.HasVertexColors ? vertices.Length : 0];

            var i = 0;
            foreach (var vID in dMesh3.VertexIndices())
            {
                vertices[i] = dMesh3.GetVertexf(vID);

                if (normals.Length > 0)
                    normals[i] = dMesh3.GetVertexNormal(vID);

                if (uv.Length > 0)
                    uv[i] = dMesh3.GetVertexUV(vID);

                if (colors.Length > 0)
                    colors[i] = dMesh3.GetVertexColor(vID);

                i++;
            }

            outputMesh.vertices = vertices;
            outputMesh.normals = normals;
            outputMesh.colors = colors;

            var submeshTriangles = new List<int>();
            outputMesh.subMeshCount = triangleGroups.Length;
            for (var submesh = 0; submesh < triangleGroups.Length; submesh++)
            {
                var gID = triangleGroups[submesh];

                foreach (var tID in dMesh3.TriangleIndices())
                {
                    if (dMesh3.GetTriangleGroup(tID) == gID)
                    {
                        var tri = dMesh3.GetTriangle(tID);
                        submeshTriangles.Add(tri.a);
                        submeshTriangles.Add(tri.b);
                        submeshTriangles.Add(tri.c);
                    }
                }

                outputMesh.SetTriangles(submeshTriangles.ToArray(), submesh);
                submeshTriangles.Clear();
            }
        }

    }
}
#endif