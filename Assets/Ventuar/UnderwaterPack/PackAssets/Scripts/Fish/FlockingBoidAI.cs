using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace LowPolyUnderwaterPack
{
    /// <summary>
    /// Low Poly Underwater Pack script that handles flocking behavior for fish using a boids algorithm
    /// </summary>
    public class FlockingBoidAI : SingleFishAI
    {
        #region Flocking Settings

        [Tooltip("The force with which boids will be attracted to each other.")]
        public float cohesionForce = 5;
        [Tooltip("The force with which boids will align with each other.")]
        public float alignmentForce = 2;
        [Tooltip("The force with which boids will be repulsed by each other.")]
        public float repulsionForce = 10;

        [Space(10)]

        [Tooltip("Controls the minimum an maximum number of other boids that each attracts to. A min value of 5 means that boids will ignore other boids unless there are 5 others in it's cohesion radius.")]
        public Vector2 minMaxBoidGroup = new Vector2(1, 10);

        #endregion

        #region Hidden Public Fields

        /**
         *  Hidden public properties needed and/or assigned by other scripts 
         */

        [HideInInspector] public string fishName;
        [HideInInspector] public bool headingForCollision;

        [HideInInspector] public BoidMaster bm;

        // Assiged by BoidSpawner
        [HideInInspector] public bool noBoidMasterInScene = false;

        #endregion

        #region Private Fields

        private Collider[] boids;
        private MeshRenderer rend;
        private Transform player;
        private Transform tr;

        private Vector3 cohesionAverage = Vector3.zero;
        private Vector3 repulsionAverage = Vector3.zero;
        private Vector3 alignmentAverage = Vector3.zero;

        private float disableDist;
        private float playerDist;

        Vector3 initialV;

        #region Alternate Boids Algorithm Fields

        /**
         *  Properties needed solely for the boids algorithm not using C# Jobs
         */

        private FlockingBoidAI boid;

        private int nearBoidsLength;
        private int count, avoidCount;
        private Vector3 cohesion, alignment, repulsion;
        private Vector3 posAverage;
        private Vector3 offset;

        #endregion

        #endregion

        #region Unity Callbacks

        private void Awake() 
        {
            rend = GetComponentInChildren<MeshRenderer>();
            player = GameObject.FindGameObjectWithTag("MainCamera").transform;
        }

        protected override void Start()
        {
            base.Start();

            // Should only apply if the fish was not spawned by a BoidSpawner
            if (bm == null && !noBoidMasterInScene)
                bm = FindObjectOfType<BoidMaster>();

            // If boid master is still null, log a warning
            if (bm == null)
                Debug.LogWarning("No BoidMaster component found in scene. Include an object with the BoidMaster component for the option to utilize the C# Jobs System for more efficient boids calculations.");

            // Remove all extra characters from name so the boids algorithm can accurately compare similar fish types
            List<char> nameChars = new List<char>(name.ToCharArray());
            nameChars = nameChars.FindAll(c => char.IsLetter(c));
            name = new string(nameChars.ToArray());

            // Default distance cull value if no optimization manager exists
            disableDist = (bm != null) ? bm.boidDisableDistance : 350;

            boids = new Collider[(int)minMaxBoidGroup.y * 2];
            initialV = transform.forward * maxVelocity;
            tr = transform;
            fishName = name;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            minMaxBoidGroup.y = Mathf.RoundToInt(Mathf.Clamp(minMaxBoidGroup.y, 1, int.MaxValue));
            minMaxBoidGroup.x = Mathf.RoundToInt(Mathf.Clamp(minMaxBoidGroup.x, 1, minMaxBoidGroup.y));
        }

        protected override void Update()
        {
            playerDist = (tr.position - player.position).sqrMagnitude;

            // Comparing square magnitude to square distance
            if (playerDist <= disableDist * disableDist)
            {
                // If inside disable dist, enable renderer and update
                if (!rend.enabled)
                    rend.enabled = true;

                base.Update();
            }
            else
            {
                // If outside of disable dist, disable renderer and do nothing
                if (rend.enabled)
                    rend.enabled = false;
            }
        }

        #endregion

        #region Boids Algorithms

        protected override void UpdateVelocity()
        {
#if BURST_PRESENT
            // If not using jobs or boid master component does not exist, use alternate (slower) boids algorithm 
            if (bm == null)
            {
                AlternateBoidsAlgorithm();
            }
            else if (!bm.runWithJobs || !bm.enabled)
            {
                AlternateBoidsAlgorithm();
            }
            else
            {
                /**
                 *  Using jobs algorithm
                 */

                // Velocity is handled by BoidMaster and the C# Jobs boids algorithm. All this needs to do is handle collision.
                DetectCollision(headingForCollision);
            }
#else
            // Use the alternate boids algorithm if the burst package is not installed
            AlternateBoidsAlgorithm();
#endif
            // Lerp to velocity so smooth out movement
            velocity = Vector3.Lerp(initialV, velocity, .25f);
            velocity = velocity.normalized * maxVelocity;
            initialV = velocity;
        }

        /// <summary>
        /// Alternate boids algorithm for when the C# Jobs algorithm cannot be used. Generally slower than C# Jobs.
        /// </summary>
        private void AlternateBoidsAlgorithm()
        {
            cohesionAverage = Vector3.zero;
            repulsionAverage = Vector3.zero;
            alignmentAverage = Vector3.zero;
            posAverage = Vector3.zero;

            nearBoidsLength = Physics.OverlapSphereNonAlloc(tr.position, detectionRadius, boids, 1 << LayerMask.NameToLayer("Fish"));

            if (nearBoidsLength < minMaxBoidGroup.x)
            {
                velocity += tr.forward;
            }
            else
            {
                count = nearBoidsLength;
                avoidCount = 0;
                for (int i = 0; i < nearBoidsLength && i < minMaxBoidGroup.y; i++)
                {
                    boid = boids[i].GetComponent<FlockingBoidAI>();

                    // If the fish is a single fish, continue to next iteration
                    if (boid == null)
                        continue;

                    offset = boids[i].transform.position - tr.position;
                    float sqrDst = offset.x * offset.x + offset.y * offset.y + offset.z * offset.z;

                    // Do not flock to fish of different types
                    if (fishName != boid.fishName)
                    {
                        count--;
                        continue;
                    }

                    // Add respective values to forces
                    cohesionAverage += offset;
                    alignmentAverage += boid.velocity;
                    posAverage += boids[i].transform.position;

                    // If the boid is inside bounds radius, add to repulsion force
                    if (sqrDst < boundsRadius * boundsRadius)
                    {
                        avoidCount++;
                        repulsionAverage += offset;
                    }
                }

                // Average out all forces
                cohesionAverage /= count;
                alignmentAverage /= count;
                repulsionAverage /= avoidCount;
                posAverage /= count;

                // Calculate force values by lerping from 0 to the average depending on distance/closeness to average or other boids
                cohesion = Vector3.Lerp(Vector3.zero, cohesionAverage.normalized, Vector3.Distance(tr.position, posAverage) / detectionRadius) * cohesionForce;
                alignment = Vector3.Lerp(Vector3.zero, alignmentAverage.normalized, Mathf.Abs(1 - Vector3.Dot(velocity.normalized, alignmentAverage.normalized)) / 2) * alignmentForce;
                repulsion = Vector3.Lerp(Vector3.zero, repulsionAverage.normalized, 1 - (repulsionAverage.sqrMagnitude / (boundsRadius * boundsRadius))) * repulsionForce;
                
                // If not a valid number, return 0 for repulsion
                if (System.Single.IsNaN(repulsion.sqrMagnitude))
                    repulsion = Vector3.zero;

                Vector3 v = cohesion + alignment - repulsion;

                // If there are no flocking forces, go forward
                if (v == Vector3.zero)
                    v = transform.forward;

                velocity = v;
            }

            DetectCollision(IsHeadingForCollision());
        }

        #endregion
    }

    /// <summary>
    /// Low Poly Underwater Pack custom editor which creates a custom inspector for FlockingBoidAI to organize properties and improve user experience.
    /// </summary>
#if UNITY_EDITOR
    [CustomEditor(typeof(FlockingBoidAI), true), CanEditMultipleObjects, System.Serializable]
    public class FlockingBoidAI_Editor : SingleFishAI_Editor
    {
        private SerializedProperty cohesionForce, alignmentForce, repulsionForce;

        protected override void OnEnable()
        {
            base.OnEnable();

            #region Serialized Property Initialization

            cohesionForce = serializedObject.FindProperty("cohesionForce");
            alignmentForce = serializedObject.FindProperty("alignmentForce");
            repulsionForce = serializedObject.FindProperty("repulsionForce");

            #endregion
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            #region Movement Settings

            // movementFoldout inherited from base class
            if (movementFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(cohesionForce);
                EditorGUILayout.PropertyField(alignmentForce);
                EditorGUILayout.PropertyField(repulsionForce);

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
