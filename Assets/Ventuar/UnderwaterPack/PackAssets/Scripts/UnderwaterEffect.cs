using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace LowPolyUnderwaterPack
{
    /// <summary>
    /// Low Poly Underwater Pack script that applies camera, lighting, and scene effects when the camera is underwater.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class UnderwaterEffect : MonoBehaviour
    {
        [Tooltip("Position offset for detecting whether the camera is above or under the water.")]
        [SerializeField] private Vector3 waterDetectionOffset = Vector3.zero;

        #region Effect Settings

        [Tooltip("Post processing profile for when the camera is underwater.")]
        [SerializeField] private VolumeProfile underwaterProfile = null;
        [Tooltip("Post processing profile for when the camera is above water.")]
        [SerializeField] private VolumeProfile surfaceProfile = null;
        [Tooltip("The color of the underwater fog.")]
        [SerializeField] private Color fogColor = Color.blue;
        [Tooltip("Toggle whether to change the tint color of the skybox to the color of the fog when underwater to better blend into the environment.")]
        [SerializeField] private bool modifySkyboxTint = false;

        [Tooltip("Water refraction renderer feature, used to create a refraction image effect.")]
        public ScriptableRendererFeature refractionRendererFeature;

        #endregion

        #region Hidden Fields

        [HideInInspector] public bool isUnderwater = false;
        [HideInInspector] public Vector3[] waterVerts;

        #endregion

        #region Private Fields

        private Material skybox;
        private Color skyColor;

        private WaterMesh water;
        private Volume vol;
        private Camera cam;

        private string tintPropName;
        private Vector3 waterMeshPoint = Vector3.zero;

        #region Boids Test Scene Variables
        
        private GameObject boidsRoom; 
        private bool inBoidsRoomScene;

        #endregion

        #endregion

        #region Unity Callbacks

        private void Awake() 
        {
            vol = FindObjectOfType<Volume>();
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            // Ensure camera generates a depth texture
            cam.depthTextureMode = DepthTextureMode.Depth;

            // If not in boids test room
            if (SceneManager.GetActiveScene().name != "BoidsTest")
            {
                water = FindObjectOfType<WaterMesh>();
                skybox = RenderSettings.skybox;
                if (RenderSettings.skybox.HasProperty("_Tint"))
                    tintPropName = "_Tint";
                if (RenderSettings.skybox.HasProperty("_SkyTint"))
                    tintPropName = "_SkyTint";
                
                if (modifySkyboxTint)
                    skyColor = skybox.GetColor(tintPropName);

                return;
            }
            
            // Only find the boids test room if in the boids test scene
            inBoidsRoomScene = true;
            boidsRoom = GameObject.Find("BoidTestRoom");
        }

        private void OnValidate()
        {
            if (cam == null)
                cam = GetComponent<Camera>();

            // Change underwater fog and background color in real time
            RenderSettings.fogColor = fogColor;
            cam.backgroundColor = fogColor;
            
            if (refractionRendererFeature != null)
                refractionRendererFeature.SetActive(false);
        }

        private void LateUpdate()
        {
            // Special logic if we are in the boids test room
            if (inBoidsRoomScene)
            {
                // If in the boids test scene but no boids room exists, do nothing and return
                if (boidsRoom == null)
                    return;

                // Camera is underwater if it is inside the walls of the test room
                bool insideBoidsRoom = boidsRoom.GetComponent<MeshCollider>().bounds.Contains(transform.position);
                ApplyUnderwaterEffects(insideBoidsRoom);

                return;
            }

            // Water detection position can be offsetted for a optimally smooth transition between above and below water
            Vector3 pos = transform.position + waterDetectionOffset;
            if (water != null)
                waterMeshPoint = water.GetWaterPoint(pos);
            //Debug.Log("waterY:" + waterMeshPoint.y);
            //Debug.Log("posy:" + pos.y);
            // If the point on the water is above our position, we are underwater and should be applying underwater effects
            ApplyUnderwaterEffects((waterMeshPoint != Vector3.zero) ? waterMeshPoint.y > 10f : false);
        }

        private void OnDrawGizmos()
        {
            // Draw green sphere gizmo where our offsetted water detection point is
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.position + waterDetectionOffset, .25f);
        }

        private void OnApplicationQuit()
        {
            // Reset underwater effects when quitting or going back into edit mode
            ApplyUnderwaterEffects(false);
        }

        #endregion

        private void ApplyUnderwaterEffects(bool underwater)
        {
            // Underwater effects
            isUnderwater = underwater;
            RenderSettings.fog = underwater;
            RenderSettings.skybox.SetColor("_Tint", ((!underwater && modifySkyboxTint) ? skyColor : fogColor));
            vol.profile = ((underwater) ? underwaterProfile : surfaceProfile);
            refractionRendererFeature.SetActive(underwater);
        }
    }

    /// <summary>
    /// Low Poly Underwater Pack custom editor which creates a custom inspector for UnderwaterEffect to organize properties and improve user experience.
    /// </summary>
#if UNITY_EDITOR
    [CustomEditor(typeof(UnderwaterEffect), true), CanEditMultipleObjects, System.Serializable]
    public class UnderwaterEffect_Editor : Editor
    {
        private SerializedProperty waterDetectionOffset, surfaceProfile, underwaterProfile, fogColor, modifySkyboxTint, refractionRendererFeature;

        private bool effectFoldout = true;

        private void OnEnable()
        {
            #region Seriealized Property Initialization

            waterDetectionOffset = serializedObject.FindProperty("waterDetectionOffset");
            
            surfaceProfile = serializedObject.FindProperty("surfaceProfile");
            underwaterProfile = serializedObject.FindProperty("underwaterProfile");
            fogColor = serializedObject.FindProperty("fogColor");
            modifySkyboxTint = serializedObject.FindProperty("modifySkyboxTint");

            refractionRendererFeature = serializedObject.FindProperty("refractionRendererFeature");

            #endregion
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Grayed out script property
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((UnderwaterEffect)target), typeof(UnderwaterEffect), false);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(waterDetectionOffset);

            EditorGUILayout.Space(10);

            #region Effect Settings

            effectFoldout = GUIHelper.Foldout(effectFoldout, "Effect Settings");

            if (effectFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(surfaceProfile);
                EditorGUILayout.PropertyField(underwaterProfile);
                EditorGUILayout.PropertyField(fogColor);
                EditorGUILayout.PropertyField(modifySkyboxTint);

                EditorGUILayout.Space(10);

                EditorGUILayout.PropertyField(refractionRendererFeature);

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