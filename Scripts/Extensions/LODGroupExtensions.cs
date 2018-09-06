using UnityEngine;

namespace Unity.AutoLOD
{
    public static class LODGroupExtensions
    {
        public static void SetEnabled(this LODGroup lodGroup, bool enabled)
        {
            if (lodGroup.enabled != enabled)
            {
                lodGroup.enabled = enabled;
                lodGroup.SetRenderersEnabled(enabled);
            }
        }

        public static void SetRenderersEnabled(this LODGroup lodGroup, bool enabled)
        {
            var lods = lodGroup.GetLODs();
            SetRenderersEnabled(lods, enabled);
        }

        public static void SetEnabled(this LODVolume.LODGroupHelper lodGroupHelper, bool enabled)
        {
            var lodGroup = lodGroupHelper.lodGroup;
            if (lodGroup.enabled != enabled)
            {
                lodGroup.enabled = enabled;
                SetRenderersEnabled(lodGroupHelper.lods, enabled);
            }
        }

        static void SetRenderersEnabled(LOD[] lods, bool enabled)
        {
            for (var i = 0; i < lods.Length; i++)
            {
                var lod = lods[i];

                var renderers = lod.renderers;
                foreach (var r in renderers)
                {
                    if (r)
                        r.enabled = enabled;
                }
            }
        }

        public static int GetMaxLOD(this LODGroup lodGroup)
        {
            return lodGroup.lodCount - 1;
        }

        public static int GetCurrentLOD(this LODGroup lodGroup, Camera camera = null)
        {
            var lods = lodGroup.GetLODs();
            var relativeHeight = lodGroup.GetRelativeHeight(camera ?? Camera.current);

            var lodIndex = GetCurrentLOD(lods, lodGroup.GetMaxLOD(), relativeHeight, camera);

            return lodIndex;
        }

        public static int GetMaxLOD(this LODVolume.LODGroupHelper lodGroupHelper)
        {
            return lodGroupHelper.maxLOD;
        }

        public static int GetCurrentLOD(this LODVolume.LODGroupHelper lodGroupHelper, Camera camera = null, Vector3? cameraPosition = null)
        {
            var lods = lodGroupHelper.lods;
            camera = camera ?? Camera.current;
            cameraPosition = cameraPosition ?? camera.transform.position;
            var relativeHeight = lodGroupHelper.GetRelativeHeight(camera, cameraPosition.Value);
            return GetCurrentLOD(lods, lodGroupHelper.GetMaxLOD(), relativeHeight, camera);
        }

        public static float GetWorldSpaceSize(this LODGroup lodGroup)
        {
            return GetWorldSpaceScale(lodGroup.transform) * lodGroup.size;
        }

        static int GetCurrentLOD(LOD[] lods, int maxLOD, float relativeHeight, Camera camera = null)
        {
            var lodIndex = maxLOD;

            for (var i = 0; i < lods.Length; i++)
            {
                var lod = lods[i];

                if (relativeHeight >= lod.screenRelativeTransitionHeight)
                {
                    lodIndex = i;
                    break;
                }
            }

            return lodIndex;
        }

        static float GetWorldSpaceScale(Transform t)
        {
            var scale = t.lossyScale;
            float largestAxis = Mathf.Abs(scale.x);
            largestAxis = Mathf.Max(largestAxis, Mathf.Abs(scale.y));
            largestAxis = Mathf.Max(largestAxis, Mathf.Abs(scale.z));
            return largestAxis;
        }

        static float DistanceToRelativeHeight(Camera camera, float distance, float size)
        {
            if (camera.orthographic)
                return size * 0.5F / camera.orthographicSize;

            var halfAngle = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5F);
            var relativeHeight = size * 0.5F / (distance * halfAngle);
            return relativeHeight;
        }

        static float GetRelativeHeight(this LODGroup lodGroup, Camera camera)
        {
            var distance = (lodGroup.transform.TransformPoint(lodGroup.localReferencePoint) - camera.transform.position).magnitude;
            return DistanceToRelativeHeight(camera, distance, lodGroup.GetWorldSpaceSize());
        }

        static float GetRelativeHeight(this LODVolume.LODGroupHelper lodGroupHelper, Camera camera, Vector3 cameraPosition)
        {
            var distance = (lodGroupHelper.referencePoint - cameraPosition).magnitude;
            return DistanceToRelativeHeight(camera, distance, lodGroupHelper.worldSpaceSize);
        }
    }
}