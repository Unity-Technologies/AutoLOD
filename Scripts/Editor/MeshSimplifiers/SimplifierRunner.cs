using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
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
            
            m_Workers.Clear();
            m_SimplificationActions.Clear();
            m_CompleteActions.Clear();

            EditorApplication.update += EditorUpdate;

            m_IsWorking = true;
            for (int i = 0; i < k_MaxWorkerCount; ++i)
            {
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += Worker_DoWork;
                worker.RunWorkerAsync();
                m_Workers.Add(worker);
            }
            
        }

        

        void OnDisable()
        {
            m_IsWorking = false;
            foreach (var worker in m_Workers)
            {
                worker.CancelAsync();
            }

            EditorApplication.update -= EditorUpdate;

            m_Workers.Clear();
            m_SimplificationActions.Clear();
            m_CompleteActions.Clear();

            
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (m_IsWorking)
            {
                if (m_SimplificationActions.Count == 0)
                {
                    Thread.Sleep(100);
                    continue;
                }

                ActionContainer actionContainer;
                lock (m_SimplificationActions)
                {
                    //double check for empty in lock.
                    if (m_SimplificationActions.Count == 0)
                        continue;

                    actionContainer = m_SimplificationActions.Dequeue();
                }

                actionContainer.DoAction();

                lock (m_CompleteActions)
                {
                    m_CompleteActions.Enqueue(actionContainer.CompleteAction);
                }
            }
        }
        private void EditorUpdate()
        {
            while (m_CompleteActions.Count > 0)
            {
                Action completeAction;
                lock (m_CompleteActions)
                {
                    if (m_CompleteActions.Count == 0)
                        return;

                    completeAction = m_CompleteActions.Dequeue();
                }

                completeAction();
            }
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


        private bool m_IsWorking = false;
        private List<BackgroundWorker> m_Workers = new List<BackgroundWorker>();

        private Queue<ActionContainer> m_SimplificationActions = new Queue<ActionContainer>();
        private Queue<Action> m_CompleteActions = new Queue<Action>();


    }
}
