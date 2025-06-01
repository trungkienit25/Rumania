using UnityEngine;
using UnityEditor;

namespace LowPolyUnderwaterPack
{
    /// <summary>
    /// Low Poly Underwater Pack script that handles behavior for single non-flocking fish
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SingleFishAI : MonoBehaviour
    {
        #region General Settings
        
        public enum SwimType
        {
            Bobbing, Straight
        }

        // Determines whether the fish will swim in a straight line or bob up and down.
        public SwimType swimType = SwimType.Straight;

        /**
         *  Hidden public fields are handled by the SingleFishAI_Editor class
         */

        // Speed at which the fish bobs up and down.
        [HideInInspector] public float bobSpeed = 1;

        // The height which the fish will bob with.
        [HideInInspector] public float bobHeight = 1;

        // Toggle to have the fish face the direction he bobs in.
        [HideInInspector] public bool faceBobDirection = true;

        [Tooltip("Toggle to visualize the view rays of the fish.")]
        [SerializeField] private bool visualizeViewRays = false;
        [Tooltip("Toggle to visualize the obstacle avoidance ray of the fish. Points forward if there is nothing to avoid.")]
        [SerializeField] private bool visualizeAvoidanceRay = false;
        [Tooltip("Toggle to visualize the detection radius of the fish.")]
        [SerializeField] private bool visualizeDetectionRadius = false;
        [Tooltip("Toggle to visualize the bounds radius of the fish.")]
        [SerializeField] private bool visualizeBoundsRadius = false;

        #endregion

        #region Detection Settings

        [Range(1, 3), Tooltip("Lower raycasting quality values increase performance but increase chance of obstacle detection error, and vice versa (determines whether to use raycasting or spherecasting)")]
        [SerializeField] protected int detectionQuality = 2;
        [Range(0, 360), Tooltip("The angle which the fish can see. Max value of 360 to see in all directions.")]
        [SerializeField] protected float viewAngle = 200;
        [Tooltip("Fraction at which the view rays are seperated from each other. Decreasing gives more accurate vision but worse performance, and vice versa.")]
        [SerializeField] protected float turnFraction = 12;
        [Tooltip("The general accuracy/density of the fish's view rays. Increasing gives more accurate vision but worse performance, and vice versa.")]
        [SerializeField] protected int accuracy = 8;

        #endregion

        #region Movement Settings

        [Tooltip("The maximum velocity at which the fish travels at.")]
        [SerializeField] protected float maxVelocity = 5;
        [Tooltip("The radius which the fish can detect other fish and obstacles.")]
        public float detectionRadius = 15;
        [Tooltip("The radius of the fish itself. Used in spherecasting to detect how wide of a gap the fish can fit through.")]
        public float boundsRadius = 2;

        [Tooltip("The force with which the fish avoids obstacles. Decreasing too much has a chance of fish clipping and/or traveling through colliders.")]
        [SerializeField] protected float obstacleAvoidanceForce = 1;

        #endregion

        #region Hidden/Private Fields

        // The smoothing factor applied to obstacle avoidance direction calculations
        private const float AVOIDANCE_DIR_SMOOTHING = .35f;

        [HideInInspector] public Vector3 velocity;
        [HideInInspector] public Collider boundsCollider;
        [HideInInspector] public Vector3 avoidancePoint;
        
        #endregion

        protected Vector3 avoidanceDir, visRay;
        protected int fishMask;

        #region Unity Callbacks

        protected virtual void Start()
        {
            velocity = transform.forward * maxVelocity;
            avoidanceDir = transform.forward;

            fishMask = ~(1 << LayerMask.NameToLayer("Fish"));
            gameObject.layer = LayerMask.NameToLayer("Fish");
        }

        protected virtual void OnValidate()
        {
            turnFraction = Mathf.Clamp(turnFraction, 0.001f, 360);
            accuracy = (int)Mathf.Clamp(accuracy, 1, Mathf.Infinity);
        }

        protected virtual void Update()
        {
            UpdateVelocity();

            // Look towards the direction of travel
            if (velocity != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(velocity);

            if (swimType == SwimType.Bobbing)
            {
                // Y-axis bobbing calculations
                Vector3 bob = Vector3.zero;
                bob.y -= bobHeight * Mathf.Cos((bobSpeed * Mathf.PI) * Time.timeSinceLevelLoad) / 100;
                transform.position += bob;
                if (faceBobDirection)
                {
                    // Rotation calculations to face bob directon
                    float f = bobHeight * Mathf.Cos((bobSpeed * Mathf.PI) * Time.timeSinceLevelLoad) / 10;
                    transform.eulerAngles += new Vector3(f, 0, 0);
                }
            }

            transform.position += velocity * Time.deltaTime;
        }

        protected void OnDrawGizmos()
        {
            if (visualizeAvoidanceRay)
            {
                Gizmos.color = Color.green;

                Vector3 d = avoidanceDir != Vector3.zero ? avoidanceDir : transform.forward;
                Gizmos.DrawRay(transform.position, d * 10);
                Gizmos.DrawSphere(transform.position + (d * 10), .35f);
            }

            Gizmos.color = Color.red;

            // Same calculations as CalculateObstacleRay for view ray visualization
            if (visualizeViewRays)
            {
                int stepModifier = 0;
                int i = 0;
                do
                {
                    stepModifier += ((i % accuracy == 0) ? 1 : 0);

                    float a = 2 * Mathf.PI * i / accuracy;
                    float c = turnFraction * stepModifier;

                    Vector3 q = Quaternion.Euler(
                        Mathf.Sin(a) * c,
                        Mathf.Cos(a) * c,
                        0) * Vector3.forward * 7.5f;

                    q = Quaternion.AngleAxis(transform.eulerAngles.z, transform.forward) * q;
                    q = Quaternion.AngleAxis(transform.eulerAngles.x, transform.right) * q;
                    q = Quaternion.AngleAxis(transform.eulerAngles.y, transform.up) * q;

                    Gizmos.DrawRay(transform.position, q);
                    i++;
                } while ((i * turnFraction * 2) / accuracy <= viewAngle);
            }

            // Detection radius visualization
            if (visualizeDetectionRadius)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, detectionRadius);
            }

            // Bounds radius visualization
            if (visualizeBoundsRadius)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, boundsRadius);
            }
        }

        #endregion

        #region Velocity Calculation

        protected virtual void UpdateVelocity()
        {
            velocity += transform.forward;

            DetectCollision(IsHeadingForCollision());

            velocity = Vector3.ClampMagnitude(velocity, maxVelocity);
        }

        #endregion

        #region Collision Detection

        protected virtual void DetectCollision(bool headingForCollision)
        {
            bool headingForBounds = IsHeadingForBounds();

            if (headingForCollision || headingForBounds)
            {
                // If heading for bounds, call overloaded CalculateObstacleRay for bounds collider
                // If avoidance dir does not exist or a new one is needed, caluclate new ray
                Vector3 newDir = headingForBounds ? CalculateObstacleRay(boundsCollider) : CalculateObstacleRay();
                
                // Apply smoothing to the obstacle avoidance direction
                avoidanceDir = Vector3.Lerp(avoidanceDir, newDir, AVOIDANCE_DIR_SMOOTHING);

                // Have a stronger avoidance force as you get closer to the avoidance point
                velocity += Vector3.Lerp(Vector3.zero, avoidanceDir.normalized * maxVelocity, 1 - Mathf.Clamp01(Vector3.Distance(transform.position, avoidancePoint) / detectionRadius)) * obstacleAvoidanceForce;

                return;
            }

            if (avoidanceDir == null)
            {
                avoidanceDir = transform.forward;
                return;
            }

            avoidanceDir = Vector3.Lerp(avoidanceDir, transform.forward, AVOIDANCE_DIR_SMOOTHING);
        }

        protected bool IsHeadingForCollision()
        {
            RaycastHit hit;

            if (detectionQuality == 1)
            {
                // Detection quality 1 uses raycasting
                if (Physics.Raycast(transform.position, transform.forward, out hit, detectionRadius, fishMask))
                {
                    avoidancePoint = hit.point;
                    return true;
                }
            }
            else
            {
                // Detection quality 2 and 3 uses spherecasting
                if (Physics.SphereCast(transform.position, boundsRadius, transform.forward, out hit, detectionRadius, fishMask))
                {
                    avoidancePoint = hit.point;
                    return true;
                }
            }

            return false;
        }

        protected bool IsHeadingForBounds()
        {
            if (boundsCollider != null)
            {
                RaycastHit hit;

                // Reversed ray to cast the ray from outside the collider
                Ray ray = new Ray(transform.position + (transform.forward * detectionRadius), -transform.forward);

                // Raycast only the bounds collider to check for hit
                // Collider casting only allows for raycasting, so no spherecasting for greater detection qualities
                if (boundsCollider.Raycast(ray, out hit, detectionRadius))
                {
                    avoidancePoint = hit.point;
                    return true;
                }
            }

            return false;
        }

        protected Vector3 CalculateObstacleRay()
        {
            RaycastHit hit;

            // Preliminary check to see if avoidanceDir actualy needs to be recalculated
            if (detectionQuality <= 2)
            {
                // Detection quality 1 and 2 uses raycasting
                if (!Physics.Raycast(transform.position, avoidanceDir, out hit, detectionRadius, fishMask))
                {
                    return avoidanceDir;
                }
            }
            else
            {
                // Detection quality 3 uses spherecasting
                if (!Physics.SphereCast(transform.position, boundsRadius, avoidanceDir, out hit, detectionRadius, fishMask))
                {
                    return avoidanceDir;
                }
            }
            
            // View rays will be stored here, and the first one that does not hit an object will be returned
            Ray ray = new Ray();

            // The next best viable ray, the one with the largest distance to the hit, will be stored here
            Ray nextBestViableRay = new Ray();

            Vector3 visRayDir;

            int stepModifier = 0;
            int i = 0;
            float furthestHitDistance = 0;

            // View ray calculation using parametric trigonometry
            do
            {
                stepModifier += ((i % accuracy == 0) ? 1 : 0);

                float a = 2 * Mathf.PI * i / accuracy;
                float c = turnFraction * stepModifier;

                visRayDir = Quaternion.Euler(Mathf.Sin(a) * c, Mathf.Cos(a) * c, 0) * Vector3.forward;

                // Rotate view rays with the fish rotation
                visRayDir = Quaternion.AngleAxis(transform.eulerAngles.z, transform.forward) * visRayDir;
                visRayDir = Quaternion.AngleAxis(transform.eulerAngles.x, transform.right) * visRayDir;
                visRayDir = Quaternion.AngleAxis(transform.eulerAngles.y, transform.up) * visRayDir;
                visRayDir = visRayDir.normalized;

                ray = new Ray(transform.position, visRayDir);

                if (detectionQuality <= 2)
                {
                    // Detection quality 1 and 2 uses raycasting
                    if (!Physics.Raycast(ray, out hit, detectionRadius, fishMask))
                    {
                        return ray.direction;
                    }
                    else
                    {
                        float hitDist = Vector3.Distance(transform.position, hit.point);

                        // Set the next best viable ray if the hit distance is larger than the furthest stored
                        if (hitDist > furthestHitDistance)
                        {
                            nextBestViableRay = ray;
                            furthestHitDistance = hitDist;
                        }
                    }
                }
                else
                {
                    // Detection quality 3 uses spherecasting
                    if (!Physics.SphereCast(ray, boundsRadius, out hit, detectionRadius, fishMask))
                    {
                        return ray.direction;
                    }
                    else
                    {
                        float hitDist = Vector3.Distance(transform.position, hit.point);

                        // Set the next best viable ray if the hit distance is larger than the furthest stored
                        if (hitDist > furthestHitDistance)
                        {
                            nextBestViableRay = ray;
                            furthestHitDistance = hitDist;
                        }
                    }
                }

                i++;
            } while ((i * turnFraction * 2) / accuracy <= viewAngle);

            // Just return last calculated ray if no valid avoidance ray exists
            return nextBestViableRay.direction;
        }

        // Overloaded CalculateObstacleRay with functionality for spawner bounds detection
        protected Vector3 CalculateObstacleRay(Collider col)
        {
            RaycastHit hit;
            
            // The regular ray used to "see" objects
            Ray regRay = new Ray();

            // The ray used to cast against the bounds collider from the outside in
            Ray colRay = new Ray();

            // The next best viable ray, the one with the largest distance to the hit, will be stored here
            Ray nextBestViableRay = new Ray();

            int stepModifier = 0;
            int i = 0;
            float furthestHitDistance = 0;

            // View ray calculation using parametric trigonometry
            do
            {
                stepModifier += ((i % accuracy == 0) ? 1 : 0);

                float a = 2 * Mathf.PI * i / accuracy;
                float c = turnFraction * stepModifier;

                Vector3 q = Quaternion.Euler(
                    Mathf.Sin(a) * c,
                    Mathf.Cos(a) * c,
                    0) * Vector3.forward * 10;

                // Rotate view rays with the fish rotation
                q = Quaternion.AngleAxis(transform.eulerAngles.z, transform.forward) * q;
                q = Quaternion.AngleAxis(transform.eulerAngles.x, transform.right) * q;
                q = Quaternion.AngleAxis(transform.eulerAngles.y, transform.up) * q;
                q = q.normalized;

                regRay = new Ray(transform.position, q);
                colRay = new Ray(transform.position + (q * detectionRadius), -q);

                if (detectionQuality <= 2)
                {
                    // Detection quality 1 and 2 uses raycasting
                    if (!Physics.Raycast(regRay, out hit, detectionRadius, fishMask) && !col.Raycast(colRay, out hit, detectionRadius))
                    {
                        return regRay.direction;
                    }
                    else
                    {
                        float hitDist = Vector3.Distance(transform.position, hit.point);

                        // Set the next best viable ray if the hit distance is larger than the furthest stored
                        if (hitDist > furthestHitDistance)
                        {
                            nextBestViableRay = regRay;
                            furthestHitDistance = hitDist;
                        }
                    }
                }
                else
                {
                    // Detection quality 3 uses spherecasting
                    if (!Physics.SphereCast(regRay, boundsRadius, out hit, detectionRadius, fishMask) && !col.Raycast(colRay, out hit, detectionRadius))
                    {
                        return regRay.direction;
                    }
                    else
                    {
                        float hitDist = Vector3.Distance(transform.position, hit.point);

                        // Set the next best viable ray if the hit distance is larger than the furthest stored
                        if (hitDist > furthestHitDistance)
                        {
                            nextBestViableRay = regRay;
                            furthestHitDistance = hitDist;
                        }
                    }
                }

                i++;
            } while ((i * turnFraction * 2) / accuracy <= viewAngle);

            // Just return last calculated ray if no valid avoidance ray exists
            return nextBestViableRay.direction;
        }

        #endregion
    }

    /// <summary>
    /// Low Poly Underwater Pack custom editor which creates a custom inspector for SingleFishAI to organize properties and improve user experience.
    /// </summary>
