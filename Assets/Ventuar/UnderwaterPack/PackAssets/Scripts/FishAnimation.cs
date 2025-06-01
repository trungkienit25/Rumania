using UnityEngine;
using UnityEditor;

namespace LowPolyUnderwaterPack
{
    /// <summary>
    /// Low Poly Underwater Pack script that sends user-defined information set in the inspector to the Fish shader for fish animation.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Renderer))]
    public class FishAnimation : MonoBehaviour
    {
        #region General Settings

        public enum Axes
        {
            None, X, Y, Z
        }

        [Tooltip("Identifier for main distortion axis.")]
        [SerializeField] private Axes mainDistortAxis = Axes.Z;
        [Tooltip("Identifier for secondary distortion axis.")]
        [SerializeField] private Axes secondaryDistortAxis = Axes.Y;

        [Tooltip("Toggle to visualize main distortion wave gradient.")]
        [SerializeField] private bool visualizeMainWaveGradient = false;
        [Tooltip("Toggle to visualize secondary distortion wave gradient.")]
        [SerializeField] private bool visualizeSeconaryWaveGradient = false;

        #endregion

        #region Wave 1 Settings

        [Range(0.1f, 50), Tooltip("Length of main distortion wave.")]
        [SerializeField] private float waveLength1 = 6.5f;
        [Range(0, 3), Tooltip("Speed of main distortion wave.")]
        [SerializeField] private float waveSpeed1 = 2;
        [Range(0, 3), Tooltip("Amplitude of main distortion wave.")]
        [SerializeField] private float waveHeight1 = .2f;
        [Range(0, 1), Tooltip("The blending/feathering between where the main distortion wave starts and stops.")]
        [SerializeField] private float gradientBlending1 = 1;
        [Tooltip("Offset/displacement value of the main distortion gradient.")]
        [SerializeField] private float gradientOffset1 = 0;

        #endregion

        #region Wave 2 Settings

        [Range(0.1f, 50), Tooltip("Length of secondary distortion wave.")]
        [SerializeField] private float waveLength2 = 6.5f;
        [Range(0, 3), Tooltip("Speed of secondary distortion wave.")]
        [SerializeField] private float waveSpeed2 = 2;
        [Range(0, 3), Tooltip("Amplitude of secondary distortion wave.")]
        [SerializeField] private float waveHeight2 = .2f;
        [Range(0, 1), Tooltip("The blending/feathering between where the secondary distortion wave starts and stops.")]
        [SerializeField] private float gradientBlending2 = 1;
        [Tooltip("Offset/displacement value of the secondary distortion gradient.")]
        [SerializeField] private float gradientOffset2 = 0;

        #endregion

        #region Misc. Settings

        [Tooltip("Minimum and maximum clamp values for main and secondary distortion gradients.")]
        [SerializeField] private Vector2 clampMinMax = new Vector2(-.1f, 3);
        [Range(0, 100), Tooltip("Chance for the animation's properties to have greater variation from the above values in-game. The higher the value the greater the chance of variation.")]
        [SerializeField] private float randomizationPercent = 50;

        #endregion

        #region Private Fields

        private MaterialPropertyBlock propBlock;
        private Material fishMatAsset;
        private Renderer rend;

        // Adds randomness to select properties. Computed in Start
        private float random = 0;

        #endregion

        #region Unity Callbacks

        private void Awake() 
        {
            rend = GetComponent<Renderer>();
        }

        private void Start()
        {
            // Randomness constant
            random = Random.Range(-randomizationPercent / 100, randomizationPercent / 100);

            //Make sure material property block is updated on start
            UpdatePropBlock(random);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!rend)
                rend = GetComponent<Renderer>();

            // Update material property block to transfer data to shader whenever something is changed in the inspector
            UpdatePropBlock(random);
        }
#endif

        #endregion

        private void UpdatePropBlock(float rand)
        {
            // Initialize material property block
            propBlock = new MaterialPropertyBlock();
            rend.GetPropertyBlock(propBlock);

            #region Gradient visualization

            propBlock.SetFloat("_VisualizeGradient1", visualizeMainWaveGradient ? 1 : 0);
            propBlock.SetFloat("_VisualizeGradient2", visualizeSeconaryWaveGradient ? 1 : 0);

            #endregion

            #region Primary axis distortion properties
            
            propBlock.SetFloat("_WaveLength1", waveLength1 + (waveLength1 * rand));
            propBlock.SetFloat("_WaveSpeed1", waveSpeed1 + (waveSpeed1 * rand));
            propBlock.SetFloat("_WaveHeight1", waveHeight1 + (waveHeight1 * rand));
            propBlock.SetFloat("_GradBlending1", gradientBlending1);
            propBlock.SetFloat("_GradOffset1", gradientOffset1);

            #endregion

            #region Secondary axis distortion properties

            propBlock.SetFloat("_WaveLength2", waveLength2 + (waveLength2 * rand));
            propBlock.SetFloat("_WaveSpeed2", waveSpeed2 + (waveSpeed2 * rand));
            propBlock.SetFloat("_WaveHeight2", waveHeight2 + (waveHeight2 * rand));
            propBlock.SetFloat("_GradBlending2", gradientBlending2);
            propBlock.SetFloat("_GradOffset2", gradientOffset2);

            #endregion

            #region Distortion gradient clamping

            propBlock.SetFloat("_ClampMin", clampMinMax.x);
            propBlock.SetFloat("_ClampMax", clampMinMax.y);

            #endregion

            #region Set main distortion axis

            switch (mainDistortAxis)
            {
                case Axes.X:
                    propBlock.SetFloat("_AxisNum1", 1);
                    break;
                case Axes.Y:
                    propBlock.SetFloat("_AxisNum1", 2);
                    break;
                case Axes.Z:
                    propBlock.SetFloat("_AxisNum1", 3);
                    break;
                default:
                    propBlock.SetFloat("_AxisNum1", 0);
                    break;
            }

            #endregion

            #region Set secondary distortion axis

            switch (secondaryDistortAxis)
            {
                case Axes.X:
                    propBlock.SetFloat("_AxisNum2", 1);
                    break;
                case Axes.Y:
                    propBlock.SetFloat("_AxisNum2", 2);
                    break;
                case Axes.Z:
                    propBlock.SetFloat("_AxisNum2", 3);
                    break;
                default:
                    propBlock.SetFloat("_AxisNum2", 0);
                    break;
            }

            #endregion

            // Set material property block
            rend.SetPropertyBlock(propBlock);
        }
    }

    /// <summary>
    /// Low Poly Underwater Pack custom editor which creates a custom inspector for FishAnimation to organize properties and improve user experience.
    /// </summary>
