using System.Collections;
using UnityEngine;
using UnityEngine.Experimental.AutoLOD;

namespace UnityEditor.Experimental.AutoLOD
{
    /// <summary>
    /// A batcher that preserves materials when combining meshes (does not reduce draw calls)
    /// </summary>
    class MaterialPreservingBatcher : IBatcher
    {
        public IEnumerator Batch(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.enabled = true;
                yield return null;
            }
            StaticBatchingUtility.Combine(go);
        }
    }
}
