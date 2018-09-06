using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.AutoLOD.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AutoLOD
{
    // Useful for launching co-routines in the Editor or executing something on the main thread
#if UNITY_EDITOR
    [InitializeOnLoad]
#else
    public class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        static T s_Instance;

        public ScriptableSingleton()
        {
            if (s_Instance != null)
                Debug.LogError((object) "ScriptableSingleton already exists. Did you query the singleton in a constructor?");
            else
                s_Instance = this as T;
        }

        public static T instance
        {
            get
            {
                if (s_Instance == null)
                    CreateInstance<T>().hideFlags = HideFlags.HideAndDontSave;

                return s_Instance;
            }
        }
    }
#endif

    public class MonoBehaviourHelper : ScriptableSingleton<MonoBehaviourHelper>
    {
        partial class Surrogate : MonoBehaviour { }

        Surrogate m_MonoBehaviour;

        MonoBehaviour monoBehaviour
        {
            get
            {
                if (!m_MonoBehaviour)
                {
#if UNITY_EDITOR
                    var go = EditorUtility.CreateGameObjectWithHideFlags("MonoBehaviourHelper", HideFlags.DontSave, typeof(Surrogate));
#else
                    var go = new GameObject("MonoBehaviourHelper", typeof(Surrogate));
#endif
                    m_MonoBehaviour = go.GetComponent<Surrogate>();
#if UNITY_EDITOR
                    m_MonoBehaviour.runInEditMode = true;
#endif
                }

                return m_MonoBehaviour;
            }
        }

        public static float? maxSharedExecutionTimeMS { get; set; }

        List<TimedEnumerator> m_Coroutines = new List<TimedEnumerator>();
        readonly Queue<Action> m_MainThreadActionQueue = new Queue<Action>();

        static int s_MainThreadId;

        public static Coroutine StartCoroutine(IEnumerator routine, float? maxIterationTimeMS = null)
        {
            return instance.monoBehaviour.StartCoroutine(CoroutineWrapper(routine, maxIterationTimeMS));
        }

        static IEnumerator CoroutineWrapper(IEnumerator routine, float? maxIterationTimeMS = null)
        {
            if (maxSharedExecutionTimeMS.HasValue)
            {
                if (maxIterationTimeMS.HasValue)
                    Debug.Log("Overriding specified iteration time because there is a shared time set.");

                maxIterationTimeMS = maxSharedExecutionTimeMS / (instance.m_Coroutines.Count + 1);
            }

            var timedEnumerator = new TimedEnumerator(routine, maxIterationTimeMS);

            //timedEnumerator.logEnabled = true;
            instance.m_Coroutines.Add(timedEnumerator);
            yield return timedEnumerator;
            instance.m_Coroutines.Remove(timedEnumerator);
        }

        static IEnumerator ActionCoroutine(Action action)
        {
            action();
            yield break;
        }

        // Simplified version of https://github.com/PimDeWitte/UnityMainThreadDispatcher
        public static void ExecuteOnMainThread(Action action)
        {
            if (IsMainThread())
            {
                if (instance)
                    action();
            }
            else
            {
                var completed = false;
                Action completionWrapper = () =>
                {
                    action();
                    completed = true;
                };

                var actionQueue = instance.m_MainThreadActionQueue;
                lock (actionQueue)
                    actionQueue.Enqueue(completionWrapper);

                while (!completed)
                    Thread.Sleep(100);
            }
        }

        public static bool IsMainThread()
        {
            return Thread.CurrentThread.ManagedThreadId == s_MainThreadId;
        }

        static MonoBehaviourHelper()
        {
            s_MainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update += EditorUpdate;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif

            if (m_MonoBehaviour)
                DestroyImmediate(m_MonoBehaviour.gameObject);
        }

#if !UNITY_EDITOR
        void Update()
        {
            EditorUpdate();
        }
#endif

        static void EditorUpdate()
        {
            var actionQueue = instance.m_MainThreadActionQueue;
            lock (actionQueue)
            {
                while (actionQueue.Count > 0)
                    StartCoroutine(ActionCoroutine(actionQueue.Dequeue()));
            }

            var coroutines = instance.m_Coroutines;
            if (coroutines.Count > 0)
            {
                if (maxSharedExecutionTimeMS.HasValue)
                {
                    var timeShare = maxSharedExecutionTimeMS / coroutines.Count;
                    foreach (var co in coroutines)
                    {
                        co.maxIterationTimeMS = timeShare;
                    }
                }

#if UNITY_EDITOR
                // this is necessary to cause our GameObject co-routines to tick at a stable frequency
                EditorApplication.QueuePlayerLoopUpdate();
#endif
            }
        }

        partial class Surrogate
        {
            //
            // Coroutine tests
            //
            [ContextMenu("CoTest")]
            void CoTest()
            {
                //StartCoroutine(TestRoutine());
                StartCoroutine(PrintNumbersSlowly());
            }

            [ContextMenu("MaxTimeTest")]
            void MaxTimeTest()
            {
                maxSharedExecutionTimeMS = 1f;

                //((MonoBehaviour)instance).StartCoroutine(ContinuallyFindObjects());
                //StartCoroutine(ContinuallyFindObjects());
                //StartCoroutine(ContinuallyFindObjects(), 1f);
                //StartCoroutine(ContinuallyFindObjects(), 1f);

                MonoBehaviourHelper.StartCoroutine(BurnCycles(), 1f);
                MonoBehaviourHelper.StartCoroutine(BurnCycles(), 1f);

                //((MonoBehaviour)instance).StartCoroutine(ContinuallyFindObjects());
            }

            IEnumerator ContinuallyFindObjects()
            {
                List<Renderer> renderers = new List<Renderer>();

                var counter = 0;
                while (true)
                {
                    counter++;
                    renderers.Clear();

                    //yield return StartCoroutine(ObjectUtils.FindObjectsOfType(renderers), 5f);
                    //Debug.Log(Time.renderedFrameCount);
                    //yield return ((MonoBehaviour)instance).StartCoroutine(ObjectUtils.FindObjectsOfType(renderers));
                    yield return ObjectUtils.FindObjectsOfType(renderers);

                    //Debug.Log(Time.renderedFrameCount);
                    Debug.Log("Renderers: " + renderers.Count);

                    if (counter > 1000)
                        break;
                }
            }

            IEnumerator BurnCycles()
            {
                var counter = 0;
                while (true)
                {
                    counter++;
                    yield return null;

                    //if (counter % 1000 == 0)
                    //    Debug.Log(counter);

                    if (counter > 10000)
                        break;
                }
            }

            IEnumerator TestRoutine()
            {
                yield return StartCoroutine(PrintNumbersSlowly());
            }

            IEnumerator PrintNumbersSlowly()
            {
                for (int i = 0; i < 10; i++)
                {
                    Debug.Log(i);
                    yield return new WaitForSecondsRealtime(1f);
                }
            }
        }
    }
}