using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
#if BURST_PRESENT
using Unity.Burst;
#endif

namespace LowPolyUnderwaterPack
{
    /// <summary>
    /// Low Poly Underwater Pack script that handles velocity calculation and some raycasting for boids by using Unity's C# Job System and the Burst compiler.
    /// </summary>
    public class BoidMaster : MonoBehaviour
    {
#if BURST_PRESENT
        [Tooltip("Toggles whether boids use Unity's C# Jobs System or not. Likely much worse performance when toggled off.")]
        public bool runWithJobs = true;
#endif
        [Tooltip("Distance at which all boids will be culled.")]
        public float boidDisableDistance = 150;

// Script only used if the burst compiler is installed
#if BURST_PRESENT

        #region Private Fields

        private FlockingBoidAI[] boids;
        private MeshRenderer[] boidRends = null;
        private Transform player;

        #region Jobs Data

        private TransformAccessArray transformAccessArray;

        private NativeArray<Vector3> resultingVelocities;
        private NativeArray<Vector3> boidPositions;
        private NativeArray<Vector3> boidVelocities;
        private NativeArray<Vector3> forwards;
        private NativeArray<Vector2> minMaxBoidGroup;
        private NativeArray<Vector2> detectionBoundsRadius;
        private NativeArray<Vector3> cohAliRepForces;
        private NativeArray<int> hashValues;

        private NativeArray<RaycastHit> results;
        private NativeArray<RaycastCommand> commands;

        #endregion

        #endregion

        #region Unity Callbacks

        private void Awake() 
        {
            if (runWithJobs)
            {
                player = GameObject.FindGameObjectWithTag("MainCamera").transform;
                boids = FindObjectsOfType<FlockingBoidAI>();

                boidRends = new MeshRenderer[boids.Length];
                for (int i = 0; i < boids.Length; i++)
                {
                    boidRends[i] = boids[i].gameObject.GetComponentInChildren<MeshRenderer>();
                }
            }
        }

        private void Start()
        {          
            // Only initialize data for jobs if we're choosing to use it
            if (runWithJobs)
            {
                transformAccessArray = new TransformAccessArray(boids.Length);

                // Add all boid transforms to the transformAccessArray
                for (int i = 0; i < boids.Length; i++)
                {
                    transformAccessArray.Add(boids[i].transform);
                }

                // Initialize native arrays for velocity calculation
                resultingVelocities = new NativeArray<Vector3>(boids.Length, Allocator.Persistent);
                boidPositions = new NativeArray<Vector3>(boids.Length, Allocator.Persistent);
                boidVelocities = new NativeArray<Vector3>(boids.Length, Allocator.Persistent);
                forwards = new NativeArray<Vector3>(boids.Length, Allocator.Persistent);
                minMaxBoidGroup = new NativeArray<Vector2>(boids.Length, Allocator.Persistent);
                detectionBoundsRadius = new NativeArray<Vector2>(boids.Length, Allocator.Persistent);
                cohAliRepForces = new NativeArray<Vector3>(boids.Length, Allocator.Persistent);
                hashValues = new NativeArray<int>(boids.Length, Allocator.Persistent);

                // Native arrays for raycasting
                results = new NativeArray<RaycastHit>(boids.Length, Allocator.Persistent);
                commands = new NativeArray<RaycastCommand>(boids.Length, Allocator.Persistent);
            }
        }

