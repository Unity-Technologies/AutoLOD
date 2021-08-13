
using Unity.Collections;
#if ENABLE_SIMPLYGON
using Simplygon;
using Simplygon.Unity.EditorPlugin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Unity.AutoLOD;
using UnityEditor;
using UnityEngine;
using Unity.Formats.USD;
using USD.NET;
using USD.NET.Unity;
using UnityObject = UnityEngine.Object;
#endif

#if ENABLE_SIMPLYGON
namespace Unity.AutoLOD
{
    public struct SimplygonMeshSimplifier : IMeshSimplifier
    {
        static pxr.TfToken s_MaterialBindToken = new pxr.TfToken("materialBind");
        static pxr.TfToken s_SubMeshesToken = new pxr.TfToken("subMeshes");

        static object s_ExecutionLock = new object();

        public void Simplify(WorkingMesh inputMesh, WorkingMesh outputMesh, float quality)
        {
            var isMainThread = MonoBehaviourHelper.IsMainThread();

            // lock (s_ExecutionLock)
            {
                MonoBehaviourHelper.ExecuteOnMainThread(() =>
                {
                    using (ISimplygon simplygon = Loader.InitSimplygon(out var simplygonErrorCode, out var simplygonErrorMessage))
                    {
                        if (simplygonErrorCode == EErrorCodes.NoError)
                        {
                            string exportTempDirectory = SimplygonUtils.GetNewTempDirectory();

                            using (spScene sgScene = ExportSimplygonScene(simplygon, exportTempDirectory, inputMesh))
                            {
                                using (spReductionPipeline reductionPipeline = simplygon.CreateReductionPipeline())
                                using (spReductionSettings reductionSettings = reductionPipeline.GetReductionSettings())
                                {
                                    reductionSettings.SetReductionTargets(EStopCondition.All, true, false, false, false);
                                    reductionSettings.SetReductionTargetTriangleRatio(quality);

                                    reductionPipeline.RunScene(sgScene, EPipelineRunMode.RunInThisProcess);

                                    string baseFolder = "Assets/SimpleReductions";
                                    if (!AssetDatabase.IsValidFolder(baseFolder))
                                    {
                                        AssetDatabase.CreateFolder("Assets", "SimpleReductions");
                                    }

                                    string meshName = inputMesh.name;
                                    string assetFolderGuid = AssetDatabase.CreateFolder(baseFolder, meshName);
                                    string assetFolderPath = AssetDatabase.GUIDToAssetPath(assetFolderGuid);

                                    int startingLodIndex = 0;
                                    List<GameObject> importedGameObjects = new List<GameObject>();
                                    SimplygonImporter.Import(simplygon, reductionPipeline, ref startingLodIndex,
                                        assetFolderPath, meshName, importedGameObjects);

                                    Debug.Assert(importedGameObjects.Count == 1, "AutoLOD: There should only be one imported mesh.");
                                    if (importedGameObjects.Count == 1)
                                    {
                                        GameObject go = importedGameObjects[0];
                                        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
                                        mf.sharedMesh.ApplyToWorkingMesh(ref outputMesh);
                                    }

                                    foreach (var go in importedGameObjects)
                                    {
                                        GameObject.DestroyImmediate(go);
                                    }
                                    AssetDatabase.DeleteAsset(assetFolderPath);
                                }
                            }
                        }
                        else
                        {
                            Debug.Log($"Initializing failed! {simplygonErrorCode}: {simplygonErrorMessage}");
                        }
                    }
                });
            }

            // while (string.IsNullOrEmpty(job.AssetDirectory))
            // {
            //     if (!isMainThread)
            //         Thread.Sleep(100);
            // }

            // MonoBehaviourHelper.ExecuteOnMainThread(() =>
            // {
            //     var customDataType = assembly.GetType("Simplygon.Cloud.Yoda.IntegrationClient.CloudJob+CustomData");
            //     var pendingFolderNameProperty = customDataType.GetProperty("UnityPendingLODFolderName");
            //     var jobCustomDataProperty = cloudJobType.GetProperty("JobCustomData");
            //     var jobCustomData = jobCustomDataProperty.GetValue(job.CloudJob, null);
            //     var jobFolderName = pendingFolderNameProperty.GetValue(jobCustomData, null) as string;
            //
            //     var lodAssetDir = "Assets/LODs/" + job.AssetDirectory;
            //     var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(string.Format("{0}/{1}_LOD1.prefab", lodAssetDir, jobName));
            //     MeshExtensions.ApplyToWorkingMesh(mesh, ref outputMesh);
            //
            //     //job.CloudJob.StateHandler.RequestJobDeletion();
            //     AssetDatabaseEx.DeletePendingLODFolder(jobFolderName);
            //     AssetDatabase.DeleteAsset(lodAssetDir);
            //
            //     UnityObject.DestroyImmediate(renderer.gameObject);
            // });
        }

        static spScene ExportSimplygonScene(ISimplygon simplygon, string tempDirectory, WorkingMesh mesh)
        {
            if (string.IsNullOrEmpty(tempDirectory))
                return (spScene)null;

            string filePath = Path.Combine(tempDirectory, "export.usd");
            InitUsd.Initialize();
            Scene scene = Scene.Create(filePath);
            
            var context = new ExportContext();
            context.scene = scene;
            context.basisTransform = BasisTransformation.SlowAndSafe;
            // context.exportRoot = root.transform.parent;

            ExportMesh(context, mesh);

            scene.Save();
            scene.Close();

            using (spSceneImporter sceneImporter = simplygon.CreateSceneImporter())
            {
                sceneImporter.SetImportFilePath(filePath);
                sceneImporter.RunImport();
                // SimplygonExporter.ExportSelectionSetsInSelection(simplygon, sceneImporter.GetScene(), selectedGameObjects, rootName);
                return sceneImporter.GetScene();
            }
        }

