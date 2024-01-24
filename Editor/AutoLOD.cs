#if UNITY_2018_4_OR_NEWER
#define HAS_MINIMUM_REQUIRED_VERSION
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.AutoLOD.Utilities;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.AutoLOD
{
    [InitializeOnLoad]
    class AutoLOD
    {
        const string k_MinimumRequiredVersion = "AutoLOD requires Unity 2018.4 or a later version";

        const HideFlags k_DefaultHideFlags = HideFlags.None;
        const string k_MaxExecutionTime = "AutoLOD.MaxExecutionTime";
        const int k_DefaultMaxExecutionTime = 8;
        const string k_DefaultMeshSimplifier = "AutoLOD.DefaultMeshSimplifier";
        const string k_DefaultMeshSimplifierDefault = "QuadricMeshSimplifier";
        const string k_DefaultMeshSimplifierDefine = "ENABLE_UNITYMESHSIMPLIFIER";
        const string k_DefaultBatcher = "AutoLOD.DefaultBatcher";
        const string k_MaxLOD = "AutoLOD.MaxLOD";
        const int k_DefaultMaxLOD = 2;
        const string k_GenerateOnImport = "AutoLOD.GenerateOnImport";
        const string k_SaveAssets = "AutoLOD.SaveAssets";
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
                var type = Type.GetType(EditorPrefs.GetString(k_DefaultMeshSimplifier, k_DefaultMeshSimplifierDefault));
                if (type == null && meshSimplifiers.Count > 0)
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
                var type = Type.GetType(EditorPrefs.GetString(k_DefaultBatcher, null));
                if (type == null && batchers.Count > 0)
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
            get { return EditorPrefs.GetBool(k_GenerateOnImport, false); }
        }

        static bool saveAssets
        {
            get { return EditorPrefs.GetBool(k_SaveAssets, true); }
            set
            {
                EditorPrefs.SetBool(k_SaveAssets, value);
                UpdateDependencies();
            }
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

        static List<Type> meshSimplifiers
        {
            get
            {
                if (s_MeshSimplifiers == null || s_MeshSimplifiers.Count == 0)
                    s_MeshSimplifiers = ObjectUtils.GetImplementationsOfInterface(typeof(IMeshSimplifier)).ToList();

                return s_MeshSimplifiers;
            }
        }

        static List<Type> batchers
        {
            get
            {
                if (s_Batchers == null || s_Batchers.Count == 0)
                    s_Batchers = ObjectUtils.GetImplementationsOfInterface(typeof(IBatcher)).ToList();

                return s_Batchers;
            }
        }

        static SceneLOD s_SceneLOD;
        static List<Type> s_MeshSimplifiers;
        static List<Type> s_Batchers;
        static IPreferences s_SimplifierPreferences;

#if HAS_MINIMUM_REQUIRED_VERSION
        static IEnumerator GetDefaultSimplifier()
        {
            var list = Client.List(true);
            while (!list.IsCompleted)
                yield return null;

            PackageStatus status = PackageStatus.Unknown;
            if (list.Status == StatusCode.Success)
            {
                foreach (var package in list.Result)
                {
                    if (package.name == "com.whinarn.unitymeshsimplifier")
                    {
                        status = package.status;
                        break;
                    }
                }
            }

            if (status != PackageStatus.Available
                && EditorUtility.DisplayDialog("Install Default Mesh Simplifier?",
                    "You are missing a default mesh simplifier. Would you like to install one?",
                    "Yes", "No"))
            {
                var request = Client.Add("https://github.com/Whinarn/UnityMeshSimplifier.git");
                while (!request.IsCompleted)
                    yield return null;

                switch (request.Status)
                {
                    case StatusCode.Success:
                        status = PackageStatus.Available;
                        break;
                    case StatusCode.InProgress:
                        status = PackageStatus.InProgress;
                        break;
                    case StatusCode.Failure:
                        Debug.LogError($"AutoLOD: {request.Error.message}");
                        break;
                }
            }

            if (status == PackageStatus.Available)
            {
                // Cribbed from ConditionalCompilationUtility
                // TODO: Remove when minimum version is 2019 LTS and use define constraints instead
                var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                if (buildTargetGroup == BuildTargetGroup.Unknown)
                {
                    var propertyInfo = typeof(EditorUserBuildSettings).GetProperty("activeBuildTargetGroup", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    if (propertyInfo != null)
                        buildTargetGroup = (BuildTargetGroup)propertyInfo.GetValue(null, null);
                }

                var previousProjectDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
                var projectDefines = previousProjectDefines.Split(';').ToList();
                if (!projectDefines.Contains(k_DefaultMeshSimplifierDefine, StringComparer.OrdinalIgnoreCase))
                {
                    EditorApplication.LockReloadAssemblies();

                    projectDefines.Add(k_DefaultMeshSimplifierDefine);

                    // This will trigger another re-compile, which needs to happen, so all the custom attributes will be visible
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, string.Join(";", projectDefines.ToArray()));

                    // Let other systems execute before reloading assemblies
                    yield return null;
                    EditorApplication.UnlockReloadAssemblies();
                }
            }
            else if (status != PackageStatus.InProgress)
            {
                Debug.LogError("AutoLOD: You must set a valid Default Mesh Simplifier under Edit -> Preferences");
            }            
        }
#endif        
        
        static void UpdateDependencies()
        {
#if HAS_MINIMUM_REQUIRED_VERSION
            if (meshSimplifierType == null)
            {
                MonoBehaviourHelper.StartCoroutine(GetDefaultSimplifier());
                ModelImporterLODGenerator.enabled = false;
                return;
            }

            MonoBehaviourHelper.maxSharedExecutionTimeMS = maxExecutionTime == 0 ? Mathf.Infinity : maxExecutionTime;

            LODDataEditor.meshSimplifier = meshSimplifierType.AssemblyQualifiedName;
            LODDataEditor.batcher = batcherType.AssemblyQualifiedName;
            LODDataEditor.maxLODGenerated = maxLOD;
            LODDataEditor.initialLODMaxPolyCount = initialLODMaxPolyCount;

            LODVolume.meshSimplifierType = meshSimplifierType;
            LODVolume.batcherType = batcherType;
            LODVolume.drawBounds = sceneLODEnabled && showVolumeBounds;

            ModelImporterLODGenerator.saveAssets = saveAssets;
            ModelImporterLODGenerator.meshSimplifierType = meshSimplifierType;
            ModelImporterLODGenerator.maxLOD = maxLOD;
            ModelImporterLODGenerator.enabled = generateOnImport;
            ModelImporterLODGenerator.initialLODMaxPolyCount = initialLODMaxPolyCount;

            if (sceneLODEnabled && !SceneLOD.activated)
            {
                if (!SceneLOD.instance)
                    Debug.LogError("SceneLOD failed to start");
            }
            else if (!sceneLODEnabled && SceneLOD.activated)
            {
                UnityObject.DestroyImmediate(SceneLOD.instance);
            }
#else
            ModelImporterLODGenerator.enabled = false;
#endif
        }

        static AutoLOD()
        {
#if HAS_MINIMUM_REQUIRED_VERSION
            UpdateDependencies();
#else
            Debug.LogWarning(k_MinimumRequiredVersion);
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
                var prefabType = PrefabUtility.GetPrefabAssetType(selection);
                if (prefabType == PrefabAssetType.Model)
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
                else if (prefabType == PrefabAssetType.Regular)
                {
                    GenerateLODs(new MenuCommand(null));
                }
            }
        }

        [MenuItem("Assets/AutoLOD/Generate LOD", true)]
        static bool CanForceGenerateLOD()
        {
            var selection = Selection.activeGameObject;
            var prefabType = selection ? PrefabUtility.GetPrefabAssetType(selection) : PrefabAssetType.NotAPrefab;
            return selection && prefabType == PrefabAssetType.Model || prefabType == PrefabAssetType.Regular;
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

                    if (selection && PrefabUtility.GetPrefabAssetType(selection) == PrefabAssetType.Regular)
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(selection);
                        callback(go);
                        PrefabUtility.SaveAsPrefabAsset(go, AssetDatabase.GetAssetPath(selection));
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
                        simplifiedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                        simplifiedMesh.name = sharedMesh.name + string.Format(" LOD{0}", l);
                        lodMF.sharedMesh = simplifiedMesh;
                        meshes.Add(simplifiedMesh);

                        var meshLOD = MeshLOD.GetGenericInstance(meshSimplifierType);
                        meshLOD.InputMesh = sharedMesh;
                        meshLOD.OutputMesh = simplifiedMesh;
                        meshLOD.Quality = Mathf.Pow(0.5f, l);
                        meshLOD.Generate();
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

                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
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

            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
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

#if HAS_MINIMUM_REQUIRED_VERSION
            // Max execution time
            {
                var label = new GUIContent("Max Execution Time (ms)",
                    "One of the features of AutoLOD is to keep the editor running responsively, so it’s possible to set"
                    + "the max execution time for coroutines that run. AutLOD will spawn LOD generators on separate "
                    + "threads, however, some generators may require main thread usage for accessing non thread-safe "
                    + "Unity data structures and classes.");

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
                    var label = new GUIContent("Default Mesh Simplifier", "All simplifiers (IMeshSimplifier) are "
                        + "enumerated and provided here for selection. By allowing for multiple implementations, "
                        + "different approaches can be compared. The default mesh simplifier is used to generate LODs "
                        + "on import and when explicitly called.");

                    var displayedOptions = meshSimplifiers.Select(t => t.Name).ToArray();
                    EditorGUI.BeginChangeCheck();
                    var selected = EditorGUILayout.Popup(label, Array.IndexOf(displayedOptions, type.Name), displayedOptions);
                    if (EditorGUI.EndChangeCheck())
                        meshSimplifierType = meshSimplifiers[selected];

                    if (meshSimplifierType != null && typeof(IMeshSimplifier).IsAssignableFrom(meshSimplifierType))
                    {
                        if (s_SimplifierPreferences == null || s_SimplifierPreferences.GetType() != meshSimplifierType)
                            s_SimplifierPreferences = Activator.CreateInstance(meshSimplifierType) as IPreferences;

                        if (s_SimplifierPreferences != null)
                        {
                            EditorGUI.indentLevel++;
                            s_SimplifierPreferences.OnPreferencesGUI();
                            EditorGUI.indentLevel--;
                        }
                    }
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
                    var label = new GUIContent("Default Batcher", "All simplifiers (IMeshSimplifier) are "
                        + "enumerated and provided here for selection. By allowing for multiple implementations, "
                        + "different approaches can be compared. The default batcher is used in HLOD generation when "
                        + "combining objects that are located within the same LODVolume.");

                    var displayedOptions = batchers.Select(t => t.Name).ToArray();
                    EditorGUI.BeginChangeCheck();
                    var selected = EditorGUILayout.Popup(label, Array.IndexOf(displayedOptions, type.Name), displayedOptions);
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
                var label = new GUIContent("Maximum LOD Generated", "Controls the depth of the generated LOD chain");

                var maxLODValues = Enumerable.Range(0, LODData.MaxLOD + 1).ToArray();
                EditorGUI.BeginChangeCheck();
                int maxLODGenerated = EditorGUILayout.IntPopup(label, maxLOD,
                    maxLODValues.Select(v => new GUIContent(v.ToString())).ToArray(), maxLODValues);
                if (EditorGUI.EndChangeCheck())
                    maxLOD = maxLODGenerated;
            }

            // Control LOD0 maximum poly count
            {
                var label = new GUIContent("Initial LOD Max Poly Count", "In the case where non realtime-ready assets "
                    + "are brought into Unity these would normally perform poorly. Being able to set a max poly count "
                    + "for LOD0 allows even the largest of meshes to import with performance-minded defaults.");

                EditorGUI.BeginChangeCheck();
                var maxPolyCount = EditorGUILayout.IntField(label, initialLODMaxPolyCount);
                if (EditorGUI.EndChangeCheck())
                    initialLODMaxPolyCount = maxPolyCount;
            }

            // Generate LODs on import
            {
                var label = new GUIContent("Generate on Import", "Controls whether automatic LOD generation will happen "
                    + "on import. Even if this option is disabled it is still possible to generate LOD chains "
                    + "individually on individual files.");

                EditorGUI.BeginChangeCheck();
                var generateLODsOnImport = EditorGUILayout.Toggle(label, generateOnImport);
                if (EditorGUI.EndChangeCheck())
                    generateOnImport = generateLODsOnImport;
            }

            // Turn off/on saving assets (performance feature
            {
                var label = new GUIContent("Save Assets",
                    "This can speed up performance, but may cause errors with some simplifiers");
                EditorGUI.BeginChangeCheck();
                var saveAssetsOnImport = EditorGUILayout.Toggle(label, saveAssets);
                if (EditorGUI.EndChangeCheck())
                    saveAssets = saveAssetsOnImport;
            }


            // Use SceneLOD?
            {
                var label = new GUIContent("Scene LOD", "Enable Hierarchical LOD (HLOD) support for scenes, "
                    + "which will automatically generate and stay updated in the background.");

                EditorGUI.BeginChangeCheck();
                var enabled = EditorGUILayout.Toggle(label, sceneLODEnabled);
                if (EditorGUI.EndChangeCheck())
                    sceneLODEnabled = enabled;

                if (sceneLODEnabled)
                {
                    label = new GUIContent("Show Volume Bounds", "This will display the bounds visually of the bounding "
                        + "volume hierarchy (currently an Octree)");

                    EditorGUI.indentLevel++;
                    EditorGUI.BeginChangeCheck();
                    var showBounds = EditorGUILayout.Toggle(label, showVolumeBounds);
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
            EditorGUILayout.HelpBox(k_MinimumRequiredVersion, MessageType.Warning);
#endif

            EditorGUILayout.EndVertical();
        }
    }
}