        private void Update()
        {
            if (runWithJobs)
            {
                int offset = 0;

                // Loop through all boids and copy information to respective native arrays
                for (int i = 0; i < boids.Length; i++)
                {
                    if (boidRends[i].enabled == false)//(inDrawDist[i] == false)
                    {
                        offset++;
                        continue;
                    }
                    
                    boidPositions[i - offset] = boids[i].transform.position;
                    boidVelocities[i - offset] = boids[i].velocity;
                    forwards[i - offset] = boids[i].transform.forward;
                    minMaxBoidGroup[i - offset] = boids[i].minMaxBoidGroup;
                    detectionBoundsRadius[i - offset] = new Vector2(boids[i].detectionRadius, boids[i].boundsRadius);
                    cohAliRepForces[i - offset] = new Vector3(boids[i].cohesionForce, boids[i].alignmentForce, boids[i].repulsionForce);
                    hashValues[i - offset] = boids[i].fishName.GetHashCode();

                    commands[i - offset] = new RaycastCommand(boidPositions[i - offset], boidVelocities[i - offset].normalized, detectionBoundsRadius[i - offset].x, ~(1 << LayerMask.NameToLayer("Fish")));
                }

                // Schedule and complete batch for raycasting
                JobHandle rayHandle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));

                // Prepare the velocity update job
                VelocityUpdateJob velocityUpdateJob = new VelocityUpdateJob()
                {
                    resultingVelocities = resultingVelocities,
                    boidPositions = boidPositions,
                    boidVelocities = boidVelocities,
                    forward = forwards,
                    minMaxBoidGroup = minMaxBoidGroup,
                    detectionBoundsRadius = detectionBoundsRadius,
                    cohAliRepForces = cohAliRepForces,
                    hashValues = hashValues,
                    deltaTime = Time.deltaTime,
                };

                // Schedule and complete batch for velocity updating
                JobHandle jobHandle = velocityUpdateJob.Schedule(boids.Length - offset, 1);
                jobHandle.Complete();
                rayHandle.Complete();

                offset = 0;