#if UNITY_EDITOR
    [CustomEditor(typeof(SingleFishAI), true), CanEditMultipleObjects, System.Serializable]
    public class SingleFishAI_Editor : Editor
    {
        protected SerializedProperty swimType, bobSpeed, bobHeight, faceBobDirection, velocity, visualizeViewRays, visualizeAvoidanceRay, visualizeDetectionRadius, visualizeBoundsRadius,
            detectionQuality, viewAngle, turnFraction, accuracy, maxVelocity, detectionRadius, boundsRadius, obstacleAvoidanceForce;

        protected bool generalFoldout = true;
        protected bool detectionFoldout = true;
        protected bool movementFoldout = true;

        protected virtual void OnEnable()
        {
            #region Serialized Property Initialization

            swimType = serializedObject.FindProperty("swimType");
            bobSpeed = serializedObject.FindProperty("bobSpeed");
            bobHeight = serializedObject.FindProperty("bobHeight");
            faceBobDirection = serializedObject.FindProperty("faceBobDirection");
            velocity = serializedObject.FindProperty("velocity");
            visualizeViewRays = serializedObject.FindProperty("visualizeViewRays");
            visualizeAvoidanceRay = serializedObject.FindProperty("visualizeAvoidanceRay");
            visualizeDetectionRadius = serializedObject.FindProperty("visualizeDetectionRadius");
            visualizeBoundsRadius = serializedObject.FindProperty("visualizeBoundsRadius");
            detectionQuality = serializedObject.FindProperty("detectionQuality");
            viewAngle = serializedObject.FindProperty("viewAngle");
            turnFraction = serializedObject.FindProperty("turnFraction");
            accuracy = serializedObject.FindProperty("accuracy");
            maxVelocity = serializedObject.FindProperty("maxVelocity");
            detectionRadius = serializedObject.FindProperty("detectionRadius");
            boundsRadius = serializedObject.FindProperty("boundsRadius");
            obstacleAvoidanceForce = serializedObject.FindProperty("obstacleAvoidanceForce");

            #endregion
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Grayed out script property
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((SingleFishAI)target), typeof(SingleFishAI), false);
            GUI.enabled = true;

            #region General Settings

            generalFoldout = GUIHelper.Foldout(generalFoldout, "General Settings");

            if (generalFoldout)
            {
                EditorGUI.indentLevel++;

                // Draw swim type enum popup
                EditorGUILayout.PropertyField(swimType);
                if (swimType.enumValueIndex == (int)SingleFishAI.SwimType.Bobbing)
                {
                    // Display bob properties if swim type is set to bobbing
                    EditorGUI.indentLevel++;

                    bobSpeed.floatValue = EditorGUILayout.FloatField(new GUIContent("Bob Speed", "Speed at which the fish bobs up and down."), bobSpeed.floatValue);
                    bobHeight.floatValue = EditorGUILayout.FloatField(new GUIContent("Bob Height", "The height which the fish will bob with."), bobHeight.floatValue);
                    faceBobDirection.boolValue = EditorGUILayout.Toggle(new GUIContent("Face Bob Direction", "Toggle to have the fish face the direction he bobs in."), faceBobDirection.boolValue);
                    
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(10);

                EditorGUILayout.PropertyField(visualizeViewRays);
                EditorGUILayout.PropertyField(visualizeAvoidanceRay);
                EditorGUILayout.PropertyField(visualizeDetectionRadius);
                EditorGUILayout.PropertyField(visualizeBoundsRadius); 

                EditorGUI.indentLevel--;
            }

            #endregion

            #region Detection Settings

            detectionFoldout = GUIHelper.Foldout(detectionFoldout, "Detection Settings");

            if (detectionFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(detectionQuality);
                EditorGUILayout.PropertyField(viewAngle);
                EditorGUILayout.PropertyField(turnFraction);
                EditorGUILayout.PropertyField(accuracy);

                EditorGUI.indentLevel--;
            }

            #endregion

            #region Movement Settings

            movementFoldout = GUIHelper.Foldout(movementFoldout, "Movement Settings");

            if (movementFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(maxVelocity);
                EditorGUILayout.PropertyField(detectionRadius);
                EditorGUILayout.PropertyField(boundsRadius);

                EditorGUILayout.Space(10);

                EditorGUILayout.PropertyField(obstacleAvoidanceForce);

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
