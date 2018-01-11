using System.Collections;

namespace UnityEngine.Experimental.AutoLOD
{
    public interface IBatcher
    {
        /// <summary>
        /// Combine children renderers of this GameObject (NOTE: Runs as a coroutine)
        /// </summary>
        /// <param name="go">GameObject hierarchy to batch</param>
        IEnumerator Batch(GameObject go);
    }
}
