using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.AutoLOD.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dbg = UnityEngine.Debug;
using UnityObject = UnityEngine.Object;


namespace Unity.AutoLOD
{
    public class SceneLOD : ScriptableSingleton<SceneLOD>
    {
        class SceneLODAssetProcessor : UnityEditor.AssetModificationProcessor
        {
            public static string[] OnWillSaveAssets(string[] paths)
            {
                foreach (string path in paths)
                {
                    if (path.EndsWith(".unity"))
                    {
                        var scene = SceneManager.GetSceneByPath(path);
                        if(!scene.IsValid()) continue;

                        try
                        {
                            AssetDatabase.StartAssetEditing();
                            var rootGameObjects = scene.GetRootGameObjects();
                            foreach (var go in rootGameObjects)
                            {
                                var lodVolume = go.GetComponent<LODVolume>();
                                if (lodVolume)
                                    PersistHLODs(lodVolume, path);
                            }
                        }
                        finally
                        {
                            AssetDatabase.StopAssetEditing();
                        }
                    }
                }
 
                return paths;
            }

            static void PersistHLODs(LODVolume lodVolume, string scenePath)
            {
                var hlodRoot = lodVolume.hlodRoot;
                if (hlodRoot)
                {
                    var mf = hlodRoot.GetComponent<MeshFilter>();
                    if (mf)
                    {
                        var sharedMesh = mf.sharedMesh;
                        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sharedMesh)))
                            SaveUniqueHLODAsset(sharedMesh, scenePath);
                    }
                }

