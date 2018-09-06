using System.Collections;

namespace Unity.AutoLOD
{
    public static class IEnumeratorExtensions
    {
        public static void ExecuteCompletely(this IEnumerator enumerator)
        {
            while (enumerator.MoveNext()) ;
        }
    }
}
