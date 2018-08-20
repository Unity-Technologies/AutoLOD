using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Experimental.AutoLOD
{
    public class SceneLODUpdater : MonoBehaviour
    {
        private LODVolume m_RootLODVolume;

        private int s_InstanceCount = 0;

        void Awake()
        {
            s_InstanceCount += 1;

            if (s_InstanceCount > 1)
            {
                Debug.LogWarning("Instance of SceneLODUpdater must be one.");
            }
        }

        void OnDestroy()
        {
            s_InstanceCount -= 1;
        }

        void Start()
        {
            m_RootLODVolume = FindRootLODVolume();
        }

        void OnEnable()
        {
            Camera.onPreCull += OnPreCull;
        }

        void OnDisable()
        {
            Camera.onPreCull -= OnPreCull;
        }

        private void OnPreCull(Camera cam)
        {
            var cameraTransform = cam.transform;
            var cameraPosition = cameraTransform.position;

            if (m_RootLODVolume != null)
            {
                m_RootLODVolume.UpdateLODGroup(cam, cameraPosition, false);
            }
        }

        private LODVolume FindRootLODVolume()
        {
            LODVolume[] volumes = FindObjectsOfType<LODVolume>();

            foreach (LODVolume volume in volumes)
            {
                if (volume.transform.parent == null)
                    return volume;
            }

            return null;
        }
        
    }

}