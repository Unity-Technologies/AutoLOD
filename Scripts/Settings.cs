using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Experimental.AutoLOD
{
    public static class Settings
    {
        const string k_ShowVolumeBounds = "AutoLOD.ShowVolumeBounds";

        public static bool ShowVolumeBounds
        {
            set
            {
                EditorPrefs.SetBool(k_ShowVolumeBounds, value);
            }
            get
            {
                return EditorPrefs.GetBool(k_ShowVolumeBounds, false);
            }
        }


    }

}