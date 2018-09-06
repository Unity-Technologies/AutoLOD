using System.Collections;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Playables;

namespace Unity.AutoLOD
{
    public class ProfileAnimation : MonoBehaviour
    {
        public PlayableDirector playableDirector;
        public bool showProfilerWindow = true;

        IEnumerator Start()
        {
            var profilerWindowType = typeof(EditorApplication).Assembly.GetType("UnityEditor.ProfilerWindow");

            ProfilerDriver.ClearAllFrames();

            // Skip first frame stutter
#if UNITY_2017_3_OR_NEWER
            ProfilerDriver.enabled = false;
#endif
            yield return null;
#if UNITY_2017_3_OR_NEWER
            ProfilerDriver.enabled = true;
#endif

            if (showProfilerWindow)
            {
                var window = EditorWindow.GetWindow(profilerWindowType);
                window.maximized = true;
            }

            while (playableDirector.playableGraph.IsValid() && playableDirector.playableGraph.IsPlaying())
                yield return null;

#if UNITY_2017_3_OR_NEWER
            ProfilerDriver.enabled = false;
#endif
            EditorApplication.isPlaying = false;
        }
    }
}