        static void ExportMesh(ExportContext exportContext, WorkingMesh mesh)
        {
            // path = /build_bighouse_02/build_bighouse_01_dragonhead_01_LOD0
            var path = new pxr.SdfPath($"/{mesh.name}");

            var scene = exportContext.scene;
            bool slowAndSafeConversion = exportContext.basisTransform == BasisTransformation.SlowAndSafe;
            var sample = new MeshSample();

            if (mesh.bounds.center == Vector3.zero && mesh.bounds.extents == Vector3.zero)
            {
                mesh.RecalculateBounds();
            }

            sample.extent = mesh.bounds;

            if (slowAndSafeConversion)
            {
                // Unity uses a forward vector that matches DirectX, but USD matches OpenGL, so a change of
                // basis is required. There are shortcuts, but this is fully general.
                sample.ConvertTransform();
                sample.extent.center = UnityTypeConverter.ChangeBasis(sample.extent.center);
            }

            // TODO: Technically a mesh could be the root transform, which is not handled correctly here.
            // It should have the same logic for root prims as in ExportXform.
            // sample.transform = XformExporter.GetLocalTransformMatrix(
            //     null,
            //     scene.UpAxis == Scene.UpAxes.Z,
            //     new pxr.SdfPath(path).IsRootPrimPath(),
            //     exportContext.basisTransform);

            sample.normals = mesh.normals;
            sample.points = mesh.vertices;
            sample.tangents = mesh.tangents;

            sample.colors = mesh.colors;
            if (sample.colors != null && sample.colors.Length == 0)
            {
                sample.colors = null;
            }

            // Gah. There is no way to inspect a meshes UVs.
            sample.st = mesh.uv;

            // Set face vertex counts and indices.
            var tris = mesh.triangles;

            if (slowAndSafeConversion)
            {
                // Unity uses a forward vector that matches DirectX, but USD matches OpenGL, so a change
                // of basis is required. There are shortcuts, but this is fully general.

                for (int i = 0; i < sample.points.Length; i++)
                {
                    sample.points[i] = UnityTypeConverter.ChangeBasis(sample.points[i]);
                    if (sample.normals != null && sample.normals.Length == sample.points.Length)
                    {
                        sample.normals[i] = UnityTypeConverter.ChangeBasis(sample.normals[i]);
                    }

                    if (sample.tangents != null && sample.tangents.Length == sample.points.Length)
                    {
                        var w = sample.tangents[i].w;
                        var t = UnityTypeConverter.ChangeBasis(sample.tangents[i]);
                        sample.tangents[i] = new Vector4(t.x, t.y, t.z, w);
                    }
                }

                for (int i = 0; i < tris.Length; i += 3)
                {
                    var t = tris[i];
                    tris[i] = tris[i + 1];
                    tris[i + 1] = t;
                }

                sample.SetTriangles(tris);

                scene.Write(path, sample);

                // TODO: this is a bit of a half-measure, we need real support for primvar interpolation.
                // Set interpolation based on color count.
                if (sample.colors != null && sample.colors.Length == 1)
                {
                    pxr.UsdPrim usdPrim = scene.GetPrimAtPath(path);
                    var colorPrimvar =
                        new pxr.UsdGeomPrimvar(usdPrim.GetAttribute(pxr.UsdGeomTokens.primvarsDisplayColor));
                    colorPrimvar.SetInterpolation(pxr.UsdGeomTokens.constant);
                    var opacityPrimvar =
                        new pxr.UsdGeomPrimvar(usdPrim.GetAttribute(pxr.UsdGeomTokens.primvarsDisplayOpacity));
                    opacityPrimvar.SetInterpolation(pxr.UsdGeomTokens.constant);
                }

                // In USD subMeshes are represented as UsdGeomSubsets.
                // When there are multiple subMeshes, convert them into UsdGeomSubsets.
                if (mesh.subMeshCount > 1)
                {
                    // Build a table of face indices, used to convert the subMesh triangles to face indices.
                    var faceTable = new Dictionary<Vector3, int>();
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        if (!slowAndSafeConversion)
                        {
                            faceTable.Add(new Vector3(tris[i], tris[i + 1], tris[i + 2]), i / 3);
                        }
                        else
                        {
                            // Under slow and safe export, index 0 and 1 are swapped.
                            // This swap will not be present in the subMesh indices, so must be undone here.
                            faceTable.Add(new Vector3(tris[i + 1], tris[i], tris[i + 2]), i / 3);
                        }
                    }

                    var usdPrim = scene.GetPrimAtPath(path);
                    var usdGeomMesh = new pxr.UsdGeomMesh(usdPrim);

                    // Process each subMesh and create a UsdGeomSubset of faces this subMesh targets.
                    for (int si = 0; si < mesh.subMeshCount; si++)
                    {
                        int[] indices = mesh.GetTriangles(si);
                        int[] faceIndices = new int[indices.Length / 3];

                        for (int i = 0; i < indices.Length; i += 3)
                        {
                            faceIndices[i / 3] = faceTable[new Vector3(indices[i], indices[i + 1], indices[i + 2])];
                        }

                        var vtIndices = UnityTypeConverter.ToVtArray(faceIndices);
                        var subset = pxr.UsdGeomSubset.CreateUniqueGeomSubset(
                            usdGeomMesh, // The object of which this subset belongs.
                            s_SubMeshesToken, // An arbitrary name for the subset.
                            pxr.UsdGeomTokens.face, // Indicator that these represent face indices
                            vtIndices, // The actual face indices.
                            s_MaterialBindToken // familyName = "materialBind"
                        );
                    }
                }
            }
        }
    }
}
#endif