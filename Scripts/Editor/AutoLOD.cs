using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Unity.AutoLOD.Utilities;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.AutoLOD
{
    [InitializeOnLoad]
    class AutoLOD
    {
        const HideFlags k_DefaultHideFlags = HideFlags.None;
        const string k_MaxExecutionTime = "AutoLOD.MaxExecutionTime";
        const int k_DefaultMaxExecutionTime = 8;
        const string k_DefaultMeshSimplifier = "AutoLOD.DefaultMeshSimplifier";
        const string k_DefaultMeshSimplifierDefault = "QuadricMeshSimplifier";
        const string k_DefaultBatcher = "AutoLOD.DefaultBatcher";
        const string k_MaxLOD = "AutoLOD.MaxLOD";
        const int k_DefaultMaxLOD = 2;
        const string k_GenerateOnImport = "AutoLOD.GenerateOnImport";
        const string k_InitialLODMaxPolyCount = "AutoLOD.InitialLODMaxPolyCount";
        const int k_DefaultInitialLODMaxPolyCount = 500000;
        const string k_SceneLODEnabled = "AutoLOD.SceneLODEnabled";
        const string k_ShowVolumeBounds = "AutoLOD.ShowVolumeBounds";

        static int maxExecutionTime
        {
            set
            {
                EditorPrefs.SetInt(k_MaxExecutionTime, value);
                UpdateDependencies();
            }
            get { return EditorPrefs.GetInt(k_MaxExecutionTime, k_DefaultMaxExecutionTime); }
        }

        static Type meshSimplifierType
        {
            set
            {
                if (typeof(IMeshSimplifier).IsAssignableFrom(value))
                    EditorPrefs.SetString(k_DefaultMeshSimplifier, value.AssemblyQualifiedName);
                else if (value == null)
                    EditorPrefs.DeleteKey(k_DefaultMeshSimplifier);

                UpdateDependencies();
            }
            get
            {
                var meshSimplifiers = ObjectUtils.GetImplementationsOfInterface(typeof(IMeshSimplifier)).ToArray();
                var type = Type.GetType(EditorPrefs.GetString(k_DefaultMeshSimplifier, k_DefaultMeshSimplifierDefault));
                if (type == null && meshSimplifiers.Length > 0)
                    type = Type.GetType(meshSimplifiers[0].AssemblyQualifiedName);
                return type;
            }
        }

        static Type batcherType
        {
            set
            {
                if (typeof(IBatcher).IsAssignableFrom(value))
                    EditorPrefs.SetString(k_DefaultBatcher, value.AssemblyQualifiedName);
                else if (value == null)
                    EditorPrefs.DeleteKey(k_DefaultBatcher);

                UpdateDependencies();
            }
            get
            {
                var batchers = ObjectUtils.GetImplementationsOfInterface(typeof(IBatcher)).ToArray();
                var type = Type.GetType(EditorPrefs.GetString(k_DefaultBatcher, null));
                if (type == null && batchers.Length > 0)
                    type = Type.GetType(batchers[0].AssemblyQualifiedName);
                return type;
            }
        }

        static int maxLOD
        {
            set
            {
                EditorPrefs.SetInt(k_MaxLOD, value);
                UpdateDependencies();
            }
            get { return EditorPrefs.GetInt(k_MaxLOD, k_DefaultMaxLOD); }
        }

        static bool generateOnImport
        {
            set
            {
                EditorPrefs.SetBool(k_GenerateOnImport, value);
                UpdateDependencies();
            }
            get { return EditorPrefs.GetBool(k_GenerateOnImport, true); }
        }

        static int initialLODMaxPolyCount
        {
            set
            {
                EditorPrefs.SetInt(k_InitialLODMaxPolyCount, value);
                UpdateDependencies();
            }
            get { return EditorPrefs.GetInt(k_InitialLODMaxPolyCount, k_DefaultInitialLODMaxPolyCount); }
        }

        static bool sceneLODEnabled
        {
            set
            {
                EditorPrefs.SetBool(k_SceneLODEnabled, value);
                UpdateDependencies();
            }
            get { return EditorPrefs.GetBool(k_SceneLODEnabled, true); }
        }

        static bool showVolumeBounds
        {
            set
            {
                EditorPrefs.SetBool(k_ShowVolumeBounds, value);
                UpdateDependencies();
            }
            get { return EditorPrefs.GetBool(k_ShowVolumeBounds, false); }
        }


        static SceneLOD m_SceneLOD;

        static void UpdateDependencies()
        {
#if UNITY_2017_3_OR_NEWER
            MonoBehaviourHelper.maxSharedExecutionTimeMS = maxExecutionTime == 0 ? Mathf.Infinity : maxExecutionTime;

            LODDataEditor.meshSimplifier = meshSimplifierType.AssemblyQualifiedName;
            LODDataEditor.batcher = batcherType.AssemblyQualifiedName;
            LODDataEditor.maxLODGenerated = maxLOD;
            LODDataEditor.initialLODMaxPolyCount = initialLODMaxPolyCount;

            LODVolume.meshSimplifierType = meshSimplifierType;
            LODVolume.batcherType = batcherType;
            LODVolume.drawBounds = sceneLODEnabled && showVolumeBounds;

            ModelImporterLODGenerator.meshSimplifierType = meshSimplifierType;
            ModelImporterLODGenerator.maxLOD = maxLOD;
            ModelImporterLODGenerator.enabled = generateOnImport;
            ModelImporterLODGenerator.initialLODMaxPolyCount = initialLODMaxPolyCount;

            if (sceneLODEnabled && !SceneLOD.activated)
            {
                if (!SceneLOD.instance)
                    Debug.Log("SceneLOD failed to start");
            }
            else if (!sceneLODEnabled && SceneLOD.activated)
            {
                Debug.Log("Destroying SceneLOD instance");
                UnityObject.DestroyImmediate(SceneLOD.instance);
            }
#else
            ModelImporterLODGenerator.enabled = false;
#endif
        }

        static AutoLOD()
        {
#if UNITY_2017_3_OR_NEWER
            UpdateDependencies();
#else
            Debug.LogWarning("AutoLOD requires Unity 2017.3 or a later version");
#endif
        }

        static bool HasLODChain(GameObject go)
        {
            var lodGroup = go.GetComponent<LODGroup>();
            if (lodGroup)
            {
                var lods = lodGroup.GetLODs();
                if (lods.Length > 0)
                {
                    for (var l = 1; l < lods.Length; l++)
                    {
                        var lod = lods[l];
                        if (lod.renderers.Length > 0)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        static bool IsDirectoryAsset(UnityObject unityObject)
        {
            if (unityObject is DefaultAsset)
            {
                var path = AssetDatabase.GetAssetPath(unityObject);
                if (File.GetAttributes(path) == FileAttributes.Directory)
                    return true;
            }

            return false;
        }

        static void SelectAllGameObjectsUnderneathFolder(DefaultAsset folderAsset, Func<GameObject, bool> predicate)
        {
            var path = AssetDatabase.GetAssetPath(folderAsset);
            if (File.GetAttributes(path) == FileAttributes.Directory)
            {
                var gameObjects = new List<UnityObject>();
                var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { path });
                foreach (var p in prefabs)
                {
                    var prefab = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(p));
                    if (prefab && (predicate != null || predicate((GameObject)prefab)))
                        gameObjects.Add(prefab);
                }
                Selection.objects = gameObjects.ToArray();
            }
        }

        [MenuItem("GameObject/AutoLOD/Generate LODs (Prefabs and Scene GameObjects)", priority = 11)]
        static void GenerateLODs(MenuCommand menuCommand)
        {
            MonoBehaviourHelper.StartCoroutine(GenerateLODsCoroutine(menuCommand));
        }

        static IEnumerator GenerateLODsCoroutine(MenuCommand menuCommand)
        {
            var activeObject = Selection.activeObject;
            DefaultAsset folderAsset = null;
            if (IsDirectoryAsset(activeObject))
            {
                folderAsset = (DefaultAsset)activeObject;
                SelectAllGameObjectsUnderneathFolder(folderAsset, prefab => !HasLODChain(prefab));
            }

            yield return null;

            var go = menuCommand.context as GameObject;
            if (go)
            {
                GenerateLODs(go);
            }
            else
            {
                IterateOverSelectedGameObjects(current =>
                {
                    RemoveChildrenLODGroups(current);
                    GenerateLODs(current);
                });
            }

            if (folderAsset)
                Selection.activeObject = folderAsset;
        }

        [MenuItem("GameObject/AutoLOD/Generate LODs (Prefabs and Scene GameObjects)", validate = true, priority = 11)]
        static bool CanGenerateLODs()
        {
            bool enabled = true;

            // Allow processing of whole directories
            var activeObject = Selection.activeObject;
            if (IsDirectoryAsset(activeObject))
                return true;

            var gameObjects = Selection.gameObjects;
            if (gameObjects.Length == 0)
                return false;

            foreach (var go in gameObjects)
            {
                enabled = !HasLODChain(go);

                if (!enabled)
                    break;
            }

            return enabled;
        }

        [MenuItem("Assets/AutoLOD/Generate LOD", false)]
        static void ForceGenerateLOD()
        {
            var selection = Selection.activeGameObject;
            if (selection)
            {
                var prefabType = PrefabUtility.GetPrefabType(selection);
                if (prefabType == PrefabType.ModelPrefab)
                {
                    var assetPath = AssetDatabase.GetAssetPath(selection);

                    if (!ModelImporterLODGenerator.enabled)
                    {
                        // If AutoLOD's generate on import is disabled for the whole project, then generate LODs for this model specifically
                        var lodData = ModelImporterLODGenerator.GetLODData(assetPath);
                        lodData.overrideDefaults = true;
                        lodData.importSettings.generateOnImport = true;

                        var lodPath = ModelImporterLODGenerator.GetLODDataPath(assetPath);
                        AssetDatabase.CreateAsset(lodData, lodPath);
                    }

                    AssetDatabase.ImportAsset(assetPath);
                }
                else if (prefabType == PrefabType.Prefab)
                {
                    GenerateLODs(new MenuCommand(null));
                }
            }
        }

        [MenuItem("Assets/AutoLOD/Generate LOD", true)]
        static bool CanForceGenerateLOD()
        {
            var selection = Selection.activeGameObject;
            var prefabType = selection ? PrefabUtility.GetPrefabType(selection) : PrefabType.None;
            return selection &&  prefabType == PrefabType.ModelPrefab || prefabType == PrefabType.Prefab;
        }


        [MenuItem("GameObject/AutoLOD/Remove LODs", validate = true, priority = 11)]
        static bool RemoveLODsValidate()
        {
            // Allow processing of whole directories
            var activeObject = Selection.activeObject;
            if (IsDirectoryAsset(activeObject))
                return true;

            var gameObjects = Selection.gameObjects;
            if (gameObjects.Length == 0)
                return false;

            foreach (var go in gameObjects)
            {
                if (go.GetComponent<LODGroup>())
                    return true;
            }

            return false;
        }

        [MenuItem("GameObject/AutoLOD/Remove LODs", priority = 11)]
        static void RemoveLODs(MenuCommand menuCommand)
        {
            var activeObject = Selection.activeObject;
            DefaultAsset folderAsset = null;
            if (IsDirectoryAsset(activeObject))
            {
                folderAsset = (DefaultAsset)activeObject;
                SelectAllGameObjectsUnderneathFolder(folderAsset, HasLODChain);
            }

            var go = menuCommand.context as GameObject;
            if (go)
                RemoveLODs(go);
            else
                IterateOverSelectedGameObjects(RemoveLODs);

            if (folderAsset)
                Selection.activeObject = folderAsset;
        }

        [MenuItem("GameObject/AutoLOD/Remove Children LODGroups", priority = 11)]
        static void RemoveChildrenLODGroups(MenuCommand menuCommand)
        {
            var folderAsset = Selection.activeObject as DefaultAsset;
            if (folderAsset)
                SelectAllGameObjectsUnderneathFolder(folderAsset, prefab => prefab.GetComponent<LODGroup>());

            var go = menuCommand.context as GameObject;
            if (go)
                RemoveChildrenLODGroups(go);
            else
                IterateOverSelectedGameObjects(RemoveChildrenLODGroups);

            if (folderAsset)
                Selection.activeObject = folderAsset;
        }

        static void IterateOverSelectedGameObjects(Action<GameObject> callback)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                var gameObjects = Selection.gameObjects;
                var count = gameObjects.Length;
                for (int i = 0; i < count; i++)
                {
                    var selection = gameObjects[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Prefabs", selection.name, i / (float)count))
                        break;

                    if (selection && PrefabUtility.GetPrefabType(selection) == PrefabType.Prefab)
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(selection);
                        callback(go);
                        PrefabUtility.ReplacePrefab(go, selection);
                        UnityObject.DestroyImmediate(go);
                    }
                    else
                    {
                        callback(selection);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
            }
        }

        static void RemoveChildrenLODGroups(GameObject go)
        {
            var mainLODGroup = go.GetComponent<LODGroup>();
            var lodGroups = go.GetComponentsInChildren<LODGroup>();
            foreach (var lodGroup in lodGroups)
            {
                if (mainLODGroup != lodGroup)
                    UnityObject.DestroyImmediate(lodGroup);
            }
        }

        static void GenerateLODs(GameObject go)
        {
            // A NOP to make sure we have an instance before launching into threads that may need to execute on the main thread
            MonoBehaviourHelper.ExecuteOnMainThread(() => {});

            var meshFilters = go.GetComponentsInChildren<MeshFilter>();

            if (meshFilters.Length > 0)
            {
                var lodGroup = go.GetComponent<LODGroup>();
                if (!lodGroup)
                    lodGroup = go.AddComponent<LODGroup>();

                var lods = new LOD[maxLOD + 1];
                var lod0 = lods[0];
                lod0.renderers = go.GetComponentsInChildren<MeshRenderer>();
                lod0.screenRelativeTransitionHeight = 0.5f;
                lods[0] = lod0;

                var meshes = new List<Mesh>();

                for (int l = 1; l <= maxLOD; l++)
                {
                    var lodRenderers = new List<MeshRenderer>();
                    foreach (var mf in meshFilters)
                    {
                        var sharedMesh = mf.sharedMesh;

                        if (!sharedMesh)
                        {
                            Debug.LogWarning("AutoLOD: Missing mesh " + mf.name, mf);
                            continue;
                        }

                        var lodTransform = EditorUtility.CreateGameObjectWithHideFlags(string.Format("{0} LOD{1}", sharedMesh.name, l),
                            k_DefaultHideFlags, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                        lodTransform.SetParent(mf.transform, false);

                        var lodMF = lodTransform.GetComponent<MeshFilter>();
                        var lodRenderer = lodTransform.GetComponent<MeshRenderer>();

                        lodRenderers.Add(lodRenderer);

                        EditorUtility.CopySerialized(mf, lodMF);
                        EditorUtility.CopySerialized(mf.GetComponent<MeshRenderer>(), lodRenderer);

                        var simplifiedMesh = new Mesh();
                        simplifiedMesh.name = sharedMesh.name + string.Format(" LOD{0}", l);
                        lodMF.sharedMesh = simplifiedMesh;
                        meshes.Add(simplifiedMesh);

                        var worker = new BackgroundWorker();

                        var index = l;
                        var inputMesh = sharedMesh.ToWorkingMesh();
                        var outputMesh = simplifiedMesh.ToWorkingMesh();

                        var meshSimplifier = (IMeshSimplifier)Activator.CreateInstance(meshSimplifierType);
                        worker.DoWork += (sender, args) =>
                        {
                            meshSimplifier.Simplify(inputMesh, outputMesh, Mathf.Pow(0.5f, index));
                            args.Result = outputMesh;
                        };

                        worker.RunWorkerCompleted += (sender, args) =>
                        {
                            var resultMesh = (WorkingMesh)args.Result;
                            resultMesh.ApplyToMesh(simplifiedMesh);
                            simplifiedMesh.RecalculateBounds();
                        };

                        worker.RunWorkerAsync();
                    }

                    var lod = lods[l];
                    lod.renderers = lodRenderers.ToArray();
                    lod.screenRelativeTransitionHeight = l == maxLOD ? 0.01f : Mathf.Pow(0.5f, l + 1);
                    lods[l] = lod;
                }

                lodGroup.ForceLOD(0);
                lodGroup.SetLODs(lods.ToArray());
                lodGroup.RecalculateBounds();
                lodGroup.ForceLOD(-1);

#if UNITY_2018_2_OR_NEWER
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
#else
                var prefab = PrefabUtility.GetPrefabParent(go);
#endif
                if (prefab)
                {
                    var lodsAssetPath = GetLODAssetPath(prefab);
                    if (File.Exists(lodsAssetPath))
                        meshes.ForEach(m => AssetDatabase.AddObjectToAsset(m, lodsAssetPath));
                    else
                        ObjectUtils.CreateAssetFromObjects(meshes.ToArray(), lodsAssetPath);
                }
            }
        }

        static string GetLODAssetPath(UnityObject prefab)
        {
            var assetPath = AssetDatabase.GetAssetPath(prefab);
            var pathPrefix = Path.GetDirectoryName(assetPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(assetPath);
            var lodsAssetPath = pathPrefix + "_lods.asset";
            return lodsAssetPath;
        }

        static void RemoveLODs(GameObject go)
        {
            var lodGroup = go.GetComponent<LODGroup>();
            if (lodGroup)
            {
                var lods = lodGroup.GetLODs();
                for (var i = 1; i < lods.Length; i++)
                {
                    var lod = lods[i];
                    var renderers = lod.renderers;
                    foreach (var r in renderers)
                    {
                        if (r)
                            UnityObject.DestroyImmediate(r.gameObject);
                    }
                }

                UnityObject.DestroyImmediate(lodGroup);
            }

            var meshFilters = go.GetComponentsInChildren<MeshFilter>();
            foreach (var mf in meshFilters)
            {
                if (!mf.sharedMesh)
                    UnityObject.DestroyImmediate(mf.gameObject);
            }

#if UNITY_2018_2_OR_NEWER
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
#else
            var prefab = PrefabUtility.GetPrefabParent(go);
#endif
            if (prefab)
            {
                var lodAssetPath = GetLODAssetPath(prefab);
                AssetDatabase.DeleteAsset(lodAssetPath);
            }
        }

        [PreferenceItem("AutoLOD")]
        static void PreferencesGUI()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space();

#if UNITY_2017_3_OR_NEWER
            // Max execution time
            {
                var label = "Max Execution Time (ms)";
                if (maxExecutionTime == 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (!EditorGUILayout.Toggle(label, true))
                        maxExecutionTime = 1;
                    GUILayout.Label("Infinity");
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    var maxTime = EditorGUILayout.IntSlider(label, maxExecutionTime, 0, 15);
                    if (EditorGUI.EndChangeCheck())
                        maxExecutionTime = maxTime;
                    
                }
            }

            // Mesh simplifier
            {
                var type = meshSimplifierType;
                if (type != null)
                {
                    var meshSimplifiers = ObjectUtils.GetImplementationsOfInterface(typeof(IMeshSimplifier)).ToList();
                    var displayedOptions = meshSimplifiers.Select(t => t.Name).ToArray();
                    EditorGUI.BeginChangeCheck();
                    var selected = EditorGUILayout.Popup("Default Mesh Simplifier", Array.IndexOf(displayedOptions, type.Name), displayedOptions);
                    if (EditorGUI.EndChangeCheck())
                        meshSimplifierType = meshSimplifiers[selected];
                }
                else
                {
                    EditorGUILayout.HelpBox("No IMeshSimplifiers found!", MessageType.Warning);
                }
            }

            // Batcher
            {
                var type = batcherType;
                if (type != null)
                {
                    var batchers = ObjectUtils.GetImplementationsOfInterface(typeof(IBatcher)).ToList();
                    var displayedOptions = batchers.Select(t => t.Name).ToArray();
                    EditorGUI.BeginChangeCheck();
                    var selected = EditorGUILayout.Popup("Default Batcher", Array.IndexOf(displayedOptions, type.Name), displayedOptions);
                    if (EditorGUI.EndChangeCheck())
                        batcherType = batchers[selected];
                }
                else
                {
                    EditorGUILayout.HelpBox("No IBatchers found!", MessageType.Warning);
                }
            }

            // Max LOD
            {
                var maxLODValues = Enumerable.Range(0, LODData.MaxLOD + 1).ToArray();
                EditorGUI.BeginChangeCheck();
                int maxLODGenerated = EditorGUILayout.IntPopup("Maximum LOD Generated", maxLOD, maxLODValues.Select(v => v.ToString()).ToArray(), maxLODValues);
                if (EditorGUI.EndChangeCheck())
                    maxLOD = maxLODGenerated;
            }

            // Control LOD0 maximum poly count
            {
                EditorGUI.BeginChangeCheck();
                var maxPolyCount = EditorGUILayout.IntField("Initial LOD Max Poly Count", initialLODMaxPolyCount);
                if (EditorGUI.EndChangeCheck())
                    initialLODMaxPolyCount = maxPolyCount;
            }

            // Generate LODs on import
            {
                EditorGUI.BeginChangeCheck();
                var generateLODsOnImport = EditorGUILayout.Toggle("Generate on Import", generateOnImport);
                if (EditorGUI.EndChangeCheck())
                    generateOnImport = generateLODsOnImport;
            }

            // Use SceneLOD?
            {
                EditorGUI.BeginChangeCheck();
                var enabled = EditorGUILayout.Toggle("Scene LOD", sceneLODEnabled);
                if (EditorGUI.EndChangeCheck())
                    sceneLODEnabled = enabled;

                if (sceneLODEnabled)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginChangeCheck();
                    var showBounds = EditorGUILayout.Toggle("Show Volume Bounds", showVolumeBounds);
                    if (EditorGUI.EndChangeCheck())
                        showVolumeBounds = showBounds;

                    var sceneLOD = SceneLOD.instance;
                    EditorGUILayout.HelpBox(string.Format("Coroutine Queue: {0}\nCurrent Execution Time: {1:0.00} s", sceneLOD.coroutineQueueRemaining, sceneLOD.coroutineCurrentExecutionTime * 0.001f), MessageType.None);

                    // Force more frequent updating
                    var mouseOverWindow = EditorWindow.mouseOverWindow;
                    if (mouseOverWindow)
                        mouseOverWindow.Repaint();

                    EditorGUI.indentLevel--;
                }
            }
#else
            EditorGUILayout.HelpBox("AutoLOD requires Unity 2017.3 or a later version", MessageType.Warning);
#endif

            EditorGUILayout.EndVertical();
        }
    }
}
