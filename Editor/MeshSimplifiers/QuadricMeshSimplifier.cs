#if ENABLE_UNITYMESHSIMPLIFIER
using System;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;
using Mesh = Unity.AutoLOD.WorkingMesh;
#endif

#if ENABLE_UNITYMESHSIMPLIFIER
namespace Unity.AutoLOD
{
    public struct QuadricMeshSimplifier : IMeshSimplifier, IPreferences
    {
        [InitializeOnLoad]
        class Preferences : ScriptableSingleton<Preferences>
        {
            const string k_Options = "AutoLOD.QuadricMeshSimplifier.Options";

            public static SimplificationOptions Options;

            // Needed for SerializedObject/SerializedProperty
            [SerializeField]
            internal SimplificationOptions m_Options;

            // Load the options statically, so they can be used in jobs
            static Preferences()
            {
                EditorApplication.delayCall += () =>
                {
                    var preferences = Preferences.instance;
                    Debug.Assert(!preferences.Equals(default), "QuadricMeshSimplifier preferences should never be the default struct");
                };
            }

            void OnEnable()
            {
                // It's okay that it doesn't save, but don't hide it otherwise the inspector GUI won't work
                hideFlags = HideFlags.DontSave;

                var savedPrefs = EditorPrefs.GetString(k_Options, null);
                if (string.IsNullOrEmpty(savedPrefs))
                    m_Options = SimplificationOptions.Default;
                else
                    m_Options = JsonUtility.FromJson<SimplificationOptions>(savedPrefs);

                // Update the static version
                Options = m_Options;
            }

            public void ResetToDefaults()
            {
                m_Options = SimplificationOptions.Default;
                Save();
            }

            public void Save()
            {
                var savedPrefs = JsonUtility.ToJson(m_Options);
                EditorPrefs.SetString(k_Options, savedPrefs);
                Options = m_Options;
            }
        }

        SerializedObject m_SerializedObject;

        public void Simplify(Mesh inputMesh, Mesh outputMesh, float quality)
        {
            var meshSimplifier = new MeshSimplifier();
            meshSimplifier.SimplificationOptions = Preferences.Options;
            meshSimplifier.Vertices = inputMesh.vertices;
            meshSimplifier.Normals = inputMesh.normals;
            meshSimplifier.Tangents = inputMesh.tangents;
            meshSimplifier.UV1 = inputMesh.uv;
            meshSimplifier.UV2 = inputMesh.uv2;
            meshSimplifier.UV3 = inputMesh.uv3;
            meshSimplifier.UV4 = inputMesh.uv4;
            meshSimplifier.Colors = inputMesh.colors;

            var triangles = new int[inputMesh.subMeshCount][];
            for (var submesh = 0; submesh < inputMesh.subMeshCount; submesh++)
            {
                triangles[submesh] = inputMesh.GetTriangles(submesh);
            }
            meshSimplifier.AddSubMeshTriangles(triangles);

            meshSimplifier.SimplifyMesh(quality);

            outputMesh.vertices = meshSimplifier.Vertices;
            outputMesh.normals = meshSimplifier.Normals;
            outputMesh.tangents = meshSimplifier.Tangents;
            outputMesh.uv = meshSimplifier.UV1;
            outputMesh.uv2 = meshSimplifier.UV2;
            outputMesh.uv3 = meshSimplifier.UV3;
            outputMesh.uv4 = meshSimplifier.UV4;
            outputMesh.colors = meshSimplifier.Colors;
            outputMesh.subMeshCount = meshSimplifier.SubMeshCount;
            for (var submesh = 0; submesh < outputMesh.subMeshCount; submesh++)
            {
                outputMesh.SetTriangles(meshSimplifier.GetSubMeshTriangles(submesh), submesh);
            }
        }

        public void OnPreferencesGUI()
        {
            var preferences = Preferences.instance;
            if (m_SerializedObject == null)
                m_SerializedObject = new SerializedObject(preferences);

            m_SerializedObject.Update();
            var property = m_SerializedObject.FindProperty("m_Options");

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(property);
            if (property.isExpanded)
            {
                GUI.enabled = !preferences.m_Options.Equals(SimplificationOptions.Default);
                if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    preferences.ResetToDefaults();
                    EditorGUIUtility.ExitGUI();
                }
                GUI.enabled = true;
            }
            EditorGUILayout.EndHorizontal();
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                property.NextVisible(true);
                do
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(property.name, GUILayout.Width(200f));
                    EditorGUILayout.PropertyField(property, GUIContent.none, true, GUILayout.ExpandWidth(false));
                    EditorGUILayout.EndHorizontal();
                } while (property.NextVisible(false));
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck())
            {
                m_SerializedObject.ApplyModifiedProperties();
                preferences.Save();
            }
        }
    }
}
#endif