#if UNITY_EDITOR
    [CustomEditor(typeof(FishAnimation), true), CanEditMultipleObjects, System.Serializable]
    public class FishAnimation_Editor : Editor
    {
        SerializedProperty mainDistortAxis, secondaryDistortAxis, visualizeMainWaveGradient, visualizeSeconaryWaveGradient, waveLength1, waveSpeed1, waveHeight1, gradientBlending1, gradientOffset1, waveLength2, waveSpeed2, waveHeight2, gradientBlending2, gradientOffset2,
            clampMinMax, randomizationPercent;

        private bool generalFoldout = true;
        private bool wave1Foldout = true;
        private bool wave2Foldout = true;
        private bool miscFoldout = true;

        private void OnEnable()
        {
            #region Seriealized Property Initialization

            mainDistortAxis = serializedObject.FindProperty("mainDistortAxis");
            secondaryDistortAxis = serializedObject.FindProperty("secondaryDistortAxis");

            visualizeMainWaveGradient = serializedObject.FindProperty("visualizeMainWaveGradient");
            visualizeSeconaryWaveGradient = serializedObject.FindProperty("visualizeSeconaryWaveGradient");

            waveLength1 = serializedObject.FindProperty("waveLength1");
            waveSpeed1 = serializedObject.FindProperty("waveSpeed1");
            waveHeight1 = serializedObject.FindProperty("waveHeight1");
            gradientBlending1 = serializedObject.FindProperty("gradientBlending1");
            gradientOffset1 = serializedObject.FindProperty("gradientOffset1");

            waveLength2 = serializedObject.FindProperty("waveLength2");
            waveSpeed2 = serializedObject.FindProperty("waveSpeed2");
            waveHeight2 = serializedObject.FindProperty("waveHeight2");
            gradientBlending2 = serializedObject.FindProperty("gradientBlending2");
            gradientOffset2 = serializedObject.FindProperty("gradientOffset2");

            clampMinMax = serializedObject.FindProperty("clampMinMax");
            randomizationPercent = serializedObject.FindProperty("randomizationPercent");

            #endregion
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Grayed out script property
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((FishAnimation)target), typeof(FishAnimation), false);
            GUI.enabled = true;

            #region General Settings

            generalFoldout = GUIHelper.Foldout(generalFoldout, "General Settings");

            if (generalFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(mainDistortAxis);
                EditorGUILayout.PropertyField(secondaryDistortAxis);

                GUILayout.Space(10);

                EditorGUILayout.PropertyField(visualizeMainWaveGradient);
                EditorGUILayout.PropertyField(visualizeSeconaryWaveGradient);

                EditorGUI.indentLevel--;
            }

            #endregion

            #region Wave 1 Settings

            wave1Foldout = GUIHelper.Foldout(generalFoldout, "Wave 1 Settings");

            if (wave1Foldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(waveLength1);
                EditorGUILayout.PropertyField(waveSpeed1);
                EditorGUILayout.PropertyField(waveHeight1);
                EditorGUILayout.PropertyField(gradientBlending1);
                EditorGUILayout.PropertyField(gradientOffset1);

                EditorGUI.indentLevel--;
            }

            #endregion

            #region Wave 2 Settings

            wave2Foldout = GUIHelper.Foldout(generalFoldout, "Wave 2 Settings");

            if (wave2Foldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(waveLength2);
                EditorGUILayout.PropertyField(waveSpeed2);
                EditorGUILayout.PropertyField(waveHeight2);
                EditorGUILayout.PropertyField(gradientBlending2);
                EditorGUILayout.PropertyField(gradientOffset2);

                EditorGUI.indentLevel--;
            }

            #endregion

            #region Misc. Settings

            miscFoldout = GUIHelper.Foldout(generalFoldout, "Misc. Settings");

            if (miscFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(clampMinMax);
                EditorGUILayout.PropertyField(randomizationPercent);

                EditorGUI.indentLevel--;
            }

            #endregion

            serializedObject.ApplyModifiedProperties();
            
            if (GUI.changed)
                EditorUtility.SetDirty(target);
        }
    }
#endif
}