using System;
using UnityEditor;
using UnityEngine;
using CustomLocalization4EditorExtension;

namespace net.nekobako.BlinkSuppressor.Editor
{
    using Runtime;

    [CustomEditor(typeof(BlinkSuppressor))]
    internal class BlinkSuppressorEditor : UnityEditor.Editor
    {
        private static readonly Lazy<GUIStyle> s_FrameStyle = new(() => new("FrameBox"));

        private SerializedProperty m_SuppressBlinkProperty = null;
        private SerializedProperty m_BlendShapeThresholdProperty = null;

        private void OnEnable()
        {
            m_SuppressBlinkProperty = serializedObject.FindProperty(nameof(BlinkSuppressor.SuppressBlink));
            m_BlendShapeThresholdProperty = serializedObject.FindProperty(nameof(BlinkSuppressor.BlendShapeThreshold));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            CL4EE.DrawLanguagePicker();

            EditorGUILayout.BeginVertical(s_FrameStyle.Value);

            EditorGUILayout.LabelField(CL4EE.Tr("property-to-animate"), EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(m_SuppressBlinkProperty, new(CL4EE.Tr("suppress-blink")), true);

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(s_FrameStyle.Value);

            EditorGUILayout.LabelField(CL4EE.Tr("setting"), EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(m_BlendShapeThresholdProperty, new(CL4EE.Tr("blend-shape-threshold")), true);

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