                foreach (Transform child in lodVolume.transform)
                {
                    var childLODVolume = child.GetComponent<LODVolume>();
                    if (childLODVolume)
                        PersistHLODs(childLODVolume, scenePath);
                }
            }

            static void SaveUniqueHLODAsset(UnityObject asset, string scenePath)
            {
                if (!string.IsNullOrEmpty(scenePath))
                {
                    var directory = Path.GetDirectoryName(scenePath) + "/" + Path.GetFileNameWithoutExtension(scenePath) + "_HLOD/";
                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    var path = directory + Path.GetRandomFileName();
                    path = Path.ChangeExtension(path, "asset");
                    AssetDatabase.CreateAsset(asset, path);
                }
            }
        }

        public static bool activated { get { return s_Activated; } }

        public int coroutineQueueRemaining { get { return m_CoroutineQueue.Count; }}
        public long coroutineCurrentExecutionTime { get { return m_ServiceCoroutineExecutionTime.ElapsedMilliseconds; }}

        static bool s_HLODEnabled = true;
        static bool s_Activated;
        
        string m_CreateRootVolumeForScene = "Default"; // Set to some value, so new scenes don't auto-create
        LODVolume m_RootVolume;
        GameObject[] m_SelectedObjects;
        Dictionary<GameObject, Pose> m_SelectedObjectLastPose = new Dictionary<GameObject, Pose>();
        Queue<IEnumerator> m_CoroutineQueue = new Queue<IEnumerator>();
        Coroutine m_ServiceCoroutineQueue;
        bool m_SceneDirty;
        Stopwatch m_ServiceCoroutineExecutionTime = new Stopwatch();
        Camera m_LastCamera;
        HashSet<Renderer> m_ExcludedRenderers = new HashSet<Renderer>();
        Vector3 m_LastCameraPosition;
        Quaternion m_LastCameraRotation;

        // Local method variable caching
        List<Renderer> m_FoundRenderers = new List<Renderer>();
        HashSet<Renderer> m_ExistingRenderers = new HashSet<Renderer>();
        HashSet<Renderer> m_AddedRenderers = new HashSet<Renderer>();
        HashSet<Renderer> m_RemovedRenderers = new HashSet<Renderer>();

        void OnEnable()
        {
            if (LayerMask.NameToLayer(LODVolume.HLODLayer) == -1)
            {
                var layers = TagManager.GetRequiredLayers();
                foreach (var layer in layers)
                {
                    TagManager.AddLayer(layer);
                }
            }

            if (LayerMask.NameToLayer(LODVolume.HLODLayer) != -1)
            {
                Tools.lockedLayers |= LayerMask.GetMask(LODVolume.HLODLayer);
                s_Activated = true;
            }

            if (s_Activated)
                AddCallbacks();

            ResetServiceCoroutineQueue();
        }

        void OnDisable()
        {
            s_Activated = false;
            RemoveCallbacks();
        }

        void AddCallbacks()
        {
            EditorApplication.update += EditorUpdate;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            Selection.selectionChanged += OnSelectionChanged;
            Camera.onPreCull += PreCull;
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui += OnSceneGUI;
#else
            SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
        }

        void RemoveCallbacks()
        {
            EditorApplication.update -= EditorUpdate;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            Camera.onPreCull -= PreCull;
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui -= OnSceneGUI;
#else
            SceneView.onSceneGUIDelegate -= OnSceneGUI;
#endif
        }

        void OnHierarchyChanged()
        {
            m_SceneDirty = true;
            m_ExcludedRenderers.Clear();
        }

        void OnSelectionChanged()
        {
            if (!m_RootVolume)
                return;

            if (m_SelectedObjects != null)
                m_CoroutineQueue.Enqueue(UpdateOctreeBounds(m_SelectedObjects));

            m_SelectedObjects = Selection.gameObjects;
            if (m_SelectedObjects != null)
            {
                foreach (var selected in m_SelectedObjects)
                {
                    if (selected)
                    {
                        var selectedTransform = selected.transform;
                        m_SelectedObjectLastPose[selected] = new Pose(selectedTransform.position, selectedTransform.rotation);
                    }
                }
            }
        }

        void OnSceneGUI(SceneView sceneView)
        {
            var activeSceneName = SceneManager.GetActiveScene().name;

            var rect = sceneView.position;
            rect.x = 0f;
            rect.y = 0f;

            Handles.BeginGUI();
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();
            if (m_RootVolume && GUILayout.Button(s_HLODEnabled ? "Disable HLOD" : "Enable HLOD"))
            {
                s_HLODEnabled = !s_HLODEnabled;
                m_LastCamera = null;
            }
            else if (!m_RootVolume && m_CreateRootVolumeForScene != activeSceneName && GUILayout.Button("Activate SceneLOD"))
            {
                m_CreateRootVolumeForScene = activeSceneName;
                m_SceneDirty = true;
                m_LastCamera = null;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        IEnumerator UpdateOctreeBounds(GameObject[] gameObjects)
        {
            foreach (var go in gameObjects)
            {
                if (!go)
                    continue;

                Pose pose;
                if (m_SelectedObjectLastPose.TryGetValue(go, out pose))
                {
                    var goTransform = go.transform;
                    if (pose.position == goTransform.position && pose.rotation == goTransform.rotation)
                        continue;
                }

                yield return UpdateChangedRenderer(go);
            }
        }

        IEnumerator UpdateChangedRenderer(GameObject go)
        {
            if (!go)
                yield break;

            while (!m_RootVolume)
                yield return UpdateOctree();

            var transform = go.transform;
            var renderer = go.GetComponent<Renderer>();
            if (renderer)
            {
                if (transform.hasChanged && m_RootVolume.renderers.Contains(renderer))
                {
                    yield return m_RootVolume.UpdateRenderer(renderer);
                    yield return SetRootLODVolume(); // In case the BVH has grown or shrunk
                    transform.hasChanged = false;
                }
            }

            foreach (Transform child in transform)
            {
                yield return UpdateChangedRenderer(child.gameObject);
                if (!transform)
                    yield break;
            }
        }

        IEnumerator UpdateOctree()
        {
            if (!m_RootVolume)
            {
                yield return SetRootLODVolume();

                if (!m_RootVolume)
                {
                    if (m_CreateRootVolumeForScene == SceneManager.GetActiveScene().name)
                        m_RootVolume = LODVolume.Create();
                    else
                        yield break;
                }
            }

            var renderers = m_FoundRenderers;
            renderers.Clear();

            yield return ObjectUtils.FindObjectsOfType(renderers);

            // Remove any renderers that should not be there (e.g. HLODs)
            renderers.RemoveAll(r => m_ExcludedRenderers.Contains(r));
            renderers.RemoveAll(r =>
            {
                if (r)
                {
                    // Check against previous collection
                    if (m_ExistingRenderers.Contains(r))
                        return false;

                    if (r.gameObject.layer == LayerMask.NameToLayer(LODVolume.HLODLayer))
                    {
                        m_ExcludedRenderers.Add(r);
                        return true;
                    }

                    var mf = r.GetComponent<MeshFilter>();
                    if (!mf || (mf.sharedMesh && mf.sharedMesh.GetTopology(0) != MeshTopology.Triangles))
                    {
                        m_ExcludedRenderers.Add(r);
                        return true;
                    }

                    var lodGroup = r.GetComponentInParent<LODGroup>();
                    if (lodGroup)
                    {
                        var lods = lodGroup.GetLODs();

                        // Skip LOD0, so that we keep the original renderers in the list
                        for (int i = 1; i < lods.Length; i++)
                        {
                            if (lods[i].renderers.Contains(r))
                            {
                                m_ExcludedRenderers.Add(r);
                                return true;
                            }
                        }
                    }
                    else
                    {
                        // HLODs should come after traditional LODs, so exclude any standalone renderers
                        m_ExcludedRenderers.Add(r);
                        return true;
                    }
                }

                return false;
            });

            var existingRenderers = m_ExistingRenderers;
            existingRenderers.Clear();
            existingRenderers.UnionWith(m_RootVolume.renderers);

            var removed = m_RemovedRenderers;
            removed.Clear();
            removed.UnionWith(m_ExistingRenderers);
            removed.ExceptWith(renderers);

            var added = m_AddedRenderers;
            added.Clear();
            added.UnionWith(renderers);
            added.ExceptWith(existingRenderers);

            foreach (var r in removed)
            {
                if (existingRenderers.Contains(r))
                {
                    yield return m_RootVolume.RemoveRenderer(r);

                    // Check if the BVH shrunk
                    yield return SetRootLODVolume();
                }
            }

            foreach (var r in added)
            {
                if (!existingRenderers.Contains(r))
                {
                    yield return m_RootVolume.AddRenderer(r);
                    r.transform.hasChanged = false;

                    // Check if the BVH grew
                    yield return SetRootLODVolume();
                }
            }
        }

        IEnumerator SetRootLODVolume()
        {
            if (m_RootVolume)
            {
                var rootVolumeTransform = m_RootVolume.transform;
                var transformRoot = rootVolumeTransform.root;

                // Handle the case where the BVH has grown
                if (rootVolumeTransform != transformRoot)
                    m_RootVolume = transformRoot.GetComponent<LODVolume>();

                yield break;
            }

            // Handle initialization or the case where the BVH has shrunk
            LODVolume lodVolume = null;
            var scene = SceneManager.GetActiveScene();
            var rootGameObjects = scene.GetRootGameObjects();
            foreach (var go in rootGameObjects)
            {
                if (!go)
                    continue;

                lodVolume = go.GetComponent<LODVolume>();
                if (lodVolume)
                    break;

                yield return null;
            }
            
            if (lodVolume)
                m_RootVolume = lodVolume;

            m_ExcludedRenderers.Clear();
        }

        void EditorUpdate()
        {
            if ((m_CoroutineQueue.Count > 0 || m_SceneDirty || (m_RootVolume && m_RootVolume.dirty))
                && m_ServiceCoroutineQueue == null)
            {
                m_ServiceCoroutineQueue = MonoBehaviourHelper.StartCoroutine(ServiceCoroutineQueue());
            }
        }
        IEnumerator ServiceCoroutineQueue()
        {
            m_ServiceCoroutineExecutionTime.Start();

            if (m_SceneDirty)
            {
                m_CoroutineQueue.Enqueue(UpdateOctree());
                m_SceneDirty = false;
            }

            if (m_RootVolume && m_RootVolume.dirty)
                m_CoroutineQueue.Enqueue(m_RootVolume.UpdateHLODs());

            while (m_CoroutineQueue.Count > 0)
            {
                yield return MonoBehaviourHelper.StartCoroutine(m_CoroutineQueue.Dequeue());
            }

            ResetServiceCoroutineQueue();
        }

        void ResetServiceCoroutineQueue()
        {
            m_ServiceCoroutineQueue = null;
            m_ServiceCoroutineExecutionTime.Reset();
        }

        // PreCull is called before LODGroup updates
        void PreCull(Camera camera)
        {
            if (!m_RootVolume)
                return;

            var cameraType = camera.cameraType;
            var cameraTransform = camera.transform;
            var cameraPosition = cameraTransform.position;
            var cameraRotation = cameraTransform.rotation;

            if (((cameraType == CameraType.Game && camera == Camera.main) || cameraType == CameraType.SceneView)
                && (m_LastCamera != camera || m_LastCameraPosition != cameraPosition || m_LastCameraRotation != cameraRotation || m_SceneDirty))
            {
                var deltaForward = Vector3.Dot(cameraPosition - m_LastCameraPosition, cameraTransform.forward);

                UpdateLODGroup(m_RootVolume, camera, cameraPosition, m_LastCamera == camera && deltaForward < 0f);

                m_LastCamera = camera;
                m_LastCameraPosition = cameraPosition;
                m_LastCameraRotation = cameraRotation;
            }
        }

        bool UpdateLODGroup(LODVolume lodVolume, Camera camera, Vector3 cameraPosition, bool fastPath)
        {
            var lodGroupEnabled = s_HLODEnabled;

            var lodGroup = lodVolume.lodGroup;
            var lodGroupExists = lodGroup != null && lodGroup.lodGroup;

            // Start with leaf nodes first
            var lodVolumeTransform = lodVolume.transform;
            var childVolumes = lodVolume.childVolumes;
            foreach (var childVolume in childVolumes)
            {
                if (childVolume)
                {
                    if (!fastPath || !lodGroupExists || !lodGroup.lodGroup.enabled)
                        lodGroupEnabled &= UpdateLODGroup(childVolume, camera, cameraPosition, fastPath);
                }
            }
            
            if (lodGroupEnabled)
            {
                var allChildrenUsingCoarsestLOD = true;
                if (lodVolumeTransform.childCount == 0) // Leaf node
                {
                    var cached = lodVolume.cached;

                    // Disable all children LODGroups if an HLOD LODGroup could replace it
                    foreach (var r in cached)
                    {
                        var childLODGroup = r as LODVolume.LODGroupHelper;

                        if (childLODGroup != null && childLODGroup.GetCurrentLOD(camera, cameraPosition) != childLODGroup.GetMaxLOD())
                        {
                            allChildrenUsingCoarsestLOD = false;
                            break;
                        }
                    }

                    foreach (var r in cached)
                    {
                        var childLODGroup = r as LODVolume.LODGroupHelper;

                        if (childLODGroup != null)
                            childLODGroup.SetEnabled(!allChildrenUsingCoarsestLOD);
                        else if (r != null)
                            ((Renderer)r).enabled = !allChildrenUsingCoarsestLOD;
                    }
                }
                else
                {
                    foreach (var childVolume in childVolumes)
                    {
                        var childLODGroup = childVolume.lodGroup;
                        if (childLODGroup != null && childLODGroup.lodGroup)
                        {
                            var maxLOD = childLODGroup.GetMaxLOD();
                            if (maxLOD > 0 && childLODGroup.GetCurrentLOD(camera, cameraPosition) != maxLOD)
                            {
                                allChildrenUsingCoarsestLOD = false;
                                break;
                            }
                        }
                    }

                    foreach (var childVolume in childVolumes)
                    {
                        var childLODGroup = childVolume.lodGroup;
                        if (childLODGroup != null && childLODGroup.lodGroup)
                            childLODGroup.SetEnabled(!allChildrenUsingCoarsestLOD);
                    }
                }

                lodGroupEnabled &= allChildrenUsingCoarsestLOD;
            }
            else if (!s_HLODEnabled && lodVolumeTransform.childCount == 0) // Re-enable default renderers
            {
                foreach (var r in lodVolume.renderers)
                {
                    if (!r)
                        continue;

                    var childLODGroup = r.GetComponentInParent<LODGroup>();
                    if (childLODGroup)
                        childLODGroup.SetEnabled(true);
                    else
                        r.enabled = true;
                }
            }

            if (lodGroupExists)
                lodGroup.SetEnabled(lodGroupEnabled);

            return lodGroupEnabled;
        }
    }
}
