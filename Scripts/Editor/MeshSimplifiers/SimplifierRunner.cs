using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace UnityEditor.Experimental.AutoLOD
{

    /// <summary>
    /// Sometimes simplifer not running when create lots of backgroud workers. ( e.g., SimplygonMeshSimplifer )
    /// This helps to didn't make too many background wokers.
    /// Runs while maintaining a specified number of background wokers.
    ///
    /// 
    /// </summary>
    class SimplifierRunner : ScriptableSingleton<SimplifierRunner>
    {
        private const int k_MaxWorkerCount = 8;


        void OnEnable()
        {
            EditorApplication.update += EditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
        }

        private void EditorUpdate()
        {
            while (m_SimplificationActions.Count > 0 && m_WorkerCount < k_MaxWorkerCount)
            {
                ActionContainer action = m_SimplificationActions.Dequeue();
                RunWorker(action);
            }
        }

        private void RunWorker(ActionContainer action)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (sender, args) =>
            {
                if (action.DoAction != null)
                    action.DoAction();
            };
            worker.RunWorkerCompleted += (sender, args) =>
            {
                m_WorkerCount -= 1;
                if (action.CompleteAction != null)
                    action.CompleteAction();
            };

            m_WorkerCount += 1;

            worker.RunWorkerAsync();
        }


        public void EnqueueSimplification(Action doAction, Action completeAction)
        {
            lock (m_SimplificationActions)
            {
                ActionContainer container;
                container.DoAction = doAction;
                container.CompleteAction = completeAction;
                m_SimplificationActions.Enqueue(container);
            }
        }

        private struct ActionContainer
        {
            public Action DoAction;
            public Action CompleteAction;
        }


        private int m_WorkerCount = 0;
        private Queue<ActionContainer> m_SimplificationActions = new Queue<ActionContainer>();


    }
}
