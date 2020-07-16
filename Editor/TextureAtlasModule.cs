using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.AutoLOD
{
    public class TextureAtlasModule : ScriptableSingleton<TextureAtlasModule>
    {
        List<TextureAtlas> m_Atlases = new List<TextureAtlas>();

        void OnEnable()
        {
            m_Atlases.Clear();

            var atlases = Resources.FindObjectsOfTypeAll<TextureAtlas>();
            m_Atlases.AddRange(atlases);
        }

        static void SaveUniqueAtlasAsset(UnityObject asset)
        {
            var directory = "Assets/AutoLOD/Generated/Atlases/";
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var path = directory + Path.GetRandomFileName();
            path = Path.ChangeExtension(path, "asset");
            AssetDatabase.CreateAsset(asset, path);
        }

        public IEnumerator GetTextureAtlas(Texture2D[] textures, Action<TextureAtlas> callback)
        {
            TextureAtlas atlas = null;

            // Clear out any atlases that were removed
            m_Atlases.RemoveAll(a => a == null);
            yield return null;

            foreach (var a in m_Atlases)
            {
                // At a minimum the atlas should have all of the textures requested, but can be a superset
                if (!textures.Except(a.textures).Any())
                {
                    atlas = a;
                    break;
                }

                yield return null;
            }

            if (!atlas)
            {
                atlas = ScriptableObject.CreateInstance<TextureAtlas>();

                foreach (var t in textures)
                {
                    var assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t));
                    var textureImporter = assetImporter as TextureImporter;
                    if (textureImporter && !textureImporter.isReadable)
                    {
                        textureImporter.isReadable = true;
                        textureImporter.SaveAndReimport();
                    }
                    else if (!assetImporter)
                    {
                        // In-memory textures need to be saved to disk in order to be referenced by the texture atlas
                        SaveUniqueAtlasAsset(t);
                    }
                    yield return null;
                }

                var textureAtlas = new Texture2D(0, 0, TextureFormat.RGB24, true, PlayerSettings.colorSpace == ColorSpace.Linear);
                var uvs = textureAtlas.PackTextures(textures.ToArray(), 0, 1024, true);
                
                if (uvs != null)
                {
                    atlas.textureAtlas = textureAtlas;
                    atlas.uvs = uvs;
                    atlas.textures = textures;

                    SaveUniqueAtlasAsset(textureAtlas);
                    SaveUniqueAtlasAsset(atlas);
                    
                    m_Atlases.Add(atlas);
                }

                yield return null;
            }

            if (callback != null)
                callback(atlas);
        }
    }
}
