using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.AutoLOD
{
    public class ModelImporterLODGenerator : AssetPostprocessor
    {
        public static bool saveAssets { set; get; }
        public static bool enabled { set; get; }
        public static Type meshSimplifierType { set; get; }
        public static int maxLOD { set; get; }
        public static int initialLODMaxPolyCount { set; get; }

        const HideFlags k_DefaultHideFlags = HideFlags.None;

        static List<string> s_ModelAssetsProcessed = new List<string>();

        public static bool IsEditable(string assetPath)
        {
            var attributes = File.GetAttributes(assetPath);

            return AssetDatabase.IsOpenForEdit(assetPath, StatusQueryOptions.ForceUpdate)
                && (attributes & FileAttributes.ReadOnly) == 0;
        }

        void OnPostprocessModel(GameObject go)
        {
            if (!go.GetComponentInChildren<LODGroup>() && meshSimplifierType != null && IsEditable(assetPath))
            {
                if (go.GetComponentsInChildren<SkinnedMeshRenderer>().Any())
                {
                    Debug.LogWarning("Automatic LOD generation on skinned meshes is not currently supported");
                    return;
                }

                var originalMeshFilters = go.GetComponentsInChildren<MeshFilter>();
                uint polyCount = 0;
                foreach (var mf in originalMeshFilters)
                {
                    var m = mf.sharedMesh;
                    for (int i = 0; i < m.subMeshCount; i++)
                    {
                        var topology = m.GetTopology(i);
                        var indexCount = m.GetIndexCount(i);

                        switch (topology)
                        {
                            case MeshTopology.Quads:
                                indexCount /= 4;
                                break;

                            case MeshTopology.Triangles:
                                indexCount /= 3;
                                break;

                            case MeshTopology.Lines:
                            case MeshTopology.LineStrip:
                                indexCount /= 2;
                                break;
                        }

                        polyCount += indexCount;
                    }
                }

                var meshLODs = new List<MeshLOD>();
                var preprocessMeshes = new HashSet<int>();

                var lodData = GetLODData(assetPath);
                var overrideDefaults = lodData.overrideDefaults;
                var importSettings = lodData.importSettings;

                // It's possible to override defaults to either generate on import or to not generate and use specified
                // LODs in the override, but in the case where we are not overriding and globally we are not generating
                // on import, then there should be no further processing.
                if (!overrideDefaults && !enabled)
                    return;

                if (importSettings.generateOnImport)
                {
                    if (importSettings.maxLODGenerated == 0 && polyCount <= importSettings.initialLODMaxPolyCount)
                        return;

                    var simplifierType = Type.GetType(importSettings.meshSimplifier) ?? meshSimplifierType;

                    if (polyCount > importSettings.initialLODMaxPolyCount)
                    {
                        foreach (var mf in originalMeshFilters)
                        {
                            var inputMesh = mf.sharedMesh;

                            var outputMesh = new Mesh();
                            outputMesh.name = inputMesh.name;
                            outputMesh.bounds = inputMesh.bounds;
                            mf.sharedMesh = outputMesh;

                            var meshLOD = new MeshLOD();
                            meshLOD.inputMesh = inputMesh;
                            meshLOD.outputMesh = outputMesh;
                            meshLOD.quality = (float)importSettings.initialLODMaxPolyCount / (float)polyCount;
                            meshLOD.meshSimplifierType = simplifierType;
                            meshLODs.Add(meshLOD);

                            preprocessMeshes.Add(outputMesh.GetInstanceID());
                        }
                    }

                    // Clear out previous LOD data in case the number of LODs has been reduced
                    for (int i = 0; i <= LODData.MaxLOD; i++)
                    {
                        lodData[i] = null;
                    }

                    lodData[0] = originalMeshFilters.Select(mf => mf.GetComponent<Renderer>()).ToArray();

                    for (int i = 1; i <= importSettings.maxLODGenerated; i++)
                    {
                        var lodMeshes = new List<Renderer>();

                        foreach (var mf in originalMeshFilters)
                        {
                            var inputMesh = mf.sharedMesh;

                            var lodTransform = EditorUtility.CreateGameObjectWithHideFlags(mf.name,
                                k_DefaultHideFlags, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                            lodTransform.parent = mf.transform;
                            lodTransform.localPosition = Vector3.zero;
                            lodTransform.localRotation = Quaternion.identity;
                            lodTransform.localScale = new Vector3(1, 1, 1);

                            var lodMF = lodTransform.GetComponent<MeshFilter>();
                            var lodRenderer = lodTransform.GetComponent<MeshRenderer>();

                            AppendLODNameToRenderer(lodRenderer, i);

                            var outputMesh = new Mesh();
                            outputMesh.name = string.Format("{0} LOD{1}", inputMesh.name, i);
                            outputMesh.bounds = inputMesh.bounds;
                            lodMF.sharedMesh = outputMesh;

                            lodMeshes.Add(lodRenderer);

                            EditorUtility.CopySerialized(mf.GetComponent<MeshRenderer>(), lodRenderer);

                            var meshLOD = new MeshLOD();
                            meshLOD.inputMesh = inputMesh;
                            meshLOD.outputMesh = outputMesh;
                            meshLOD.quality = Mathf.Pow(0.5f, i);
                            meshLOD.meshSimplifierType = simplifierType;
                            meshLODs.Add(meshLOD);
                        }

                        lodData[i] = lodMeshes.ToArray();
                    }

                    // Change the name of the original renderers last, so the name change doesn't end up in the clones for other LODs
                    AppendLODNameToRenderers(go.transform, 0);

                    if (meshLODs.Count > 0)
                    {
                        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(lodData)))
                        {
                            AssetDatabase.CreateAsset(lodData, GetLODDataPath(assetPath));
                        }
                        else
                        {
                            var objects = AssetDatabase.LoadAllAssetsAtPath(GetLODDataPath(assetPath));
                            foreach (var o in objects)
                            {
                                var mesh = o as Mesh;
                                if (mesh)
                                    UnityObject.DestroyImmediate(mesh, true);
                            }
                            EditorUtility.SetDirty(lodData);
                        }
                        meshLODs.ForEach(ml => AssetDatabase.AddObjectToAsset(ml.outputMesh, lodData));
                        if (saveAssets)
                            AssetDatabase.SaveAssets();

                        // Process dependencies first
                        var jobDependencies = new NativeArray<JobHandle>(preprocessMeshes.Count, Allocator.Temp);
                        var i = 0;
                        meshLODs.RemoveAll(ml =>
                        {
                            if (preprocessMeshes.Contains(ml.outputMesh.GetInstanceID()))
                            {
                                jobDependencies[i++] = ml.Generate();
                                return true;
                            }

                            return false;
                        });

                        // Process remaining meshes
                        foreach (var ml in meshLODs)
                        {
                            ml.Generate(jobDependencies);
                        }

                        jobDependencies.Dispose();
                    }
                }
                else
                {
                    // Don't allow overriding LOD0
                    lodData[0] = originalMeshFilters.Select(mf =>
                    {
                        var r = mf.GetComponent<Renderer>();
                        AppendLODNameToRenderer(r, 0);
                        return r;
                    }).ToArray();

                    for (int i = 1; i <= LODData.MaxLOD; i++)
                    {
                        var renderers = lodData[i];
                        for (int j = 0; j < renderers.Length; j++)
                        {
                            var r = renderers[j];

                            var lodTransform = EditorUtility.CreateGameObjectWithHideFlags(r.name,
                                k_DefaultHideFlags, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                            lodTransform.parent = go.transform;
                            lodTransform.localPosition = Vector3.zero;

                            var lodMF = lodTransform.GetComponent<MeshFilter>();
                            var lodRenderer = lodTransform.GetComponent<MeshRenderer>();

                            EditorUtility.CopySerialized(r.GetComponent<MeshFilter>(), lodMF);
                            EditorUtility.CopySerialized(r, lodRenderer);

                            AppendLODNameToRenderer(lodRenderer, i);

                            renderers[j] = lodRenderer;
                        }
                    }
                }

                List<LOD> lods = new List<LOD>();
                var maxLODFound = -1;
                for (int i = 0; i <= LODData.MaxLOD; i++)
                {
                    var renderers = lodData[i];
                    if (renderers == null || renderers.Length == 0)
                        break;

                    maxLODFound++;
                }

                var importerRef = new SerializedObject(assetImporter);
                var importerLODLevels = importerRef.FindProperty("m_LODScreenPercentages");
                for (int i = 0; i <= maxLODFound; i++)
                {
                    var lod = new LOD();
                    lod.renderers = lodData[i];
                    var screenPercentage = i == maxLODFound ? 0.01f : Mathf.Pow(0.5f, i + 1);

                    // Use the model importer percentages if they exist
                    if (i < importerLODLevels.arraySize && maxLODFound == importerLODLevels.arraySize)
                    {
                        var element = importerLODLevels.GetArrayElementAtIndex(i);
                        screenPercentage = element.floatValue;
                    }

                    lod.screenRelativeTransitionHeight = screenPercentage;
                    lods.Add(lod);
                }

                if (importerLODLevels.arraySize != 0 && maxLODFound != importerLODLevels.arraySize)
                    Debug.LogWarning("The model has an existing lod group, but it's settings will not be used because " +
                        "the specified lod count in the AutoLOD settings is different.");

                var lodGroup = go.AddComponent<LODGroup>();
                lodGroup.SetLODs(lods.ToArray());
                lodGroup.RecalculateBounds();

                // Keep model importer in sync
                importerLODLevels.ClearArray();
                for (int i = 0; i < lods.Count; i++)
                {
                    var lod = lods[i];
                    importerLODLevels.InsertArrayElementAtIndex(i);
                    var element = importerLODLevels.GetArrayElementAtIndex(i);
                    element.floatValue = lod.screenRelativeTransitionHeight;
                }
                importerRef.ApplyModifiedPropertiesWithoutUndo();

                s_ModelAssetsProcessed.Add(assetPath);
            }
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool assetsImported = false;

            foreach (var asset in importedAssets)
            {
                if (s_ModelAssetsProcessed.Remove(asset))
                {
                    var go = (GameObject)AssetDatabase.LoadMainAssetAtPath(asset);
                    var lodData = AssetDatabase.LoadAssetAtPath<LODData>(GetLODDataPath(asset));

                    var lodGroup = go.GetComponentInChildren<LODGroup>();
                    var lods = lodGroup.GetLODs();
                    for (int i = 0; i < lods.Length; i++)
                    {
                        var lod = lods[i];
                        lodData[i] = lod.renderers;
                    }

                    EditorUtility.SetDirty(lodData);
                    assetsImported = true;
                }
            }

            if (assetsImported && saveAssets)
                AssetDatabase.SaveAssets();
        }

        internal static string GetLODDataPath(string assetPath)
        {
            var pathPrefix = Path.GetDirectoryName(assetPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(assetPath);
            return pathPrefix + "_lods.asset";
        }

        internal static LODData GetLODData(string assetPath)
        {
            var lodData = AssetDatabase.LoadAssetAtPath<LODData>(GetLODDataPath(assetPath));
            if (!lodData)
                lodData = ScriptableObject.CreateInstance<LODData>();

            var overrideDefaults = lodData.overrideDefaults;

            var importSettings = lodData.importSettings;
            if (importSettings == null)
            {
                importSettings = new LODImportSettings();
                lodData.importSettings = importSettings;
            }

            if (!overrideDefaults)
            {
                importSettings.generateOnImport = enabled;
                importSettings.meshSimplifier = meshSimplifierType.AssemblyQualifiedName;
                importSettings.maxLODGenerated = maxLOD;
                importSettings.initialLODMaxPolyCount = initialLODMaxPolyCount;
            }

            return lodData;
        }

        static void AppendLODNameToRenderers(Transform root, int lod)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                AppendLODNameToRenderer(r, lod);
            }
        }

        static void AppendLODNameToRenderer(Renderer r, int lod)
        {
            if (r.name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase) == -1)
                r.name = string.Format("{0}_LOD{1}", r.name, lod);
        }
    }
}
