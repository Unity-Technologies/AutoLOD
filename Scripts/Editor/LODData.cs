using UnityEngine;

namespace Unity.AutoLOD
{
    public class LODData : ScriptableObject
    {
        public const int MaxLOD = 7;

        public bool overrideDefaults;
        public LODImportSettings importSettings;
        public Renderer[] lod0;
        public Renderer[] lod1;
        public Renderer[] lod2;
        public Renderer[] lod3;
        public Renderer[] lod4;
        public Renderer[] lod5;
        public Renderer[] lod6;
        public Renderer[] lod7;

        public Renderer[] this[int index]
        {
            get
            {
                switch (index)
                {
                    default:
                        return lod0;
                    case 1:
                        return lod1;
                    case 2:
                        return lod2;
                    case 3:
                        return lod3;
                    case 4:
                        return lod4;
                    case 5:
                        return lod5;
                    case 6:
                        return lod6;
                    case 7:
                        return lod7;
                }
            }
            set
            {
                switch (index)
                {
                    default:
                        lod0 = value;
                        break;
                    case 1:
                        lod1 = value;
                        break;
                    case 2:
                        lod2 = value;
                        break;
                    case 3:
                        lod3 = value;
                        break;
                    case 4:
                        lod4 = value;
                        break;
                    case 5:
                        lod5 = value;
                        break;
                    case 6:
                        lod6 = value;
                        break;
                    case 7:
                        lod7 = value;
                        break;
                }
            }
        }
    }
}
