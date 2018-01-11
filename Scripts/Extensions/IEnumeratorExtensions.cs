using System.Collections;

namespace UnityEngine.Experimental.AutoLOD
{
    public static class IEnumeratorExtensions
    {
        public static void ExecuteCompletely(this IEnumerator enumerator)
        {
            while (enumerator.MoveNext()) ;
        }
    }
}
