using UnityEditor;
using Unity.AutoLOD;
using UnityEngine;

namespace Unity.AutoLOD
{
    [CustomEditor(typeof(LODData))]
    [CanEditMultipleObjects]
    public class LODDataEditor : Editor
    {
        public static int maxLODGenerated { set; get; }
        public static int initialLODMaxPolyCount { set; get; }
        public static string meshSimplifier;
        public static string batcher;

        SerializedProperty m_OverrideDefaults;
        SerializedProperty m_ImportSettings;
        SerializedProperty[] m_LODs = new SerializedProperty[LODData.MaxLOD + 1];

        void OnEnable()
        {
            m_OverrideDefaults = serializedObject.FindProperty("overrideDefaults");
            m_ImportSettings = serializedObject.FindProperty("importSettings");
            for (int i = 0; i < m_LODs.Length; i++)
            {
                m_LODs[i] = serializedObject.FindProperty("lod" + i);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            var settingsOverridden = m_OverrideDefaults.boolValue;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_OverrideDefaults, new GUIContent("Override Defaults"));
            if (EditorGUI.EndChangeCheck() && m_OverrideDefaults.boolValue)
            {
                m_ImportSettings.FindPropertyRelative("generateOnImport").boolValue = true;
                m_ImportSettings.FindPropertyRelative("meshSimplifier").stringValue = meshSimplifier;
                m_ImportSettings.FindPropertyRelative("batcher").stringValue = batcher;
                m_ImportSettings.FindPropertyRelative("maxLODGenerated").intValue = maxLODGenerated;
                m_ImportSettings.FindPropertyRelative("initialLODMaxPolyCount").intValue = initialLODMaxPolyCount;
            }

            if (settingsOverridden)
            {
                EditorGUILayout.PropertyField(m_ImportSettings, new GUIContent("Import Settings"), true);
            }

            EditorGUI.BeginDisabledGroup(!settingsOverridden || m_ImportSettings.FindPropertyRelative("generateOnImport").boolValue);
            for (int i = 0; i < m_LODs.Length; i++)
            {
                var lod = m_LODs[i];
                if (lod.arraySize > 0)
                {
                    EditorGUI.BeginDisabledGroup(i == 0);
                    EditorGUILayout.PropertyField(lod, new GUIContent("LOD" + i), true);
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    if (settingsOverridden)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginDisabledGroup(i == 1);
                        if (GUILayout.Button("Remove LOD"))
                            m_LODs[i - 1].ClearArray();
                        EditorGUI.EndDisabledGroup();
                        if (GUILayout.Button("Add LOD"))
                            lod.InsertArrayElementAtIndex(0);
                        EditorGUILayout.EndHorizontal();
                    }
                    break;
                }
            }
            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();
        }
    }
}
