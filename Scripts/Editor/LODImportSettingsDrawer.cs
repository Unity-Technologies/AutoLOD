using System;
using System.Linq;
using Unity.AutoLOD.Utilities;
using UnityEditor;
using UnityEngine;

namespace Unity.AutoLOD
{
    [CustomPropertyDrawer(typeof(LODImportSettings))]
    public class LODImportSettingsDrawer : PropertyDrawer
    {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            EditorGUILayout.PrefixLabel(label);

            // Draw fields - passs GUIContent.none to each so they are drawn without labels
            var generateOnImportProperty = property.FindPropertyRelative("generateOnImport");
            EditorGUILayout.PropertyField(generateOnImportProperty, new GUIContent("Generate on Import"));

            if (generateOnImportProperty.boolValue)
            {
                PropertyTypeField<IMeshSimplifier>(property.FindPropertyRelative("meshSimplifier"));
                PropertyTypeField<IBatcher>(property.FindPropertyRelative("batcher"));

                var maxLODProperty = property.FindPropertyRelative("maxLODGenerated");
                var maxLODValues = Enumerable.Range(0, LODData.MaxLOD + 1).ToArray();
                EditorGUI.BeginChangeCheck();
                int maxLODGenerated = EditorGUILayout.IntPopup("Maximum LOD Generated", maxLODProperty.intValue, maxLODValues.Select(v => v.ToString()).ToArray(), maxLODValues);
                if (EditorGUI.EndChangeCheck())
                    maxLODProperty.intValue = maxLODGenerated;

                var initialLODMaxPolyCountProperty = property.FindPropertyRelative("initialLODMaxPolyCount");
                EditorGUI.BeginChangeCheck();
                var maxPolyCount = EditorGUILayout.IntField("Initial LOD Max Poly Count", initialLODMaxPolyCountProperty.intValue);
                if (EditorGUI.EndChangeCheck())
                    initialLODMaxPolyCountProperty.intValue = maxPolyCount;
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUI.EndProperty();
        }

        void PropertyTypeField<T>(SerializedProperty property)
        {
            var implementations = ObjectUtils.GetImplementationsOfInterface(typeof(T)).ToList();
            var type = Type.GetType(property.stringValue);
            if (type == null && implementations.Count > 0)
                type = Type.GetType(implementations[0].AssemblyQualifiedName);

            var displayedOptions = implementations.Select(t => t.Name).ToArray();
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUILayout.Popup(property.displayName, Array.IndexOf(displayedOptions, type.Name), displayedOptions);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = implementations[selected].AssemblyQualifiedName;
        }
    }
}