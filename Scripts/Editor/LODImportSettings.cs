using System;

namespace UnityEditor.Experimental.AutoLOD
{
    [Serializable]
    public class LODImportSettings
    {
        public bool generateOnImport;
        public string meshSimplifier;
        public string batcher;
        public int maxLODGenerated;
        public int initialLODMaxPolyCount;
    }
}
