using System;
using System.Collections;
using System.Text;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Unity.AutoLOD
{
    interface IMeshGenerateLODJob : IJob, IDisposable
    {
        WorkingMesh InputMesh { set;  }
        WorkingMesh OutputMesh { get; set; }
        float Quality { set; }
    }

    interface IMeshLOD
    {
        Mesh InputMesh { set; }
        Mesh OutputMesh { get; set; }
        float Quality { set; }

        JobHandle Generate(NativeArray<JobHandle>? jobDependencies = null);
    }

    struct MeshGenerateLODJob<TSimplifier> : IMeshGenerateLODJob
        where TSimplifier : struct, IMeshSimplifier
    {
        public WorkingMesh InputMesh { get; set; }
        public WorkingMesh OutputMesh { get; set; }
        public float Quality { get; set; }

        public void Execute()
        {
            var outputMesh = OutputMesh;
            outputMesh.indexFormat = InputMesh.indexFormat;
            OutputMesh = outputMesh;
            var meshSimplifier = default(TSimplifier);
            meshSimplifier.Simplify(InputMesh, OutputMesh, Quality);
        }

        public void Dispose()
        {
            InputMesh.Dispose();
            OutputMesh.Dispose();
        }
    }

    struct MeshLOD<T> : IMeshLOD
        where T : struct, IMeshGenerateLODJob
    {
        public Mesh InputMesh { get; set; }
        public Mesh OutputMesh { get; set; }
        public float Quality { get; set; }

        IEnumerator UpdateMesh(JobHandle jobHandle, IMeshGenerateLODJob job)
        {
            while (!jobHandle.IsCompleted)
                yield return new WaitForSecondsRealtime(0.5f);

            jobHandle.Complete();

            var finalMesh = OutputMesh;
            var jobOutputMesh = job.OutputMesh;
            jobOutputMesh.name = finalMesh.name;
            jobOutputMesh.ApplyToMesh(OutputMesh);
            finalMesh.RecalculateBounds();

            job.Dispose();
        }

        public JobHandle Generate(NativeArray<JobHandle>? jobDependencies = null)
        {
            // A NOP to make sure we have an instance before launching into threads that may need to execute on the main thread
            MonoBehaviourHelper.ExecuteOnMainThread(() => { });

            var job = default(T);
            var inputMesh = InputMesh;
            job.InputMesh = inputMesh.ToWorkingMesh(Allocator.Persistent);
            job.Quality = Quality;
            var workingMesh = new WorkingMesh(Allocator.Persistent, inputMesh.vertexCount, inputMesh.GetTriangleCount(),
                inputMesh.subMeshCount, inputMesh.blendShapeCount);
            workingMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            job.OutputMesh = workingMesh;

            JobHandle jobHandle;
            if (jobDependencies.HasValue)
                jobHandle = job.Schedule(JobHandle.CombineDependencies(jobDependencies.Value));
            else
                jobHandle = job.Schedule();

            MonoBehaviourHelper.StartCoroutine(UpdateMesh(jobHandle, job));

            return jobHandle;
        }
    }

    static class MeshLOD
    {
        static Type GetGenericType(Type meshSimplifierType)
        {
            var genericJobType = typeof(MeshGenerateLODJob<>).MakeGenericType(meshSimplifierType);
            var genericType = typeof(MeshLOD<>).MakeGenericType(genericJobType);

            return genericType;
        }

        public static IMeshLOD GetGenericInstance(Type meshSimplifierType)
        {
            return (IMeshLOD)Activator.CreateInstance(GetGenericType(meshSimplifierType));
        }
    }
}
