using Unity.AutoLOD.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AutoLOD
{
    public class GridPlacementUtility : EditorWindow
    {
        PrimitiveType m_PrimitiveType = PrimitiveType.Cube;
        Vector3 m_Number = Vector3.one;
        Vector3 m_Spacing = Vector3.one;
        GameObject m_Prefab;
        Material m_Material;
        bool m_UniqueMaterials;

        [MenuItem("GameObject/AutoLOD/Grid Placement Utility")]
        static void Init()
        {
            EditorWindow.GetWindow<GridPlacementUtility>(true, "Grid Placement Utility").Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            m_Prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", m_Prefab, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                var bounds = ObjectUtils.GetBounds(m_Prefab.transform);
                m_Spacing = bounds.size;
            }

            if (!m_Prefab)
            {
                GUILayout.Label("OR");
                m_PrimitiveType = (PrimitiveType)EditorGUILayout.EnumPopup("Primitive", m_PrimitiveType);
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();


            m_Number = EditorGUILayout.Vector3Field("Number", m_Number);
            m_Spacing = EditorGUILayout.Vector3Field("Spacing", m_Spacing);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            m_UniqueMaterials = EditorGUILayout.Toggle("Unique Materials", m_UniqueMaterials);
            m_Material = (Material)EditorGUILayout.ObjectField("Material", m_Material, typeof(Material), true);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (GUILayout.Button("Create"))
            {
                GameObject parent = new GameObject("Primitives");
                for (int i = 0; i < Mathf.FloorToInt(m_Number.x); i++)
                {
                    for (int j = 0; j < Mathf.FloorToInt(m_Number.y); j++)
                    {
                        for (int k = 0; k < Mathf.FloorToInt(m_Number.z); k++)
                        {
                            GameObject go = m_Prefab ? (GameObject)PrefabUtility.InstantiatePrefab(m_Prefab) : GameObject.CreatePrimitive(m_PrimitiveType);
                            go.name = string.Format("{0} {1}_{2}_{3}", m_Prefab ? m_Prefab.name : m_PrimitiveType.ToString(), i, j, k);
                            go.transform.position = Vector3.right * (m_Spacing.x * i + i) + Vector3.up * (m_Spacing.y * j + j) + Vector3.forward * (m_Spacing.z * k + k);
                            go.transform.parent = parent.transform;

                            var meshRenderers = go.GetComponentsInChildren<MeshRenderer>();
                            foreach (var mr in meshRenderers)
                            {
                                var sharedMaterials = mr.sharedMaterials;
                                for (int m = 0; m < sharedMaterials.Length; m++)
                                {
                                    var material = m_Material ? m_Material : sharedMaterials[m];
                                    sharedMaterials[m] = m_UniqueMaterials ? Instantiate(material) : material;
                                }
                                mr.sharedMaterials = sharedMaterials;
                            }
                        }
                    }
                }
                Close();
            }
        }
    }
}