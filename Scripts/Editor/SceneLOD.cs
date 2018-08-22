using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor.Experimental.AutoLOD.Utilities;
using UnityEngine;
using UnityEngine.Experimental.AutoLOD;
using UnityEngine.SceneManagement;
using Dbg = UnityEngine.Debug;
using Debug = System.Diagnostics.Debug;
using UnityObject = UnityEngine.Object;


namespace UnityEditor.Experimental.AutoLOD
{
    public class SceneLOD : ScriptableSingleton<SceneLOD>
    {
        private const string k_GenerateSceneLODMenuPath = "AutoLOD/Generate SceneLOD";
        private const string k_DestroySceneLODMenuPath = "AutoLOD/Destroy SceneLOD";
        private const string k_UpdateSceneLODMenuPath = "AutoLOD/Update SceneLOD";
        private const string k_ShowVolumeBoundsMenuPath = "AutoLOD/Show Volume Bounds";
        class SceneLODAssetProcessor : AssetModificationProcessor
        {
            public static string[] OnWillSaveAssets(string[] paths)
            {
                foreach (string path in paths)
                {
                    if (path.Contains(".unity"))
                    {
                        AssetDatabase.StartAssetEditing();

                        var scene = SceneManager.GetSceneByPath(path);
                        var rootGameObjects = scene.GetRootGameObjects();
                        foreach (var go in rootGameObjects)
                        {
                            var lodVolume = go.GetComponent<LODVolume>();
                            if (lodVolume)
                                PersistHLODs(lodVolume, path);
                        }

                        AssetDatabase.StopAssetEditing();
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
                    var sharedMesh= mf.sharedMesh;
                    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sharedMesh)))
                    {
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
#if UNITY_2017_3_OR_NEWER
            if (LayerMask.NameToLayer(LODVolume.HLODLayer) == -1)
            {
                Dbg.LogWarning("Adding missing HLOD layer");

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

            m_ServiceCoroutineQueue = null;
#endif
            if (m_RootVolume != null)
                m_RootVolume.ResetLODGroup();

            Menu.SetChecked(k_ShowVolumeBoundsMenuPath, Settings.ShowVolumeBounds);
        }

        void OnDisable()
        {
            s_Activated = false;
            RemoveCallbacks();

            if (m_RootVolume != null)
                m_RootVolume.ResetLODGroup();
        }

        void AddCallbacks()
        {
            EditorApplication.update += EditorUpdate;
            Selection.selectionChanged += OnSelectionChanged;
            Camera.onPreCull += PreCull;
            SceneView.onSceneGUIDelegate += OnSceneGUI;
        }

        void RemoveCallbacks()
        {
            EditorApplication.update -= EditorUpdate;
            Selection.selectionChanged -= OnSelectionChanged;
            Camera.onPreCull -= PreCull;
            SceneView.onSceneGUIDelegate -= OnSceneGUI;
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

                if ( m_RootVolume != null )
                    m_RootVolume.ResetLODGroup();
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
                    {
                        Dbg.Log("Creating root volume");
                        m_RootVolume = LODVolume.Create();
                    }
                    else
                    {
                        yield break;
                    }
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
            if ((m_CoroutineQueue.Count > 0 || m_SceneDirty || (m_RootVolume && m_RootVolume.dirty)) && m_ServiceCoroutineQueue == null)
                m_ServiceCoroutineQueue = MonoBehaviourHelper.StartCoroutine(ServiceCoroutineQueue());
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
                yield return MonoBehaviourHelper.StartCoroutine(m_CoroutineQueue.Dequeue());

            m_ServiceCoroutineQueue = null;
            m_ServiceCoroutineExecutionTime.Reset();
        }

        // PreCull is called before LODGroup updates
        void PreCull(Camera camera)
        {

            //if playing in editor, not use this flow.
            if (Application.isPlaying == true)
                return;

            if (s_HLODEnabled == false)
                return;
            
            if (!m_RootVolume)
                return;

            var cameraTransform = camera.transform;
            var cameraPosition = cameraTransform.position;

            m_RootVolume.UpdateLODGroup(camera, cameraPosition, false);
        }

#region Menu
        //AutoLOD requires Unity 2017.3 or a later version
#if UNITY_2017_3_OR_NEWER
        [MenuItem(k_GenerateSceneLODMenuPath, true, priority = 1)]
        static bool CanGenerateSceneLOD(MenuCommand menuCommand)
        {
            return instance.m_RootVolume == null;
        }

        [MenuItem(k_GenerateSceneLODMenuPath, priority = 1)]
        static void GenerateSceneLOD(MenuCommand menuCommand)
        {
            instance.m_CreateRootVolumeForScene = SceneManager.GetActiveScene().name;
            instance.m_SceneDirty = true;
            instance.m_LastCamera = null;
        }


        [MenuItem(k_DestroySceneLODMenuPath, true, priority = 1)]
        static bool CanDestroySceneLOD(MenuCommand menuCommand)
        {
            return instance.m_RootVolume != null;
        }

        [MenuItem(k_DestroySceneLODMenuPath, priority = 1)]
        static void DestroySceneLOD(MenuCommand menuCommand)
        {
            MonoBehaviourHelper.StartCoroutine(ObjectUtils.FindGameObject("HLODs",
                root => { DestroyImmediate(root); }));
            DestroyImmediate(instance.m_RootVolume.gameObject);
            instance.m_SceneDirty = false;
        }

        [MenuItem(k_UpdateSceneLODMenuPath, true, priority = 1)]
        static bool CanUpdateSceneLOD(MenuCommand menuCommand)
        {
            return instance.m_RootVolume != null;
        }

        [MenuItem(k_UpdateSceneLODMenuPath, priority = 1)]
        static void UpdateSceneLOD(MenuCommand menuCommand)
        {
            DestroySceneLOD(menuCommand);
            GenerateSceneLOD(menuCommand);
        }

        [MenuItem(k_ShowVolumeBoundsMenuPath, priority = 50)]
        static void ShowVolumeBounds(MenuCommand menuCommand)
        {
            bool showVolume = !Settings.ShowVolumeBounds;
            Menu.SetChecked(k_ShowVolumeBoundsMenuPath, showVolume);

            LODVolume.drawBounds = showVolume;
            Settings.ShowVolumeBounds = showVolume;

            // Force more frequent updating
            var mouseOverWindow = EditorWindow.mouseOverWindow;
            if (mouseOverWindow)
                mouseOverWindow.Repaint();

        }
#endif
#endregion
    }
}