                // Loop through all boids and apply information back from jobs
                for (int i = 0; i < boids.Length; i++)
                {
                    if (boidRends[i].enabled == false)
                    {
                        boids[i].velocity = Vector3.zero;
                        boids[i].headingForCollision = false;
                        offset++;
                        continue;
                    }
                    
                    boids[i].velocity += resultingVelocities[i - offset];
                    boids[i].headingForCollision = (results[i - offset].transform != null);

                    if (boids[i].headingForCollision)
                        boids[i].avoidancePoint = results[i - offset].point;
                }
            }
        }

        private void OnDestroy()
        {
            // Dispose of velocity calcuation native arrays if using jobs
            if (runWithJobs)
            {
                resultingVelocities.Dispose();
                transformAccessArray.Dispose();
                boidPositions.Dispose();
                boidVelocities.Dispose();
                forwards.Dispose();
                minMaxBoidGroup.Dispose();
                detectionBoundsRadius.Dispose();
                cohAliRepForces.Dispose();
                hashValues.Dispose();

                // Dispose of raycasting native arrays
                results.Dispose();
                commands.Dispose();
            }
        }

        #endregion

        /// <summary>
        /// Uses C# Jobs and the Burst Compiler to handle boids AI for FlockingBoidAI script.
        /// </summary>
        [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
        private struct VelocityUpdateJob : IJobParallelFor
        {
            [WriteOnly, NoAlias] public NativeArray<Vector3> resultingVelocities;

            [ReadOnly, NoAlias] public NativeArray<Vector3> boidPositions;
            [ReadOnly, NoAlias] public NativeArray<Vector3> boidVelocities;
            [ReadOnly, NoAlias] public NativeArray<Vector3> forward;
            [ReadOnly, NoAlias] public NativeArray<Vector2> minMaxBoidGroup;
            [ReadOnly, NoAlias] public NativeArray<Vector2> detectionBoundsRadius;    // detectionRadius and boundsRadius respectively
            [ReadOnly, NoAlias] public NativeArray<Vector3> cohAliRepForces;    // cohesionForce, alignmentForce, and repulsionForce respectively
            [ReadOnly, NoAlias] public NativeArray<int> hashValues;

            Vector3 cohesionAverage, repulsionAverage, alignmentAverage, posAverage;
            Vector3 cohesion, alignment, repulsion;

            public float deltaTime;

            public void Execute(int current)
            {
                cohesionAverage = Vector3.zero;
                repulsionAverage = Vector3.zero;
                alignmentAverage = Vector3.zero;
                posAverage = Vector3.zero;

                // Loop through boids until all have been looped through or the max detectable boids has been reached
                int count = 0;
                int avoidCount = 0;
                for (int i = 0; i < boidPositions.Length && count <= minMaxBoidGroup[current].y; i++)
                {
                    // If it is the same boid, continue to next iteration
                    if (boidPositions[current] == boidPositions[i] && boidVelocities[current] == boidPositions[i])
                         continue;
                    
                    Vector3 offset = boidPositions[i] - boidPositions[current];
                    float sqrDst = offset.x * offset.x + offset.y * offset.y + offset.z * offset.z;

                    // If fish is inside detection radius and fish types are the same (via hash value comparison)
                    if (sqrDst < detectionBoundsRadius[current].x * detectionBoundsRadius[current].x && hashValues[current] == hashValues[i])
                    {
                        // Add respective values to forces
                        cohesionAverage += offset;
                        alignmentAverage += boidVelocities[i];
                        posAverage += boidPositions[i];

                        // If the boid is inside bounds radius, add to repulsion force
                        if (sqrDst < detectionBoundsRadius[current].y * detectionBoundsRadius[current].y)
                        {
                            avoidCount++;
                            repulsionAverage += offset;
                        }

                        count++;
                    }
                }

                // Average out all forces
                cohesionAverage /= count;
                alignmentAverage /= count;
                repulsionAverage /= avoidCount;
                posAverage /= count;

                // Calculate force values by lerping from 0 to the average depending on distance/closeness to average or other boids
                cohesion = Vector3.Lerp(Vector3.zero, cohesionAverage.normalized, Vector3.Distance(boidPositions[current], posAverage) / detectionBoundsRadius[current].x) * cohAliRepForces[current].x;
                alignment = Vector3.Lerp(Vector3.zero, alignmentAverage.normalized, Mathf.Abs(1 - Vector3.Dot(boidVelocities[current].normalized, alignmentAverage.normalized)) / 2) * cohAliRepForces[current].y;
                repulsion = Vector3.Lerp(Vector3.zero, repulsionAverage.normalized, 1 - (repulsionAverage.sqrMagnitude / (detectionBoundsRadius[current].y * detectionBoundsRadius[current].y))) * cohAliRepForces[current].z;

                // If not a valid number, return 0 for repulsion
                if (System.Single.IsNaN(repulsion.sqrMagnitude))
                    repulsion = Vector3.zero;

                Vector3 v = cohesion + alignment - repulsion;
                
                // If there are no flocking forces, go forward
                if (v == Vector3.zero)
                    v = forward[current];

                resultingVelocities[current] = v;
            }
        }
#endif
    }

    /// <summary>
    /// Low Poly Underwater Pack custom editor which creates a custom inspector for BoidMaster to organize properties and improve user experience.
    /// </summary>
#if UNITY_EDITOR
    [CustomEditor(typeof(BoidMaster), true), CanEditMultipleObjects, System.Serializable]
    public class BoidMaster_Editor : Editor
    {
        private SerializedProperty runWithJobs, boidDisableDistance;

        private bool boidFoldout = true;

        private void OnEnable()
        {
            #region Seriealized Property Initialization

            runWithJobs = serializedObject.FindProperty("runWithJobs");
            boidDisableDistance = serializedObject.FindProperty("boidDisableDistance");

            #endregion
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Grayed out script property
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((BoidMaster)target), typeof(BoidMaster), false);
            GUI.enabled = true;

            #region Boid Settings

            boidFoldout = GUIHelper.Foldout(boidFoldout, "Boid Settings");

            if (boidFoldout)
            {
                EditorGUI.indentLevel++;

#if BURST_PRESENT
                EditorGUILayout.PropertyField(runWithJobs);
#endif
                EditorGUILayout.PropertyField(boidDisableDistance);

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

