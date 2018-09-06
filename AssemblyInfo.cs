using System.Runtime.CompilerServices;

// This is necessary to allow cross-communication between assemblies for classes in the Unity.AutoLOD
// namespace; The plan is that all code will eventually be within a single assembly.
#if UNITY_2017_3_OR_NEWER
[assembly: InternalsVisibleTo("AutoLOD-Editor")]
#else
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
#endif
