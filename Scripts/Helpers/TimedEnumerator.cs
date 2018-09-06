using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.AutoLOD
{
    public class TimedEnumerator : IEnumerator
    {
        public float? maxIterationTimeMS { get; set; }
        public bool logEnabled { get; set; }
        public float totalExecutionTime { get; private set; }
        public float selfExecutionTime { get; private set; }
        public float iterationExecutionTime { get; private set; }

        float? m_FirstIterationTime;
        float? m_FinalIterationTime;
        Stack<IEnumerator> m_EnumeratorStack = new Stack<IEnumerator>();

        public bool MoveNext()
        {
            var sw = new Stopwatch();
            sw.Start();

            // We won't be able to capture time spent in other YieldInstructions because those are objects that get waited
            // on separately, so we keep track of absolute time until the iterator is complete
            if (!m_FirstIterationTime.HasValue)
                m_FirstIterationTime = Time.realtimeSinceStartup;

            var routine = m_EnumeratorStack.Peek();
            var result = true;
            if (maxIterationTimeMS.HasValue)
            {
                var maxTime = maxIterationTimeMS * 0.001f * Stopwatch.Frequency;

                // Execute through as many yields as time permits
                do
                {
                    result &= routine.MoveNext();

                    if (!result)
                    {
                        if (m_EnumeratorStack.Count > 1)
                        {
                            // Nested coroutine ended
                            m_EnumeratorStack.Pop();
                            result = true;
                        }
                        else
                            break;
                    }
                    else
                    {
                        var current = Current;
                        if (current is YieldInstruction || current is CustomYieldInstruction)
                        {
                            // We have to leave these to Unity to resolve
                            break;
                        }

                        // Handle nesting
                        var enumerator = current as IEnumerator;
                        if (enumerator != null)
                        {
                            m_EnumeratorStack.Push(enumerator);
                        }
                    }

                    routine = m_EnumeratorStack.Peek();
                } while (sw.ElapsedTicks < maxTime);
            }
            else
            {
                result = routine.MoveNext();
            }

            sw.Stop();
            var endTime = Time.realtimeSinceStartup;

            if (!result && !m_FinalIterationTime.HasValue)
                m_FinalIterationTime = endTime;

            iterationExecutionTime = (float)sw.ElapsedTicks / Stopwatch.Frequency;
            selfExecutionTime += iterationExecutionTime;

            if (m_FinalIterationTime.HasValue)
                totalExecutionTime = m_FinalIterationTime.Value - m_FirstIterationTime.Value;
            else
                totalExecutionTime = endTime - m_FirstIterationTime.Value;

            if (logEnabled)
                Debug.LogFormat("Iteration: {0} / Self: {1} / Total: {2}", iterationExecutionTime, selfExecutionTime, totalExecutionTime);

            return result;
        }

        public void Reset()
        {
            while (m_EnumeratorStack.Count > 1)
                m_EnumeratorStack.Pop();

            m_EnumeratorStack.Peek().Reset();
            iterationExecutionTime = 0f;
            selfExecutionTime = 0f;
            totalExecutionTime = 0f;
            m_FirstIterationTime = null;
            m_FinalIterationTime = null;
        }

        public object Current
        {
            get { return m_EnumeratorStack.Count > 0 ? m_EnumeratorStack.Peek().Current : null; }
        }

        public TimedEnumerator(IEnumerator routine, float? maxIterationTimeMS = null)
        {
            m_EnumeratorStack.Push(routine);

            if (maxIterationTimeMS.HasValue)
                this.maxIterationTimeMS = maxIterationTimeMS;
        }
    }
}
