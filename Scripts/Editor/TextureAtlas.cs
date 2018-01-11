using UnityEngine;

namespace UnityEditor.Experimental.AutoLOD
{
    [CreateAssetMenu]
    public class TextureAtlas : ScriptableObject
    {
        public Texture2D textureAtlas;
        public Texture2D[] textures;
        public Rect[] uvs;
    }
}
