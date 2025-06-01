using UnityEngine;
using UnityEditor;

namespace LowPolyUnderwaterPack
{
    /// <summary>
    /// Low Poly Underwater Pack script that sends user-defined information set in the inspector to the PlantCoral shader for plant and coral animation.
    /// </summary>
    [ExecuteInEditMode]
    public class PlantCoralAnimation : MonoBehaviour
    {
        #region General Settings

        /**
         *  Custom coral property fields handled by PlantCoralAnimation_Editor
         */

        [Tooltip("Toggle to show properties for custom coral texturing.")]
        public bool showCoralProperties = false;
        [Tooltip("Offset value for the texture atlas, primarily intended to give varied coloring to corals.")]
        [HideInInspector] public Vector2Int materialTextureOffset = Vector2Int.zero;    // Hidden in inspector unless showCoralProperties is toggled

        [Tooltip("Toggle to visualize distortion wave gradient.")]
        [SerializeField] private bool visualizeWaveGradient = false;

        #endregion

        #region Animation Settings

        [Range(0.1f, 10), Tooltip("Length of distortion wave.")]
        [SerializeField] private float waveLength = 4;
        [Range(0, 1), Tooltip("Speed of distortion wave.")]
        [SerializeField] private float waveSpeed = 0.2f;
        [Range(0, .2f), Tooltip("Amplitude of distortion wave.")]
        [SerializeField] private float waveHeight = 0.045f;
        [Range(0, 1), Tooltip("The blending/feathering between where the distortion wave starts and stops.")]
        [SerializeField] private float gradientBlending = 1;
        [Tooltip("Offset/displacement value of the distortion gradient.")]
        [SerializeField] private float gradientOffset = 0;

        [Tooltip("Minimum and maximum clamp values for distortion gradient.")]
        [SerializeField] private Vector2 clampMinMax = new Vector2(-.1f, 3);
        [Range(0, 100), Tooltip("Chance for the animation's properties to have greater variation from the above values in-game. The higher the value the greater the chance of variation.")]
        [SerializeField] private float randomizationPercent = 30;

        #endregion

        #region Private Fields

        // Number of tiles on the UnderwaterPackAtlas.png texture
        private const int ATLAS_SIZE = 6;

        private MaterialPropertyBlock propBlock;
        private Material plantCoralMatAsset;
        private Renderer rend;

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
        public void OnValidate()
        {
            if (!rend)
                rend = GetComponent<Renderer>();

            // Update material property block to transfer data to shader whenever something is changed in the inspector
            UpdatePropBlock(random);
        }
#endif

        #endregion

        #region Texturing

        public void RandomizeTextureOffset()
        {
            // Shift the material texture offset randomly between 0 and the number of tiles on the atlax texture. Max value is exclusive, hence + 1
            materialTextureOffset = new Vector2Int(Random.Range(0, ATLAS_SIZE + 1), Random.Range(0, ATLAS_SIZE + 1));
        }

        private void UpdatePropBlock(float rand)
        {
            // Initialize material property block
            propBlock = new MaterialPropertyBlock();
            rend.GetPropertyBlock(propBlock);

            // Wave gradient visualization
            propBlock.SetFloat("_VisualizeGradient", visualizeWaveGradient ? 1 : 0);

            #region Custom Coral Property Texturing

            if (showCoralProperties)
            {
                // Set texture offset if using coral properties
                float factor = (1f / (float)(ATLAS_SIZE + 1));
                propBlock.SetVector("_MainTex_ST", new Vector4(1, 1, materialTextureOffset.x * factor, materialTextureOffset.y * factor));
            }
            else
            {
                // Revert to default texture offset if not using coral properties
                propBlock.SetVector("_MainTex_ST", new Vector4(1, 1, 0, 0));
            }

            #endregion

            #region Wave distortion properties
            
            propBlock.SetFloat("_WaveLength", waveLength + (waveLength * rand));
            propBlock.SetFloat("_WaveSpeed", waveSpeed + (waveSpeed * rand));
            propBlock.SetFloat("_WaveHeight", waveHeight + (waveHeight * rand));
            propBlock.SetFloat("_ClampMin", clampMinMax.x);
            propBlock.SetFloat("_ClampMax", clampMinMax.y);
            propBlock.SetFloat("_GradBlending", gradientBlending);
            propBlock.SetFloat("_GradOffset", gradientOffset);

            #endregion

            // Set material property block
            rend.SetPropertyBlock(propBlock);
        }

