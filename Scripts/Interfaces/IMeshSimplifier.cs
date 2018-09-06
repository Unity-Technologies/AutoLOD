namespace Unity.AutoLOD
{
    public interface IMeshSimplifier
    {
        /// <summary>
        /// Simplify an input mesh to quality threshold (NOTE: Runs on a separate thread)
        /// </summary>
        /// <param name="inputMesh">A working copy of the input mesh</param>
        /// <param name="outputMesh">Where the output mesh should be stored</param>
        /// <param name="quality">Percentage of quality requested in relation to the input mesh</param>
        void Simplify(WorkingMesh inputMesh, WorkingMesh outputMesh, float quality);
    }
}
