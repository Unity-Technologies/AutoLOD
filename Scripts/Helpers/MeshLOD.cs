using System;
using System.Collections;
using System.Text;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Unity.AutoLOD
{
    struct MeshLOD
    {
        public Mesh inputMesh;
        public Mesh outputMesh;
        public float quality;
        public Type meshSimplifierType;

        struct GenerateMeshLODJob : IJob
        {
            public NativeArray<byte> meshSimplifierTypeName;
            public WorkingMesh inputMesh;
            public WorkingMesh outputMesh;
            public float quality;

            public void Execute()
            {
                outputMesh.indexFormat = inputMesh.indexFormat;
                var typeName = Encoding.UTF8.GetString(meshSimplifierTypeName.ToArray());
                var meshSimplifierType = Type.GetType(typeName);
                var meshSimplifier = (IMeshSimplifier)Activator.CreateInstance(meshSimplifierType);
                meshSimplifier.Simplify(inputMesh, outputMesh, quality);
            }

            public void Dispose()
            {
                meshSimplifierTypeName.Dispose();
                inputMesh.Dispose();
                outputMesh.Dispose();
            }
        }

        IEnumerator UpdateMesh(JobHandle jobHandle, GenerateMeshLODJob job)
        {
            while (!jobHandle.IsCompleted)
                yield return new WaitForSecondsRealtime(0.5f);

            jobHandle.Complete();

            var finalMesh = outputMesh;
            var jobOutputMesh = job.outputMesh;
            jobOutputMesh.name = finalMesh.name;
            jobOutputMesh.ApplyToMesh(outputMesh);
            finalMesh.RecalculateBounds();

            job.Dispose();
        }

        public JobHandle Generate(NativeArray<JobHandle>? jobDependencies = null)
        {
            // A NOP to make sure we have an instance before launching into threads that may need to execute on the main thread
            MonoBehaviourHelper.ExecuteOnMainThread(() => { });

            var job = new GenerateMeshLODJob();
            job.inputMesh = inputMesh.ToWorkingMesh(Allocator.TempJob);
            job.quality = quality;
            var typeNameBytes = Encoding.UTF8.GetBytes(meshSimplifierType.AssemblyQualifiedName);
            job.meshSimplifierTypeName = new NativeArray<byte>(typeNameBytes, Allocator.TempJob);
            job.outputMesh = new WorkingMesh(Allocator.Persistent, inputMesh.vertexCount, inputMesh.GetTriangleCount(),
                inputMesh.subMeshCount, inputMesh.blendShapeCount);

            JobHandle jobHandle;
            if (jobDependencies.HasValue)
                jobHandle = job.Schedule(JobHandle.CombineDependencies(jobDependencies.Value));
            else
                jobHandle = job.Schedule();

            MonoBehaviourHelper.StartCoroutine(UpdateMesh(jobHandle, job));

            return jobHandle;
        }
    }
}
