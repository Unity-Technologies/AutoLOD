using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AutoLOD;
using Unity.AutoLOD.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[RequiresLayer(HLODLayer)]
public class LODVolume : MonoBehaviour
{
    [Serializable]
    public class LODGroupHelper
    {
        public LODGroup lodGroup
        {
            get { return m_LODGroup; }
            set
            {
                m_LODGroup = value;
                m_LODs = null;
            }
        }
        public LOD[] lods
        {
            get
            {
                if (m_LODs == null && m_LODGroup)
                    m_LODs = m_LODGroup.GetLODs();

                return m_LODs;
            }
        }
        public Vector3 referencePoint
        {
            get
            {
                if (!m_ReferencePoint.HasValue)
                    m_ReferencePoint = m_LODGroup ? m_LODGroup.transform.TransformPoint(m_LODGroup.localReferencePoint) : Vector3.zero;

                return m_ReferencePoint.Value;
            }
        }
        public float worldSpaceSize
        {
            get
            {
                if (!m_WorldSpaceSize.HasValue && m_LODGroup)
                    m_WorldSpaceSize = m_LODGroup.GetWorldSpaceSize();

                return m_WorldSpaceSize ?? 0f;
            }

        }

        public int maxLOD
        {
            get
            {
                if (!m_MaxLOD.HasValue)
                    m_MaxLOD = lodGroup.GetMaxLOD();

                return m_MaxLOD.Value;
            }
        }

        [SerializeField]
        LODGroup m_LODGroup;

        //Transform m_Transform;
        LOD[] m_LODs;
        Vector3? m_ReferencePoint;
        float? m_WorldSpaceSize;
        int? m_MaxLOD;
    }

    public const string HLODLayer = "HLOD";
    public static Type meshSimplifierType { set; get; }
    public static Type batcherType { set; get; }
    public static bool drawBounds { set; get; }

    public LODGroupHelper lodGroup
    {
        get
        {
            if (m_LODGroupHelper == null)
            {
                m_LODGroupHelper = new LODGroupHelper();
                m_LODGroupHelper.lodGroup = GetComponent<LODGroup>();
            }

            return m_LODGroupHelper;
        }
    }

    public bool dirty;
    public Bounds bounds;
    public GameObject hlodRoot;
    public List<Renderer> renderers = new List<Renderer>();
    public List<LODVolume> childVolumes = new List<LODVolume>();

    public List<object> cached
    {
        get
        {
            if (m_Cached.Count == 0)
            {
                foreach (var r in renderers)
                {
                    var lg = r.GetComponentInParent<LODGroup>();
                    if (lg)
                    {
                        var lgh = new LODGroupHelper();
                        lgh.lodGroup = lg;
                        m_Cached.Add(lgh);
                    }
                    else
                        m_Cached.Add(r);
                }
            }

            return m_Cached;
        }
    }

    const HideFlags k_DefaultHideFlags = HideFlags.None;
    const ushort k_VolumeSplitRendererCount = 32;//ushort.MaxValue;
    const string k_DefaultName = "LODVolumeNode";
    const string k_HLODRootContainer = "HLODs";
    const int k_Splits = 2;

    static int s_VolumesCreated;

    LODGroupHelper m_LODGroupHelper;
    List<object> m_Cached = new List<object>();

    IMeshSimplifier m_MeshSimplifier;

    static readonly Color[] k_DepthColors = new Color[]
    {
        Color.red,
        Color.green,
        Color.blue,
        Color.magenta,
        Color.yellow,
        Color.cyan,
        Color.grey,
    };

    void Awake()
    {
        if (Application.isPlaying)
        {
            // Prime helper object properties on start to avoid hitches later
            var primeCached = cached;
            var primeLODGroup = lodGroup;
            var primeTransform = transform;
            if (lodGroup.lodGroup)
            {
                var primeLODs = lodGroup.lods;
                var primeMaxLOD = lodGroup.maxLOD;
                var primeReferencePoint = lodGroup.referencePoint;
                var primeWorldSize = lodGroup.worldSpaceSize;
            }

            foreach (var c in cached)
            {
                var lgh = c as LODGroupHelper;
                if (lgh != null)
                {
                    var primeLODs = lgh.lods;
                    var primeMaxLOD = lgh.maxLOD;
                    var primeReferencePoint = lgh.referencePoint;
                    var primeWorldSize = lgh.worldSpaceSize;
                }
            }
        }
    }

    public static LODVolume Create()
    {
        GameObject go = new GameObject(k_DefaultName + s_VolumesCreated++, typeof(LODVolume));
        go.layer = LayerMask.NameToLayer(HLODLayer);
        LODVolume volume = go.GetComponent<LODVolume>();
        return volume;
    }