        #endregion
    }

    /// <summary>
    /// Low Poly Underwater Pack custom editor which creates a custom inspector for PlantCoralAnimation to organize properties and improve user experience.
    /// </summary>
#if UNITY_EDITOR
    [CustomEditor(typeof(PlantCoralAnimation)), CanEditMultipleObjects, System.Serializable]
    public class PlantCoralAnimation_Editor : Editor
    {
        private SerializedProperty showCoralProperties, materialTextureOffset, visualizeWaveGradient, waveLength, waveSpeed, waveHeight, gradientBlending, gradientOffset, 
            clampMinMax, randomizationPercent;

        private bool generalFoldout = true;
        private bool animationFoldout = true;

        private void OnEnable()
        {
            #region Serialized Property Initialization

            showCoralProperties = serializedObject.FindProperty("showCoralProperties");
            materialTextureOffset = serializedObject.FindProperty("materialTextureOffset");

            visualizeWaveGradient = serializedObject.FindProperty("visualizeWaveGradient");
            waveLength = serializedObject.FindProperty("waveLength");
            waveSpeed = serializedObject.FindProperty("waveSpeed");
            waveHeight = serializedObject.FindProperty("waveHeight");
            gradientBlending = serializedObject.FindProperty("gradientBlending");
            gradientOffset = serializedObject.FindProperty("gradientOffset");
            clampMinMax = serializedObject.FindProperty("clampMinMax");
            randomizationPercent = serializedObject.FindProperty("randomizationPercent");
            waveHeight = serializedObject.FindProperty("waveHeight");
            gradientBlending = serializedObject.FindProperty("gradientBlending");
            gradientOffset = serializedObject.FindProperty("gradientOffset");

            clampMinMax = serializedObject.FindProperty("clampMinMax");
            randomizationPercent = serializedObject.FindProperty("randomizationPercent");

            #endregion
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Grayed out script property
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((PlantCoralAnimation)target), typeof(PlantCoralAnimation), false);
            GUI.enabled = true;

            #region General Settings

            generalFoldout = GUIHelper.Foldout(generalFoldout, "General Settings");

            if (generalFoldout)
            {
                EditorGUI.indentLevel++;

                // Draw custom coral properties toggle
                showCoralProperties.boolValue = EditorGUILayout.Toggle("Custom Coral Properties", showCoralProperties.boolValue);

                if (showCoralProperties.boolValue == true)
                {
                    // If custom coral properties has been toggled, draw custom properties
                    materialTextureOffset.vector2IntValue = EditorGUILayout.Vector2IntField("Material Texture Offset", materialTextureOffset.vector2IntValue);
                    if (GUILayout.Button("Randomize Texture Offset"))
                    {
                        for (int i = 0; i < targets.Length; i++)
                        {
                            // Find our selected object call RandomizeTextureOffset() if the Randomize Texture Offset button has been pressed
                            PlantCoralAnimation obj = (PlantCoralAnimation)targets[i];
                            obj.RandomizeTextureOffset();
                        }

                    }
                }

                GUILayout.Space(10);

                EditorGUILayout.PropertyField(visualizeWaveGradient);

                EditorGUI.indentLevel--;
            }

            #endregion

            #region Animation Settings

            animationFoldout = GUIHelper.Foldout(animationFoldout, "Animation Settings");

            if (animationFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(waveLength);
                EditorGUILayout.PropertyField(waveSpeed);
                EditorGUILayout.PropertyField(waveHeight);
                EditorGUILayout.PropertyField(gradientBlending);
                EditorGUILayout.PropertyField(gradientOffset);

                GUILayout.Space(10);

                EditorGUILayout.PropertyField(clampMinMax);
                EditorGUILayout.PropertyField(randomizationPercent); 

                EditorGUI.indentLevel--;
            }

            #endregion

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    // Call OnValidate if anything has changed
                    PlantCoralAnimation obj = (PlantCoralAnimation)targets[i];
                    obj.OnValidate();
                }

                EditorUtility.SetDirty(target);
            }
        }
    }
#endif
}
