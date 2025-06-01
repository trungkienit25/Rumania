using UnityEditor;
using UnityEngine;
using Unity.Netcode.Components;

namespace NetcodePlus.Editor
{
    /// <summary>
    /// The <see cref="CustomEditor"/> for <see cref="SNetworkTransform"/>
    /// </summary>
    [CustomEditor(typeof(SNetworkTransform), true)]
    public class SNetworkTransformEditor : UnityEditor.Editor
    {
        private SerializedProperty m_SyncPositionXProperty;
        private SerializedProperty m_SyncPositionYProperty;
        private SerializedProperty m_SyncPositionZProperty;
        private SerializedProperty m_SyncRotationXProperty;
        private SerializedProperty m_SyncRotationYProperty;
        private SerializedProperty m_SyncRotationZProperty;
        private SerializedProperty m_SyncScaleXProperty;
        private SerializedProperty m_SyncScaleYProperty;
        private SerializedProperty m_SyncScaleZProperty;
        private SerializedProperty m_PositionThresholdProperty;
        private SerializedProperty m_RotAngleThresholdProperty;
        private SerializedProperty m_ScaleThresholdProperty;
        private SerializedProperty m_InLocalSpaceProperty;
        private SerializedProperty m_InterpolateProperty;
        private SerializedProperty m_AlwaysSync;

        private static int s_ToggleOffset = 45;
        private static float s_MaxRowWidth = EditorGUIUtility.labelWidth + EditorGUIUtility.fieldWidth + 5;
        private static GUIContent s_PositionLabel = EditorGUIUtility.TrTextContent("Position");
        private static GUIContent s_RotationLabel = EditorGUIUtility.TrTextContent("Rotation");
        private static GUIContent s_ScaleLabel = EditorGUIUtility.TrTextContent("Scale");

        /// <inheritdoc/>
        public void OnEnable()
        {
            m_SyncPositionXProperty = serializedObject.FindProperty(nameof(SNetworkTransform.SyncPositionX));
            m_SyncPositionYProperty = serializedObject.FindProperty(nameof(SNetworkTransform.SyncPositionY));
            m_SyncPositionZProperty = serializedObject.FindProperty(nameof(SNetworkTransform.SyncPositionZ));
            m_SyncRotationXProperty = serializedObject.FindProperty(nameof(SNetworkTransform.SyncRotAngleX));
            m_SyncRotationYProperty = serializedObject.FindProperty(nameof(SNetworkTransform.SyncRotAngleY));
            m_SyncRotationZProperty = serializedObject.FindProperty(nameof(SNetworkTransform.SyncRotAngleZ));
            m_SyncScaleXProperty = serializedObject.FindProperty(nameof(SNetworkTransform.SyncScaleX));
            m_SyncScaleYProperty = serializedObject.FindProperty(nameof(SNetworkTransform.SyncScaleY));
            m_SyncScaleZProperty = serializedObject.FindProperty(nameof(SNetworkTransform.SyncScaleZ));
            m_PositionThresholdProperty = serializedObject.FindProperty(nameof(SNetworkTransform.PositionThreshold));
            m_RotAngleThresholdProperty = serializedObject.FindProperty(nameof(SNetworkTransform.RotAngleThreshold));
            m_ScaleThresholdProperty = serializedObject.FindProperty(nameof(SNetworkTransform.ScaleThreshold));
            m_InLocalSpaceProperty = serializedObject.FindProperty(nameof(SNetworkTransform.InLocalSpace));
            m_InterpolateProperty = serializedObject.FindProperty(nameof(SNetworkTransform.Interpolate));
            m_AlwaysSync = serializedObject.FindProperty(nameof(SNetworkTransform.AlwaysSync));
        }

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Syncing", EditorStyles.boldLabel);
            {
                GUILayout.BeginHorizontal();

                var rect = GUILayoutUtility.GetRect(EditorGUIUtility.fieldWidth, s_MaxRowWidth, EditorGUIUtility.singleLineHeight, EditorGUIUtility.singleLineHeight, EditorStyles.numberField);
                var ctid = GUIUtility.GetControlID(7231, FocusType.Keyboard, rect);

                rect = EditorGUI.PrefixLabel(rect, ctid, s_PositionLabel);
                rect.width = s_ToggleOffset;

                m_SyncPositionXProperty.boolValue = EditorGUI.ToggleLeft(rect, "X", m_SyncPositionXProperty.boolValue);
                rect.x += s_ToggleOffset;
                m_SyncPositionYProperty.boolValue = EditorGUI.ToggleLeft(rect, "Y", m_SyncPositionYProperty.boolValue);
                rect.x += s_ToggleOffset;
                m_SyncPositionZProperty.boolValue = EditorGUI.ToggleLeft(rect, "Z", m_SyncPositionZProperty.boolValue);

                GUILayout.EndHorizontal();
            }
            {
                GUILayout.BeginHorizontal();

                var rect = GUILayoutUtility.GetRect(EditorGUIUtility.fieldWidth, s_MaxRowWidth, EditorGUIUtility.singleLineHeight, EditorGUIUtility.singleLineHeight, EditorStyles.numberField);
                var ctid = GUIUtility.GetControlID(7231, FocusType.Keyboard, rect);

                rect = EditorGUI.PrefixLabel(rect, ctid, s_RotationLabel);
                rect.width = s_ToggleOffset;

                m_SyncRotationXProperty.boolValue = EditorGUI.ToggleLeft(rect, "X", m_SyncRotationXProperty.boolValue);
                rect.x += s_ToggleOffset;
                m_SyncRotationYProperty.boolValue = EditorGUI.ToggleLeft(rect, "Y", m_SyncRotationYProperty.boolValue);
                rect.x += s_ToggleOffset;
                m_SyncRotationZProperty.boolValue = EditorGUI.ToggleLeft(rect, "Z", m_SyncRotationZProperty.boolValue);

                GUILayout.EndHorizontal();
            }
            {
                GUILayout.BeginHorizontal();

                var rect = GUILayoutUtility.GetRect(EditorGUIUtility.fieldWidth, s_MaxRowWidth, EditorGUIUtility.singleLineHeight, EditorGUIUtility.singleLineHeight, EditorStyles.numberField);
                var ctid = GUIUtility.GetControlID(7231, FocusType.Keyboard, rect);

                rect = EditorGUI.PrefixLabel(rect, ctid, s_ScaleLabel);
                rect.width = s_ToggleOffset;

                m_SyncScaleXProperty.boolValue = EditorGUI.ToggleLeft(rect, "X", m_SyncScaleXProperty.boolValue);
                rect.x += s_ToggleOffset;
                m_SyncScaleYProperty.boolValue = EditorGUI.ToggleLeft(rect, "Y", m_SyncScaleYProperty.boolValue);
                rect.x += s_ToggleOffset;
                m_SyncScaleZProperty.boolValue = EditorGUI.ToggleLeft(rect, "Z", m_SyncScaleZProperty.boolValue);

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Thresholds", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_PositionThresholdProperty);
            EditorGUILayout.PropertyField(m_RotAngleThresholdProperty);
            EditorGUILayout.PropertyField(m_ScaleThresholdProperty);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configurations", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_InLocalSpaceProperty);
            EditorGUILayout.PropertyField(m_InterpolateProperty);
            EditorGUILayout.PropertyField(m_AlwaysSync);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