    public IEnumerator UpdateRenderer(Renderer renderer)
    {
        yield return RemoveRenderer(renderer);

        if (!this)
            yield break;

        var parent = transform.parent;
        var rootVolume = parent ? parent.GetComponentInParent<LODVolume>() : this; // In case the BVH has shrunk
        yield return rootVolume.AddRenderer(renderer);
    }

    public IEnumerator AddRenderer(Renderer renderer)
    {
        if (!this)
            yield break;

        if (!renderer)
            yield break;

        if (renderer.gameObject.layer == LayerMask.NameToLayer(HLODLayer))
            yield break;

        {
            var mf = renderer.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh && mf.sharedMesh.GetTopology(0) != MeshTopology.Triangles)
                yield break;
        }


        if (renderers.Count == 0 && Mathf.Approximately(bounds.size.magnitude, 0f))
        {
            bounds = renderer.bounds;
            bounds = GetCuboidBounds(bounds);

            transform.position = bounds.min;
        }

        // Each LODVolume maintains it's own list of renderers, which includes renderers in children nodes
        if (WithinBounds(renderer, bounds))
        {
            if (!renderers.Contains(renderer))
                renderers.Add(renderer);

            if (transform.childCount == 0)
            {
                if (renderers.Count > k_VolumeSplitRendererCount)
                    yield return Split();
                else
                    yield return SetDirty();
            }
            else
            {
                foreach (Transform child in transform)
                {
                    if (!renderer)
                        yield break;

                    var lodVolume = child.GetComponent<LODVolume>();
                    if (WithinBounds(renderer, lodVolume.bounds))
                    {
                        yield return lodVolume.AddRenderer(renderer);
                        break;
                    }
                    yield return null;
                }
            }
        }
        else if (!transform.parent)
        {
            if (transform.childCount == 0 && renderers.Count < k_VolumeSplitRendererCount)
            {
                bounds.Encapsulate(renderer.bounds);
                bounds = GetCuboidBounds(bounds);
                if (!renderers.Contains(renderer))
                    renderers.Add(renderer);

                yield return SetDirty();
            }
            else
            {
                // Expand and then try to add at the larger bounds
                var targetBounds = bounds;
                targetBounds.Encapsulate(renderer.bounds);
                targetBounds = GetCuboidBounds(targetBounds);
                yield return Grow(targetBounds);
                yield return transform.parent.GetComponent<LODVolume>().AddRenderer(renderer);
            }
        }

    }

    public IEnumerator RemoveRenderer(Renderer renderer)
    {
        if (renderers != null && renderers.Remove(renderer))
        {
            //Debug.Log("Removing " + renderer);

            foreach (Transform child in transform)
            {
                var lodVolume = child.GetComponent<LODVolume>();
                if (lodVolume)
                    yield return lodVolume.RemoveRenderer(renderer);

                yield return null;
            }

            renderers.RemoveAll(r => r == null);

            if (!transform.parent)
                yield return Shrink();

            if (!this)
                yield break;

            if (transform.childCount == 0)
                yield return SetDirty();
        }
    }

    [ContextMenu("Split")]
    void SplitContext()
    {
        MonoBehaviourHelper.StartCoroutine(Split());
    }

    IEnumerator Split()
    {
        Vector3 size = bounds.size;
        size.x /= k_Splits;
        size.y /= k_Splits;
        size.z /= k_Splits;

        for (int i = 0; i < k_Splits; i++)
        {
            for (int j = 0; j < k_Splits; j++)
            {
                for (int k = 0; k < k_Splits; k++)
                {
                    var lodVolume = Create();
                    var lodVolumeTransform = lodVolume.transform;
                    lodVolumeTransform.parent = transform;
                    var center = bounds.min + size * 0.5f + Vector3.Scale(size, new Vector3(i, j, k));
                    lodVolumeTransform.position = center;
                    lodVolume.bounds = new Bounds(center, size);

                    foreach (var r in renderers)
                    {
                        if (r && WithinBounds(r, lodVolume.bounds))
                        {
                            lodVolume.renderers.Add(r);
                            yield return lodVolume.SetDirty();
                        }
                    }

                    yield return null;
                }
            }
        }
    }

    [ContextMenu("Grow")]
    void GrowContext()
    {
        var targetBounds = bounds;
        targetBounds.center += Vector3.up;
        MonoBehaviourHelper.StartCoroutine(Grow(targetBounds));
    }

    IEnumerator Grow(Bounds targetBounds)
    {
        var direction = Vector3.Normalize(targetBounds.center - bounds.center);
        Vector3 size = bounds.size;
        size.x *= k_Splits;
        size.y *= k_Splits;
        size.z *= k_Splits;

        var corners = new Vector3[]
        {
            bounds.min,
            bounds.min + Vector3.right * bounds.size.x,
            bounds.min + Vector3.forward * bounds.size.z,
            bounds.min + Vector3.up * bounds.size.y,
            bounds.min + Vector3.right * bounds.size.x + Vector3.forward * bounds.size.z,
            bounds.min + Vector3.right * bounds.size.x + Vector3.up * bounds.size.y,
            bounds.min + Vector3.forward * bounds.size.x + Vector3.up * bounds.size.y,
            bounds.min + Vector3.right * bounds.size.x + Vector3.forward * bounds.size.z + Vector3.up * bounds.size.y
        };

        // Determine where the current volume is situated in the new expanded volume
        var best = 0f;
        var expandedVolumeCenter = bounds.min;
        foreach (var c in corners)
        {
            var dot = Vector3.Dot(c, direction);
            if (dot > best)
            {
                best = dot;
                expandedVolumeCenter = c;
            }
            yield return null;
        }

        var expandedVolume = Create();
        var expandedVolumeTransform = expandedVolume.transform;
        expandedVolumeTransform.position = expandedVolumeCenter;
        expandedVolume.bounds = new Bounds(expandedVolumeCenter, size);
        expandedVolume.renderers = new List<Renderer>(renderers);
        var expandedBounds = expandedVolume.bounds;

        transform.parent = expandedVolumeTransform;

        var splitSize = bounds.size;
        var currentCenter = bounds.center;
        for (int i = 0; i < k_Splits; i++)
        {
            for (int j = 0; j < k_Splits; j++)
            {
                for (int k = 0; k < k_Splits; k++)
                {
                    var center = expandedBounds.min + splitSize * 0.5f + Vector3.Scale(splitSize, new Vector3(i, j, k));
                    if (Mathf.Approximately(Vector3.Distance(center, currentCenter), 0f))
                        continue; // Skip the existing LODVolume we are growing from

                    var lodVolume = Create();
                    var lodVolumeTransform = lodVolume.transform;
                    lodVolumeTransform.parent = expandedVolumeTransform;
                    lodVolumeTransform.position = center;
                    lodVolume.bounds = new Bounds(center, splitSize);
                }
            }
        }
    }

    IEnumerator Shrink()
    {
        var populatedChildrenNodes = new List<LODVolume>();
        foreach (Transform child in transform)
        {
            var lodVolume = child.GetComponent<LODVolume>();
            var renderers = lodVolume.renderers;
            if (renderers != null && renderers.Count > 0)
                populatedChildrenNodes.Add(lodVolume);

            yield return null;
        }

        if (populatedChildrenNodes.Count == 1)
        {
            var newRootVolume = populatedChildrenNodes[0];
            newRootVolume.transform.parent = null;
            CleanupHLOD();
            DestroyImmediate(gameObject);

            yield return newRootVolume.Shrink();
        }
    }

    IEnumerator SetDirty()
    {
        dirty = true;

        childVolumes.Clear();
        foreach (Transform child in transform)
        {
            var cv = child.GetComponent<LODVolume>();
            if (cv)
                childVolumes.Add(cv);
        }

        cached.Clear();

        var lodVolumeParent = transform.parent;
        var parentLODVolume = lodVolumeParent ? lodVolumeParent.GetComponentInParent<LODVolume>() : null;
        if (parentLODVolume)
            yield return parentLODVolume.SetDirty();
    }

    static Bounds GetCuboidBounds(Bounds bounds)
    {
        // Expand bounds side lengths to maintain a cube
        var maxSize = Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.y), bounds.size.z);
        var extents = Vector3.one * maxSize * 0.5f;
        bounds.center = bounds.min + extents;
        bounds.extents = extents;

        return bounds;
    }

    void OnDrawGizmos()
    {
        if (drawBounds)
        {
            var depth = GetDepth(transform);
            DrawGizmos(Mathf.Max(1f - Mathf.Pow(0.9f, depth), 0.2f), GetDepthColor(depth));
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (Selection.activeGameObject == gameObject)
            DrawGizmos(1f, Color.magenta);
    }
#endif

    void DrawGizmos(float alpha, Color color)
    {
        color.a = alpha;
        Gizmos.color = color;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

#if UNITY_EDITOR
    [ContextMenu("GenerateHLOD")]
    void GenerateHLODContext()
    {
        MonoBehaviourHelper.StartCoroutine(GenerateHLOD());
    }

    public IEnumerator UpdateHLODs()
    {
        // Process children first, since we are now combining children HLODs to make parent HLODs
        foreach (Transform child in transform)
        {
            var childLODVolume = child.GetComponent<LODVolume>();
            if (childLODVolume)
                yield return childLODVolume.UpdateHLODs();

            if (!this)
                yield break;
        }

        if (dirty)
        {
            yield return GenerateHLOD(false);
            dirty = false;
        }
    }

    public IEnumerator GenerateHLOD(bool propagateUpwards = true)
    {
        var mergeChildrenVolumes = false;
        HashSet<Renderer> hlodRenderers = new HashSet<Renderer>();

        var rendererMaterials = renderers.SelectMany(r => r.sharedMaterials);
        yield return null;

        foreach (Transform child in transform)
        {
            var childLODVolume = child.GetComponent<LODVolume>();
            if (childLODVolume && childLODVolume.renderers.Count > 0)
            {
                if (rendererMaterials.Except(childLODVolume.renderers.SelectMany(r => r.sharedMaterials)).Any())
                {
                    hlodRenderers.Clear();
                    mergeChildrenVolumes = false;
                    break;
                }

                var childHLODRoot = childLODVolume.hlodRoot;
                if (childHLODRoot)
                {
                    var childHLODRenderer = childHLODRoot.GetComponent<Renderer>();
                    if (childHLODRenderer)
                    {
                        mergeChildrenVolumes = true;
                        hlodRenderers.Add(childHLODRenderer);
                        continue;
                    }
                }

                hlodRenderers.Clear();
                mergeChildrenVolumes = false;
                break;
            }

            yield return null;
        }

        if (!mergeChildrenVolumes)
        {
            foreach (var r in renderers)
            {
                var mr = r as MeshRenderer;
                if (mr)
                {
                    // Use the coarsest LOD if it exists
                    var mrLODGroup = mr.GetComponentInParent<LODGroup>();
                    if (mrLODGroup)
                    {
                        var mrLODs = mrLODGroup.GetLODs();
                        var maxLOD = mrLODGroup.GetMaxLOD();
                        var mrLOD = mrLODs[maxLOD];
                        foreach (var lr in mrLOD.renderers)
                        {
                            if (lr && lr.GetComponent<MeshFilter>())
                                hlodRenderers.Add(lr);
                        }
                    }
                    else if (mr.GetComponent<MeshFilter>())
                    {
                        hlodRenderers.Add(mr);
                    }
                }

                yield return null;
            }
        }

        var lodRenderers = new List<Renderer>();
        CleanupHLOD();

        GameObject hlodRootContainer = null;
        yield return ObjectUtils.FindGameObject(k_HLODRootContainer, root =>
        {
            if (root)
                hlodRootContainer = root;
        });

        if (!hlodRootContainer)
            hlodRootContainer = new GameObject(k_HLODRootContainer);

        var hlodLayer = LayerMask.NameToLayer(HLODLayer);

        hlodRoot = new GameObject("HLOD");
        hlodRoot.layer = hlodLayer;
        hlodRoot.transform.parent = hlodRootContainer.transform;

        if (mergeChildrenVolumes)
        {
            Material sharedMaterial = null;

            CombineInstance[] combine = new CombineInstance[hlodRenderers.Count];
            var i = 0;
            foreach (var r in hlodRenderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                var ci = new CombineInstance();
                ci.transform = r.transform.localToWorldMatrix;
                ci.mesh = mf.sharedMesh;
                combine[i] = ci;

                if (sharedMaterial == null)
                    sharedMaterial = r.sharedMaterial;

                i++;
            }

            var sharedMesh = new Mesh();
#if UNITY_2017_3_OR_NEWER
            sharedMesh.indexFormat = IndexFormat.UInt32;
#endif
            sharedMesh.CombineMeshes(combine, true, true);
            sharedMesh.RecalculateBounds();
            var meshFilter = hlodRoot.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = sharedMesh;

            var meshRenderer = hlodRoot.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = sharedMaterial;
        }
        else
        {
            var parent = hlodRoot.transform;
            foreach (var r in hlodRenderers)
            {
                var rendererTransform = r.transform;

                var child = new GameObject(r.name, typeof(MeshFilter), typeof(MeshRenderer));
                child.layer = hlodLayer;
                var childTransform = child.transform;
                childTransform.SetPositionAndRotation(rendererTransform.position, rendererTransform.rotation);
                childTransform.localScale = rendererTransform.lossyScale;
                childTransform.SetParent(parent, true);

                var mr = child.GetComponent<MeshRenderer>();
                EditorUtility.CopySerialized(r.GetComponent<MeshFilter>(), child.GetComponent<MeshFilter>());
                EditorUtility.CopySerialized(r.GetComponent<MeshRenderer>(), mr);

                lodRenderers.Add(mr);
            }
        }
        LOD lod = new LOD();

        var lodGroup = GetComponent<LODGroup>();
        if (!lodGroup)
            lodGroup = gameObject.AddComponent<LODGroup>();
        this.lodGroup.lodGroup = lodGroup;

        if (!mergeChildrenVolumes)
        {
            var batcher = (IBatcher)Activator.CreateInstance(batcherType);
            yield return batcher.Batch(hlodRoot);
        }

        lod.renderers = hlodRoot.GetComponentsInChildren<Renderer>(false);
        lodGroup.SetLODs(new LOD[] { lod });

        if (propagateUpwards)
        {
            var lodVolumeParent = transform.parent;
            var parentLODVolume = lodVolumeParent ? lodVolumeParent.GetComponentInParent<LODVolume>() : null;
            if (parentLODVolume)
                yield return parentLODVolume.GenerateHLOD();
        }
    }
#endif

    void CleanupHLOD()
    {
        if (hlodRoot) // Clean up old HLOD
        {
#if UNITY_EDITOR
            var mf = hlodRoot.GetComponent<MeshFilter>();
            if (mf)
                DestroyImmediate(mf.sharedMesh, true); // Clean up file on disk
#endif
            DestroyImmediate(hlodRoot);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("GenerateLODs")]
    void GenerateLODsContext()
    {
        GenerateLODs();
    }

    void GenerateLODs()
    {
        int maxLOD = 1;
        var go = gameObject;

        var hlodLayer = LayerMask.NameToLayer(HLODLayer);

        var lodGroup = go.GetComponent<LODGroup>();
        if (lodGroup)
        {
            var lods = new LOD[maxLOD + 1];
            var lod0 = lodGroup.GetLODs()[0];
            lod0.screenRelativeTransitionHeight = 0.5f;
            lods[0] = lod0;

            var meshes = new List<Mesh>();

            var totalMeshCount = maxLOD * lod0.renderers.Length;
            for (int l = 1; l <= maxLOD; l++)
            {
                var lodRenderers = new List<MeshRenderer>();
                foreach (var mr in lod0.renderers)
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    var sharedMesh = mf.sharedMesh;

                    var lodTransform = EditorUtility.CreateGameObjectWithHideFlags(string.Format("{0} LOD{1}", sharedMesh.name, l),
                        k_DefaultHideFlags, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    lodTransform.gameObject.layer = hlodLayer;
                    lodTransform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                    lodTransform.localScale = mf.transform.lossyScale;
                    lodTransform.SetParent(mf.transform, true);

                    var lodMF = lodTransform.GetComponent<MeshFilter>();
                    var lodRenderer = lodTransform.GetComponent<MeshRenderer>();

                    lodRenderers.Add(lodRenderer);

                    EditorUtility.CopySerialized(mf, lodMF);
                    EditorUtility.CopySerialized(mf.GetComponent<MeshRenderer>(), lodRenderer);

                    var simplifiedMesh = new Mesh();
                    simplifiedMesh.name = sharedMesh.name + string.Format(" LOD{0}", l);
                    lodMF.sharedMesh = simplifiedMesh;
                    meshes.Add(simplifiedMesh);

                    var meshLOD = new MeshLOD();
                    meshLOD.inputMesh = sharedMesh;
                    meshLOD.outputMesh = simplifiedMesh;
                    meshLOD.quality = Mathf.Pow(0.5f, l);
                    meshLOD.meshSimplifierType = meshSimplifierType;
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
                var assetPath = AssetDatabase.GetAssetPath(prefab);
                var pathPrefix = Path.GetDirectoryName(assetPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(assetPath);
                var lodsAssetPath = pathPrefix + "_lods.asset";
                ObjectUtils.CreateAssetFromObjects(meshes.ToArray(), lodsAssetPath);
            }
        }
    }
#endif

    public static int GetDepth(Transform transform)
    {
        int count = 0;
        Transform parent = transform.parent;
        while (parent)
        {
            count++;
            parent = parent.parent;
        }

        return count;
    }

    public static Color GetDepthColor(int depth)
    {
        return k_DepthColors[depth % k_DepthColors.Length];
    }

    static bool WithinBounds(Renderer r, Bounds bounds)
    {
        // Use this approach if we are not going to split meshes and simply put the object in one volume or another
        return Mathf.Approximately(bounds.size.magnitude, 0f) || bounds.Contains(r.bounds.center);
    }
}
