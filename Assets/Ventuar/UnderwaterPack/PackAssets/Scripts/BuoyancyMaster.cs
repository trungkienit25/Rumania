using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace LowPolyUnderwaterPack
{
    /// <summary>
    /// Low Poly Underwater Pack script that handles the computation of accurate water detection for child objects with the Buoyancy component.
    /// </summary>
    public class BuoyancyMaster : MonoBehaviour
    {
        [Tooltip("Enables use of accurate water detection in a specific range of the player. Can be heavy on performance at times.")]
        public bool useAccurateDetection = true;

        [Tooltip("Distance at which buoyant props will switch from approximate to exact water detection.")]
        public float accurateBuoyancyDist = 100;
        [Tooltip("Toggle to visualize objects which are using accurate water detection. Gizmos will only appear during runtime.")]
        public bool visualizeAccurateDetectionObjs = true;

        #region Private Fields

        private Buoyancy[] buoyantObjs;
        private List<WaterMesh> waters = new List<WaterMesh>();

        private Transform player;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            buoyantObjs = FindObjectsOfType<Buoyancy>();
            player = GameObject.FindGameObjectWithTag("MainCamera").transform;
        }

        private void Start() 
        {
            if (useAccurateDetection == false)
                return;

            // Get all waters in-use by buoyant objects if using accurate detection
            for (int i = 0; i < buoyantObjs.Length; i++)
            {
                WaterMesh currWater = buoyantObjs[i].water;

                if (waters.Contains(currWater) == false)
                {
                    waters.Add(currWater);
                }
            }
        }

        private void Update()
        {
            // If using accurate detection, there is a water object, and there are buoyant objects in the scene
            if (useAccurateDetection && waters.Count > 0 && buoyantObjs.Length > 0)
            {
                // Array of lists of all floating points in range of the player for each water object in the scene
                List<Vector3>[] validFloatPoints = new List<Vector3>[waters.Count];
                
                // Initialize each list
                for (int i = 0; i < validFloatPoints.Length; i++)
                {
                    validFloatPoints[i] = new List<Vector3>();
                }

                // Assign each in-range float point to validFloatPoints respective to their water object
                for (int i = 0; i < buoyantObjs.Length; i++)
                {
                    // Add floating points of this object if it's inside the player range threshold
                    if (Vector3.Distance(buoyantObjs[i].transform.position, player.position) < accurateBuoyancyDist && buoyantObjs[i].water != null)
                    {
                        buoyantObjs[i].inPlayerRange = true;

                        for (int j = 0; j < waters.Count; j++)
                        {
                            // Assign the in-range float points to validFloatPoints index corresponsing to which water object the buoyant object is using
                            if (buoyantObjs[i].water == waters[j])
                            {
                                for (int k = 0; k < buoyantObjs[i].buoyancyPoints.Count; k++)
                                {
                                    validFloatPoints[j].Add(buoyantObjs[i].transform.TransformPoint(buoyantObjs[i].buoyancyPoints[k]));
                                }
                            }
                        }
                    }
                    else
                    {
                        // Else mark it as not in range
                        buoyantObjs[i].inPlayerRange = false;
                    }
                }

                // Check if there are any float points that actually are in range
                bool validFloatPointsInRange = false;
                for (int i = 0; i < waters.Count; i++)
                {
                    if (validFloatPoints[i].Count > 0)
                    {
                        validFloatPointsInRange = true;
                        break;
                    }
                }

                // Assign accurate water points to valid float points in range
                if (validFloatPointsInRange)
                {
                    // 2D Vector3 array for all calculated water points relative to their respective water objects
                    Vector3[][] waterPoints = new Vector3[waters.Count][];
                    for (int i = 0; i < waters.Count; i++)
                    {
                        // Call GetWaterPoints() to retrieve accurate water points for points in the validFloatPoints array
                        if (validFloatPoints[i].Count > 0)
                            waterPoints[i] = waters[i].GetWaterPoints(validFloatPoints[i].ToArray());
                    }

                    // Count of all the water points that have been dealt with already for each water object
                    int[] count = new int[waters.Count];

                    // The list of water points to assign back to each individual buoyant object
                    List<Vector3> currentWaterPoints = new List<Vector3>();

                    // Loop through each buoyant object and set the respective water point for each of their floating points
                    for (int i = 0; i < buoyantObjs.Length; i++)
                    {
                        // If the object isn't in the player range, continue
                        if (!buoyantObjs[i].inPlayerRange)
                            continue;

                        // Clear the list of current water points
                        currentWaterPoints.Clear();

                        // Add the correct points to assign back to each buoyant object 
                        for (int j = 0; j < waters.Count; j++)
                        {
                            if (buoyantObjs[i].water == waters[j])
                            {
                                // Add the correctly indexed water points to currentWaterPoints using the count variable as an offset to know which points have already been assigned
                                for (int k = 0; k < buoyantObjs[i].buoyancyPoints.Count; k++)
                                {
                                    currentWaterPoints.Add(waterPoints[j][count[j] + k]);
                                }

                                // Update the count offset value
                                count[j] += buoyantObjs[i].buoyancyPoints.Count;
                            }
                        }

                        // Set the water points for the buoyant object
                        buoyantObjs[i].SetWaterPoints(currentWaterPoints.ToArray());
                    }
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Low Poly Underwater Pack custom editor which creates a custom inspector for BuoyancyMaster to organize properties and improve user experience.
    /// </summary>
#if UNITY_EDITOR
    [CustomEditor(typeof(BuoyancyMaster), true), CanEditMultipleObjects, System.Serializable]
    public class BuoyancyMaster_Editor : Editor
    {
        SerializedProperty useAccurateDetection, accurateBuoyancyDist, visualizeAccurateDetectionObjs;

        private bool buoyancyFoldout = true;

        private void OnEnable()
        {
            #region Seriealized Property Initialization

            useAccurateDetection = serializedObject.FindProperty("useAccurateDetection");
            accurateBuoyancyDist = serializedObject.FindProperty("accurateBuoyancyDist");
            visualizeAccurateDetectionObjs = serializedObject.FindProperty("visualizeAccurateDetectionObjs");

            #endregion
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Grayed out script property
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((BuoyancyMaster)target), typeof(BuoyancyMaster), false);
            GUI.enabled = true;

            #region Buoyancy Settings

            buoyancyFoldout = GUIHelper.Foldout(buoyancyFoldout, "Buoyancy Settings");

            if (buoyancyFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(useAccurateDetection);

                if (useAccurateDetection.boolValue)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(accurateBuoyancyDist);
                    EditorGUILayout.PropertyField(visualizeAccurateDetectionObjs);
                
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            #endregion

            // Draw the rest of the inspector excluding everything specifically drawn here
            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                EditorUtility.SetDirty(target);
        }
    }
#endif
}
