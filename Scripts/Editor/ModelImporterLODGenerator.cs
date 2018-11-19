//#define SINGLE_THREADED
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using Unity.AutoLOD;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.AutoLOD
{
    public class ModelImporterLODGenerator : AssetPostprocessor
    {
        public static bool enabled { set; get; }
        public static Type meshSimplifierType { set; get; }
        public static int maxLOD { set; get; }
        public static int initialLODMaxPolyCount { set; get; }

        const HideFlags k_DefaultHideFlags = HideFlags.None;

        static List<string> s_ModelAssetsProcessed = new List<string>();

        struct MeshLOD
        {
            public Mesh inputMesh;
            public Mesh outputMesh;
            public float quality;
            public Type meshSimplifierType;
        }

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
                        AssetDatabase.SaveAssets();

                        foreach (var ml in meshLODs)
                        {
                            GenerateMeshLOD(ml, preprocessMeshes);
                        }
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
            bool saveAssets = false;

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
                    saveAssets = true;
                }
            }

            if (saveAssets)
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

        static void GenerateMeshLOD(MeshLOD meshLOD, HashSet<int> preprocessMeshes)
        {
            // A NOP to make sure we have an instance before launching into threads that may need to execute on the main thread
            MonoBehaviourHelper.ExecuteOnMainThread(() => { });

            WorkingMesh inputMesh = null;
            var inputMeshID = meshLOD.inputMesh.GetInstanceID();
            if (!preprocessMeshes.Contains(inputMeshID))
                inputMesh = meshLOD.inputMesh.ToWorkingMesh();

            var meshSimplifier = (IMeshSimplifier)Activator.CreateInstance(meshLOD.meshSimplifierType);
#if !SINGLE_THREADED
            var worker = new BackgroundWorker();
            worker.DoWork += (sender, args) =>
            {
                // If this mesh is dependent on another mesh, then let it complete first
                if (inputMesh == null)
                {
                    while (preprocessMeshes.Contains(inputMeshID))
                        Thread.Sleep(100);

                    MonoBehaviourHelper.ExecuteOnMainThread(() => inputMesh = meshLOD.inputMesh.ToWorkingMesh());
                }
#endif

                var outputMesh = new WorkingMesh();
#if UNITY_2017_3_OR_NEWER
                outputMesh.indexFormat = inputMesh.indexFormat;
#endif
                meshSimplifier.Simplify(inputMesh, outputMesh, meshLOD.quality);
#if !SINGLE_THREADED
                args.Result = outputMesh;
            };
#endif

#if !SINGLE_THREADED
            worker.RunWorkerCompleted += (sender, args) =>
#endif
            {
                var outMesh = meshLOD.outputMesh;
                Debug.Log("Completed LOD " + outMesh.name);
#if !SINGLE_THREADED
                var resultMesh = (WorkingMesh)args.Result;
#else
                var resultMesh = outputMesh;
#endif
                resultMesh.name = outMesh.name;
                resultMesh.ApplyToMesh(outMesh);
                outMesh.RecalculateBounds();

                var outputMeshID = outMesh.GetInstanceID();
                if (preprocessMeshes.Remove(outputMeshID))
                    Debug.Log("Pre-process mesh complete: " + outputMeshID);
            };

#if !SINGLE_THREADED
            worker.RunWorkerAsync();
#endif
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
