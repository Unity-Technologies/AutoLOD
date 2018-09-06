using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.AutoLOD
{
    /// <summary>
    /// A simple batcher that combines textures into an atlas and meshes (non material-preserving)
    /// </summary>
    class SimpleBatcher : IBatcher
    {
        Texture2D whiteTexture
        {
            get
            {
                if (!m_WhiteTexture)
                {
                    var path = "Assets/AutoLOD/Generated/Atlases/white.asset";
                    m_WhiteTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (!m_WhiteTexture)
                    {
                        m_WhiteTexture = Object.Instantiate(Texture2D.whiteTexture);
                        var directory = Path.GetDirectoryName(path);
                        if (!Directory.Exists(directory))
                            Directory.CreateDirectory(directory);

                        AssetDatabase.CreateAsset(m_WhiteTexture, path);
                    }
                }

                return m_WhiteTexture;
            }
        }

        Texture2D m_WhiteTexture;

        public IEnumerator Batch(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var materials = new HashSet<Material>(renderers.SelectMany(r => r.sharedMaterials));

            //foreach (var material in materials) { Debug.Log(material); }
            var textures = new HashSet<Texture2D>(materials.Select(m =>
            {
                if (m)
                    return m.mainTexture as Texture2D;

                return null;
            }).Where(t => t != null)).ToList();
            textures.Add(whiteTexture);

            TextureAtlas atlas = null;
            yield return TextureAtlasModule.instance.GetTextureAtlas(textures.ToArray(), a => atlas = a);

            var atlasLookup = new Dictionary<Texture2D, Rect>();
            var atlasTextures = atlas.textures;
            for (int i = 0; i < atlasTextures.Length; i++)
            {
                atlasLookup[atlasTextures[i]] = atlas.uvs[i];
            }

            MeshFilter[] meshFilters = go.GetComponentsInChildren<MeshFilter>();
            var combine = new List<CombineInstance>();
            for (int i = 0; i < meshFilters.Length; i++)
            {
                var mf = meshFilters[i];
                var sharedMesh = mf.sharedMesh;

                if (!sharedMesh)
                    continue;

                if (!sharedMesh.isReadable)
                {
                    var assetPath = AssetDatabase.GetAssetPath(sharedMesh);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                        if (importer)
                        {
                            importer.isReadable = true;
                            importer.SaveAndReimport();
                        }
                    }
                }

                var ci = new CombineInstance();

                var mesh = Object.Instantiate(sharedMesh);

                var mr = mf.GetComponent<MeshRenderer>();
                var sharedMaterials = mr.sharedMaterials;
                var uv = mesh.uv;
                var colors = mesh.colors;
                if (colors == null || colors.Length == 0)
                    colors = new Color[uv.Length];
                var updated = new bool[uv.Length];
                var triangles = new List<int>();

                // Some meshes have submeshes that either aren't expected to render or are missing a material, so go ahead and skip
                var subMeshCount = Mathf.Min(mesh.subMeshCount, sharedMaterials.Length);
                for (int j = 0; j < subMeshCount; j++)
                {
                    var sharedMaterial = sharedMaterials[Mathf.Min(j, sharedMaterials.Length - 1)];
                    var mainTexture = whiteTexture;
                    var materialColor = Color.white;

                    if (sharedMaterial)
                    {
                        var texture = sharedMaterial.mainTexture as Texture2D;
                        if (texture)
                            mainTexture = texture;

                        if (sharedMaterial.HasProperty("_Color"))
                            materialColor = sharedMaterial.color;
                    }

                    if (mesh.GetTopology(j) != MeshTopology.Triangles)
                    {
                        Debug.LogWarning("Mesh must have triangles", mf);
                        continue;
                    }

                    triangles.Clear();
                    mesh.GetTriangles(triangles, j);
                    var uvOffset = atlasLookup[mainTexture];
                    foreach (var t in triangles)
                    {
                        if (!updated[t])
                        {
                            var uvCoord = uv[t];
                            if (mainTexture == whiteTexture)
                            {
                                // Sample at center of white texture to avoid sampling edge colors incorrectly
                                uvCoord.x = 0.5f;
                                uvCoord.y = 0.5f;
                            }

                            uvCoord.x = Mathf.Lerp(uvOffset.xMin, uvOffset.xMax, uvCoord.x);
                            uvCoord.y = Mathf.Lerp(uvOffset.yMin, uvOffset.yMax, uvCoord.y);
                            uv[t] = uvCoord;

                            if (mainTexture == whiteTexture)
                                colors[t] = materialColor;
                            else
                                colors[t] = Color.white;

                            updated[t] = true;
                        }
                    }

                    yield return null;
                }
                mesh.uv = uv;
                mesh.uv2 = null;
                mesh.colors = colors;

                ci.mesh = mesh;
                ci.transform = mf.transform.localToWorldMatrix;
                combine.Add(ci);

                mf.gameObject.SetActive(false);

                yield return null;
            }

            var combinedMesh = new Mesh();
#if UNITY_2017_3_OR_NEWER
            combinedMesh.indexFormat = IndexFormat.UInt32;
#endif
            combinedMesh.CombineMeshes(combine.ToArray(), true, true);
            combinedMesh.RecalculateBounds();
            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = combinedMesh;

            for (int i = 0; i < meshFilters.Length; i++)
            {
                Object.DestroyImmediate(meshFilters[i].gameObject);
            }

            var meshRenderer = go.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Custom/AutoLOD/SimpleBatcher"));
            material.mainTexture = atlas.textureAtlas;
            meshRenderer.sharedMaterial = material;
        }
    }
}
