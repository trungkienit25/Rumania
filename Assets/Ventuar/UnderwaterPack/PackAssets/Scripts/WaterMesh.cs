using System;
using System.Collections;
using UnityEngine;
using UnityEditor;

namespace LowPolyUnderwaterPack
{
	/// <summary>
	/// Low Poly Underwater Pack script that generates the water mesh and sends animation, coloring, and shading data to Water.shader
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
	public class WaterMesh : MonoBehaviour
	{
		[Tooltip("Compute shader used to by the CPU to calculate water height.")]
		public ComputeShader computeShader;
		private GameObject camera;
		#region Visualization Settings

		[Tooltip("Display a wireframe view of the water grid mesh.")]
		[SerializeField] private bool displayWireframe = false;

		[Tooltip("Toggle visualization of waves and related graphical features.")]
		[SerializeField] private  bool visualizeWaves = true;
		[Tooltip("Toggle visualization of base waves.")]
		[SerializeField] private  bool visualizeBaseWaves = true;
		[Tooltip("Toggle visualization of noise displacement.")]
		[SerializeField] private  bool visualizeWaveNoise = true;
		[Tooltip("Toggle visualization of foam rendering.")]
		[SerializeField] private  bool visualizeWaveFoam = true;

		#endregion

		#region Mesh Settings

		[Tooltip("Toggle whether all sides have the same length or not.")]
		[SerializeField] private bool isEquilateral = true;
		[Min(0), Tooltip("X size of the water grid mesh.")]
		[SerializeField] private  float xSize = 100;
		[Min(0), Tooltip("Z size of the water grid mesh.")]
		[SerializeField] private  float zSize = 100;
		[Min(1), Tooltip("Number of geometry subdivisions in the water grid mesh.")]
		[SerializeField] private  int subdivision = 25;

		#endregion

		#region Shader Settings

		[Tooltip("The color the water will sample when the surface below is at its deepest.")]
		[SerializeField] private  Color depthColor = new Color(0.3f, 0.55f, 0.75f, 0.6f);
		[Tooltip("The color the water will sample when the surface below is shallow.")]
		[SerializeField] private  Color shallowColor = new Color(0.43f, 0.73f, 0.75f, 1f);
		[Tooltip("Color to draw the water foam.")]
		[SerializeField] private  Color foamColor = Color.white * 0.9f;
		[Min(0), Tooltip("Maximum distance which the surface below the water will affect the depth and shallow color blending.")]
		[SerializeField] private  float depthDistance = 60;
		[Range(0, 10), Tooltip("Value to control how smooth the blend between the depth and shallow colors are.")]
		[SerializeField] private  float depthBlending = 1;

		[Range(0, 50), Tooltip("How \"shiny\" the mesh is. Setting this value to 0 will not get rid of specularity entirely -- set _SpecIntensity to 0 as well to achieve this.")]
		[SerializeField] private  float specularity = 40;
		[Range(0, 5), Tooltip("Strength/brightness of the specular effect.")]
		[SerializeField] private  float specularIntensity = 0.75f;

		[Tooltip("Noise texture used to generate foam.")]
		[SerializeField] private  Texture2D foamNoise = null;
		[Tooltip("Tiling and offset values of the foam noise texture.")]
		[SerializeField] private  Vector2 foamNoiseTiling = Vector2.one, foamNoiseOffset = Vector2.zero;

		[Min(0), Tooltip("Speed in UVs per second which the noise will scroll.")]
		[SerializeField] private  float foamScrollSpeed = 2;
		[Range(0, 1), Tooltip("Values in the noise texture above this cutoff are rendered on the surface.")]
		[SerializeField] private  float foamNoiseCutoff = .25f;
		[Tooltip("Controls the distance that surfaces below the water will contribute to foam being rendered.")]
		[SerializeField] private  Vector2 foamMinMaxDistance = new Vector2(5, 20);

		[Tooltip("Red and green channels of this texture are used to offset the foam noise texture to create distortion in the foam.")]
		[SerializeField] private  Texture2D foamDistortionTexture = null;
		[Tooltip("Tiling and offset values of the foam distortion texture.")]
		[SerializeField] private  Vector2 foamDistTiling = Vector2.one, foamDistOffset = Vector2.zero;
		[Range(0, 1), Tooltip("Amplifies the foam distortion effect given by the foam distortion texture.")]
		[SerializeField] private  float foamDistortionAmount = 0.2f;

		[Tooltip("Controls the direction of the waves.")]
		[SerializeField] private  Vector2 waveDirection = Vector2.one;

		[Min(0), Tooltip("Controls the height of the primary waves.")]
		[SerializeField] private  float waveAmplitude1 = 1;
		[Min(0), Tooltip("Controls how frequently a primary wave appears. Lower values spread waves further out and vice versa.")]
		[SerializeField] private  float waveFrequency1 = 10;
		[Min(0), Tooltip("Controls how fast primary waves move.")]
		[SerializeField] private  float waveSpeed1 = 0.25f;

		[Min(0), Tooltip("Controls the height of the secondary waves.")]
		[SerializeField] private  float waveAmplitude2 = 0.5f;
		[Min(0), Tooltip("Controls how frequently a secondary wave appears. Lower values spread waves further out and vice versa.")]
		[SerializeField] private  float waveFrequency2 = 6;
		[Min(0), Tooltip("Controls how fast secondary waves move.")]
		[SerializeField] private  float waveSpeed2 = 0.4f;

		[Min(0), Tooltip("Controls amplification of the displacement noise in the vertex displacement calculations.")]
		[SerializeField] private  float noiseAmplitude = 2;
		[Min(0), Tooltip("How fast the displacement noise scrolls along the mesh.")]
		[SerializeField] private  float noiseSpeed = 1.5f;
		[Min(0.01f), Tooltip("The scale/density of the displacement noise.")]
		[SerializeField] private  float noiseScale = 3.5f;

        #endregion

		#region Particle Settings

		[Tooltip("The parent object of particles used underwater.")]
        [SerializeField] private ParticleSystem[] underwaterParticles = null;
        [Tooltip("The default queue which particles render on. Unless specifically changed, the value should be 3000.")]
        [SerializeField] private int defaultParticleQueue = 3000;

		#endregion

        #region Private Fields

        private Mesh mesh;
		private Vector3[] startVerts, verts;
		private MeshFilter filter;
		private BoxCollider col;
		private UnderwaterEffect underwaterEffect;

		private MaterialPropertyBlock propBlock;
		private Renderer rend;

		private ComputeShader computeAsset;
		private Material waterMatAsset;
		private Texture2D foamNoiseAsset, foamFlowAsset;

		private int underwaterQueue;
        private int surfaceQueue;

		private Vector2 foamTilingFactor = new Vector2();	// Used to scale foam texture appropriately with the water

        #endregion

        #region Unity Callbacks


        private void Awake()
		{
			// Throws an error if called on project load in edit mode
			if (Application.isPlaying)
				underwaterEffect = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<UnderwaterEffect>();

			try
			{
				filter = GetComponent<MeshFilter>();
				rend = GetComponent<Renderer>();
				col = GetComponent<BoxCollider>();	
			}
			catch { }
		}

		private void Start() 
		{
			camera = GameObject.FindGameObjectWithTag("MainCamera");
			col.isTrigger = true;

			// Make sure mesh is generated correctly on start
			filter.sharedMesh = GenerateMesh(1);
			mesh = filter.sharedMesh;

			// Initialize vertices
			startVerts = new Vector3[mesh.vertexCount];
			verts = new Vector3[mesh.vertexCount];
			Array.Copy(mesh.vertices, startVerts, startVerts.Length);
			
			// Ensure main directional light(s) have the SetShadowMapAsGlobalTexture script necessary for correct water shading
			Light[] sceneLights = FindObjectsOfType<Light>();
			for (int i = 0; i < sceneLights.Length; i++)
			{
				if (sceneLights[i].type == LightType.Directional && !sceneLights[i].gameObject.GetComponent<SetShadowMapAsGlobalTexture>())
				{
					sceneLights[i].gameObject.AddComponent<SetShadowMapAsGlobalTexture>();
				}
			}

			// Set underwater and surface render queue values for correct particle rendering
            underwaterQueue = defaultParticleQueue + 1;
            surfaceQueue = defaultParticleQueue - 1;

			UpdatePropBlock();
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			#region Public Field Clamping

			foamMinMaxDistance.x = Mathf.Clamp(foamMinMaxDistance.x, 0, foamMinMaxDistance.y);
			foamMinMaxDistance.y = Mathf.Clamp(foamMinMaxDistance.y, foamMinMaxDistance.x, 200);    // Upper bound taken from editor code

			waveDirection.x = Mathf.Clamp(waveDirection.x, -1, 1);
			waveDirection.y = Mathf.Clamp(waveDirection.y, -1, 1);

            #endregion

			// Initialize necessary components if not already initialized
			if (filter == null || col == null)
				Awake();

			// Generate new mesh whenever a value is changed in inspector
			EditorApplication.delayCall += () => 
			{ 
				try
				{	
					// Object selected in hierarchy/scene, not in project or prefab mode (used to prevent constant refreshing when selecting water prefab)
					if (PrefabUtility.GetPrefabInstanceStatus(gameObject) != PrefabInstanceStatus.NotAPrefab || AssetDatabase.Contains(gameObject) == false)
					{
						filter.sharedMesh = GenerateMesh(37);
					}
				}
				catch { }
			};

			mesh = filter.sharedMesh;
        }
#endif

        private void Update()
		{				
			// Update collider properties. Only y values of collider should be editable to match the mesh size
			col.size = new Vector3(xSize, col.size.y, zSize);
			col.center = new Vector3(0, col.center.y, 0);

			if (Application.isPlaying)
			{
				/*
				 *	In-game
				 */

				// Particle render queue has to be changed dynamically according to whether or not the camera is underwater in order to render correctly with the water
				if (underwaterParticles.Length > 0 && underwaterEffect != null)
				{
					int queue = ((underwaterEffect.isUnderwater) ? underwaterQueue : surfaceQueue);
					
					// Check if the queue was already applied so the for loop doesn't run every frame
					if (underwaterParticles[0].GetComponent<ParticleSystemRenderer>().material.renderQueue != queue)
					{
						// Loop through all particles and apply appropriate queue
						for (int i = 0; i < underwaterParticles.Length; i++)
						{
							underwaterParticles[i].GetComponent<ParticleSystemRenderer>().material.renderQueue = queue;
						}
					}
				}
			}

			// Update material property block to transfer data to shader whenever something is changed
			UpdatePropBlock();
		}


		private void OnDrawGizmosSelected()
		{
			if (displayWireframe)
			{
				// Wireframe visualization of mesh subdividsion
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireMesh(GetComponent<MeshFilter>().sharedMesh, transform.position, transform.rotation, transform.localScale);
			}
		}

        #endregion

        #region Water Point Calculations

        /// <summary>
        /// Utilizes the water compute shader to find a point intersecting the deformed water mesh with the same x and z values as the given point.  
        /// Due to performance issues, use either GetWaterPointApprox or GetWaterPoints if many references are being made.
        /// </summary>
        /// <param name="pos">World space reference point to be used to find a water intersection point with the same x and z values.</param>
        /// <returns>A world space point intersecting the deformed water mesh with the same x and z values as the inputted point.</returns>
        public Vector3 GetWaterPoint(Vector3 pos)
		{
			// Convert point to local space
			pos = transform.InverseTransformPoint(pos);

			// Compute shader requires a Vector3 array to work, so for one point we just create an array with a length of 1
			Vector3[] yPositions = new Vector3[1];
			yPositions[0] = pos;

			// Create and set compute buffer
			var buffer = new ComputeBuffer(1, sizeof(float) * 3);
			buffer.SetData(yPositions);
			computeShader.SetBuffer(0, "yPositions", buffer);

			// Transfer all required properties to the compute shader
			computeShader.SetVector("waveDir", waveDirection.normalized);
			computeShader.SetVector("frequencies", new Vector2(waveFrequency1, waveFrequency2));
			computeShader.SetFloat("time", Time.timeSinceLevelLoad);
			computeShader.SetVector("speeds", new Vector2(waveSpeed1, waveSpeed2));
			computeShader.SetVector("amplitudes", new Vector3(waveAmplitude1, waveAmplitude2, noiseAmplitude));
			computeShader.SetFloat("waterSize", zSize);
			computeShader.SetFloat("noiseSpeed", noiseSpeed);
			computeShader.SetFloat("noiseAmp", noiseAmplitude);
			computeShader.SetFloat("noiseScale", noiseScale);

			// Dispatch, get data, and release
			computeShader.Dispatch(0, 8, 1, 1);

			buffer.GetData(yPositions);

			buffer.Release();

			// Return the single point computed and converted back to world space
			return transform.TransformPoint(yPositions[0]);
		}

		/// <summary>
		/// Finds an approximate point intersecting the deformed water mesh with the same x and z values as the given point. 
		/// Faster than GetWaterPoint but less accurate.
		/// </summary>
		/// <param name="pos">World space reference point to be used to find a water intersection point with the same x and z values.</param>
		/// <returns>A world space point approximately intersecting the deformed water mesh with the same x and z values as the inputted point.</returns>
		public Vector3 GetWaterPointApprox(Vector3 pos)
		{
			Vector2 dir = waveDirection.normalized;
			float time = Shader.GetGlobalVector("_Time").y;

			// Convert point to local space
			pos = transform.InverseTransformPoint(pos);

			// Calculations derived from shader script. No convenient way of sending information from shader to script as far as I know, so we recreate it here
			float k1 = (2 * Mathf.PI) / (1 / waveFrequency1);
			float k2 = (2 * Mathf.PI) / (1 / waveFrequency2);

			float a1 = waveAmplitude1 * zSize * waveFrequency1 / (k1 * 100);
			float a2 = waveAmplitude2 * zSize * waveFrequency2 / (k2 * 100);

			float sf1 = k1 * (((pos.x * dir.x + pos.z * dir.y) / (zSize * 2)) - ((waveSpeed1 / 10) * (time)));

			// Find new x and z starting values to work with wave calculations
			pos.x -= a1 * Mathf.Cos(sf1) / (waveAmplitude1 * 5) * dir.x;
			pos.z -= a1 * Mathf.Cos(sf1) / (waveAmplitude1 * 5) * dir.y;

			float f1 = k1 * (((pos.x * dir.x + pos.z * dir.y) / (zSize * 2)) - ((waveSpeed1 / 10) * (time)));
			float f2 = k2 * (((pos.x * -dir.x + pos.z * dir.y) / (zSize * 2)) - ((waveSpeed2 / 10) * (time)));

			pos.y = (a1 * Mathf.Sin(f1)) + (a2 * Mathf.Sin(f2));
			camera.transform.position = new Vector3(camera.transform.position.x, pos.y, camera.transform.position.z);
			pos.x += a1 * Mathf.Cos(f1) / (waveAmplitude1 * 5) * dir.x;
			pos.z += a1 * Mathf.Cos(f1) / (waveAmplitude1 * 5) * dir.y;

			// Return the single point computed and converted back to world space
			return transform.TransformPoint(pos);
		}

		/// <summary>
		/// Utilizes the water compute shader to find points intersecting the deformed water mesh with the same x and z values as the given points in the input array. 
		/// Faster than multiple references to GetWaterPoint due to all information being sent to compute shader in one batch
		/// </summary>
		/// <param name="pos">World space reference points to be used to find water intersection points with the same x and z values.</param>
		/// <returns>An array world space points intersecting the deformed water mesh with the same length and x and z values as the inputted point array.</returns>
		public Vector3[] GetWaterPoints(Vector3[] pos)
		{
			// Convert all points to local space
			for (int i = 0; i < pos.Length; i++)
			{
				pos[i] = transform.InverseTransformPoint(pos[i]);
			}

			// Copy pos array to new yPositions array
			Vector3[] yPositions = new Vector3[pos.Length];
			pos.CopyTo(yPositions, 0);

			// Create and set compute buffer
			var buffer = new ComputeBuffer(yPositions.Length, sizeof(float) * 3);
			buffer.SetData(yPositions);
			computeShader.SetBuffer(0, "yPositions", buffer);

			// Transfer all required properties to the compute shader
			computeShader.SetVector("waveDir", waveDirection.normalized);
			computeShader.SetVector("frequencies", new Vector2(waveFrequency1, waveFrequency2));
			computeShader.SetFloat("time", Time.timeSinceLevelLoad);
			computeShader.SetVector("speeds", new Vector2(waveSpeed1, waveSpeed2));
			computeShader.SetVector("amplitudes", new Vector3(waveAmplitude1, waveAmplitude2, noiseAmplitude));
			computeShader.SetFloat("waterSize", zSize);
			computeShader.SetFloat("noiseSpeed", noiseSpeed);
			computeShader.SetFloat("noiseAmp", noiseAmplitude);
			computeShader.SetFloat("noiseScale", noiseScale);

			// Dispatch, get data, and release
			int groups = Mathf.CeilToInt(yPositions.Length / 8f);
			computeShader.Dispatch(0, groups, groups, 1);

			buffer.GetData(yPositions);

			buffer.Release();

			// Convert computed points back to world space and return
			for (int i = 0; i < yPositions.Length; i++)
			{
				yPositions[i] = transform.TransformPoint(yPositions[i]);
			}

			return yPositions;
		}

        #endregion

        private Mesh GenerateMesh(int asdasd)
		{
			try
			{
				// Destroy old mesh if it exists
				if (GetComponent<MeshFilter>().sharedMesh != null)
				{
					DestroyImmediate(GetComponent<MeshFilter>().sharedMesh);
				}

				Mesh m = new Mesh();
	
				int xSubdivision = 0;
				int zSubdivision = 0;
				if (isEquilateral) 
				{
					xSubdivision = subdivision;
					zSubdivision = subdivision;
				}
				else if (xSize >= zSize)
				{
					// Subdivide based on the largest axis size value (xSize), with extra size checks performed to prevent long, narrow water strips with too high of a mesh resolution.
					zSubdivision = (int)Mathf.Ceil((float)subdivision / (xSize / zSize));
					xSubdivision = (int) ((zSubdivision / zSize) * xSize);
					foamTilingFactor = new Vector2(xSize / zSize, 1);
				}
				else
				{
					// Subdivide based on the largest axis size value (zSize), with extra size checks performed to prevent long, narrow water strips with too high of a mesh resolution.
					xSubdivision = (int)Mathf.Ceil((float)subdivision / (zSize / xSize));
					zSubdivision = (int) ((xSubdivision / xSize) * zSize);
					foamTilingFactor = new Vector2(1, zSize / xSize);
				}

				int vertexCount = (xSubdivision + 1) * (zSubdivision + 1);
				Vector3[] vertices = new Vector3[vertexCount];
				Vector3[] normals = new Vector3[vertexCount];
				Vector3[] uvs = new Vector3[vertexCount];

				// Add vertices, normals, and uvs according to the mesh subdivision
				int k = 0;
				for (int x = 0; x < xSubdivision + 1; x++)
				{
					for (int z = 0; z < zSubdivision + 1; z++)
					{
						vertices[k] = new Vector3((-xSize * .5f) + (xSize * (x / (float)xSubdivision)), 0, (-zSize * .5f) + (zSize * (z / (float)zSubdivision)));
						normals[k] = Vector3.up;
						uvs[k] = new Vector2(x / (float)xSubdivision, z / (float)zSubdivision);

						k++;
					}
				}

				int[] triangles = new int[(int)(xSubdivision * zSubdivision * 6)];

				int vert = 0;
				int tris = 0;
				for (int x = 0; x < xSubdivision; x++)
				{
					for (int z = 0; z < zSubdivision; z++)
					{
						// Form quad from 2 triangles
						triangles[tris + 0] = vert;
						triangles[tris + 1] = vert + zSubdivision + 1;
						triangles[tris + 2] = vert + 1;
						triangles[tris + 3] = vert + 1;
						triangles[tris + 4] = vert + zSubdivision + 1;
						triangles[tris + 5] = vert + zSubdivision + 2;

						vert++;
						tris += 6;
					}

					vert++;
				}

				// Set all data to mesh
				if (underwaterEffect != null)
					underwaterEffect.waterVerts = vertices;
				
				// Allows for meshes with a subdivision larger than 255
				m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

				m.SetVertices(vertices);
				m.SetNormals(normals);
				m.SetUVs(0, uvs);
				m.SetTriangles(triangles, 0);

				return m;
			}
			catch
			{
				return null;
			}
		}

		private void UpdatePropBlock()
		{
			propBlock = new MaterialPropertyBlock();
			rend.GetPropertyBlock(propBlock);

			propBlock.SetFloat("_WaterSize", zSize);

			#region Visualization

			// Waves
			if (visualizeWaves)
				propBlock.SetFloat("_VisualizeWaves", 1);
			else
				propBlock.SetFloat("_VisualizeWaves", 0);

			// Base Waves
			if (visualizeBaseWaves)
				propBlock.SetFloat("_VisualizeBaseWaves", 1);
			else
				propBlock.SetFloat("_VisualizeBaseWaves", 0);

			// Noise Displacement
			if (visualizeWaveNoise)
				propBlock.SetFloat("_VisualizeWaveNoise", 1);
			else
				propBlock.SetFloat("_VisualizeWaveNoise", 0);

			// Wave Foam
			if (visualizeWaveFoam)
				propBlock.SetFloat("_VisualizeWaveFoam", 1);
			else
				propBlock.SetFloat("_VisualizeWaveFoam", 0);

			#endregion

			#region Shader

			propBlock.SetColor("_Color1", depthColor);
			propBlock.SetColor("_Color2", shallowColor);
			propBlock.SetColor("_Color3", foamColor);
			propBlock.SetFloat("_DepthDistance", depthDistance);
			propBlock.SetFloat("_DepthBlend", depthBlending);

			propBlock.SetFloat("_Specularity", specularity);
			propBlock.SetFloat("_SpecIntensity", specularIntensity);

			if (foamNoise)
				propBlock.SetTexture("_FoamNoise", foamNoise);
			else
				propBlock.SetTexture("_FoamNoise", null);

			propBlock.SetVector("_FoamNoise_ST", new Vector4(foamNoiseTiling.x, foamNoiseTiling.y, foamNoiseOffset.x, foamNoiseOffset.y) * foamTilingFactor);
			propBlock.SetFloat("_FoamScrollSpeed", foamScrollSpeed / 100);  // Divided by 100 so it's easier to make small adjustments
			propBlock.SetFloat("_FoamNoiseCutoff", foamNoiseCutoff);
			propBlock.SetVector("_FoamMinMaxDist", foamMinMaxDistance);

			if (foamDistortionTexture)
				propBlock.SetTexture("_FoamDistortion", foamDistortionTexture);
			else
				propBlock.SetTexture("_FoamDistortion", null);

			propBlock.SetVector("_FoamDistortion_ST", new Vector4(foamDistTiling.x, foamDistTiling.y, foamDistOffset.x, foamDistOffset.y));
			propBlock.SetFloat("_FoamDistortionAmount", foamDistortionAmount);

			propBlock.SetVector("_WaveDir", waveDirection);
			propBlock.SetFloat("_Amplitude1", waveAmplitude1);
			propBlock.SetFloat("_Frequency1", waveFrequency1);
			propBlock.SetFloat("_Speed1", waveSpeed1);
			propBlock.SetFloat("_Amplitude2", waveAmplitude2);
			propBlock.SetFloat("_Frequency2", waveFrequency2);
			propBlock.SetFloat("_Speed2", waveSpeed2);

			propBlock.SetFloat("_NoiseAmplitude", noiseAmplitude);
			propBlock.SetFloat("_NoiseSpeed", noiseSpeed);
			propBlock.SetFloat("_NoiseScale", noiseScale);

			#endregion

			rend.SetPropertyBlock(propBlock);
		}
	}

	/// <summary>
	/// Low Poly Underwater Pack custom editor which creates a custom inspector for WaterMesh to organize properties and improve user experience.
	/// </summary>
#if UNITY_EDITOR
	[CustomEditor(typeof(WaterMesh), true), CanEditMultipleObjects, System.Serializable]
	public class WaterMesh_Editor : Editor
	{
        #region Private Fields

		private SerializedProperty computeShader, displayWireframe, visualizeWaves, visualizeBaseWaves, visualizeWaveNoise, visualizeWaveFoam, isEquilateral, xSize, zSize, subdivision, depthColor, shallowColor, foamColor, depthDistance, depthBlending,
			specularity, specularIntensity, foamNoise, foamNoiseTiling, foamNoiseOffset, foamScrollSpeed, foamNoiseCutoff, foamMinMaxDistance, foamDistortionTexture, foamDistTiling, foamDistOffset, foamDistortionAmount, waveDirection, waveAmplitude1, 
			waveAmplitude2, waveFrequency1, waveFrequency2, waveSpeed1, waveSpeed2, noiseAmplitude, noiseSpeed, noiseScale, underwaterParticles, defaultParticleQueue;

        #region Main Foldouts

        private bool visualizationFoldout = true;
		private bool meshFoldout = true;
		private bool shaderFoldout = true;
		private bool particleFoldout = true;

        #endregion

        #region Nested Folouts

        private bool depthAndColoringFoldout = true;
		private bool foamFoldout = true;
		private bool waveFoldout = true;
		private bool displacementNoiseFoldout = true;
		
        #endregion

        #endregion

        private void OnEnable()
		{
			#region Serialized Property Initialization

			computeShader = serializedObject.FindProperty("computeShader");

			displayWireframe = serializedObject.FindProperty("displayWireframe");
			visualizeWaves = serializedObject.FindProperty("visualizeWaves");
			visualizeBaseWaves = serializedObject.FindProperty("visualizeBaseWaves");
			visualizeWaveNoise = serializedObject.FindProperty("visualizeWaveNoise");
			visualizeWaveFoam = serializedObject.FindProperty("visualizeWaveFoam");

			isEquilateral = serializedObject.FindProperty("isEquilateral");
			xSize = serializedObject.FindProperty("xSize");
			zSize = serializedObject.FindProperty("zSize");
			subdivision = serializedObject.FindProperty("subdivision");

			depthColor = serializedObject.FindProperty("depthColor");
			shallowColor = serializedObject.FindProperty("shallowColor");
			foamColor = serializedObject.FindProperty("foamColor");
			depthDistance = serializedObject.FindProperty("depthDistance");
			depthBlending = serializedObject.FindProperty("depthBlending");
			specularity = serializedObject.FindProperty("specularity");
			specularIntensity = serializedObject.FindProperty("specularIntensity");

			foamNoise = serializedObject.FindProperty("foamNoise");
			foamNoiseTiling = serializedObject.FindProperty("foamNoiseTiling");
			foamNoiseOffset = serializedObject.FindProperty("foamNoiseOffset");
			foamScrollSpeed = serializedObject.FindProperty("foamScrollSpeed");
			foamNoiseCutoff = serializedObject.FindProperty("foamNoiseCutoff");
			foamMinMaxDistance = serializedObject.FindProperty("foamMinMaxDistance");
			foamDistortionTexture = serializedObject.FindProperty("foamDistortionTexture");
			foamDistTiling = serializedObject.FindProperty("foamDistTiling");
			foamDistOffset = serializedObject.FindProperty("foamDistOffset");
			foamDistortionAmount = serializedObject.FindProperty("foamDistortionAmount");

			waveDirection = serializedObject.FindProperty("waveDirection");
			waveAmplitude1 = serializedObject.FindProperty("waveAmplitude1");
			waveAmplitude2 = serializedObject.FindProperty("waveAmplitude2");
			waveFrequency1 = serializedObject.FindProperty("waveFrequency1");
			waveFrequency2 = serializedObject.FindProperty("waveFrequency2");
			waveSpeed1 = serializedObject.FindProperty("waveSpeed1");
			waveSpeed2 = serializedObject.FindProperty("waveSpeed2");

			noiseAmplitude = serializedObject.FindProperty("noiseAmplitude");
			noiseSpeed = serializedObject.FindProperty("noiseSpeed");
			noiseScale = serializedObject.FindProperty("noiseScale");

			underwaterParticles = serializedObject.FindProperty("underwaterParticles");
            defaultParticleQueue = serializedObject.FindProperty("defaultParticleQueue");

			#endregion
		}

		// Handles the display of all inspector properties
		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUI.indentLevel = 0;

			// Grayed out script property
			GUI.enabled = false;
			EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((WaterMesh)target), typeof(MonoScript), false);
			GUI.enabled = true;

			EditorGUILayout.PropertyField(computeShader, new GUIContent("Compute Shader:"));

			EditorGUILayout.Space(10);

			#region Visualization Settings

			visualizationFoldout = GUIHelper.Foldout(visualizationFoldout, "Visualization Settings");

			if (visualizationFoldout)
			{
				EditorGUI.indentLevel++;

				EditorGUILayout.PropertyField(displayWireframe);

				EditorGUILayout.Space(10);

                #region Wave Visualization

				EditorGUILayout.PropertyField(visualizeWaves);

				if (visualizeWaves.boolValue)
				{
					EditorGUI.indentLevel++;

					EditorGUILayout.PropertyField(visualizeBaseWaves);
					EditorGUILayout.PropertyField(visualizeWaveNoise);
					EditorGUILayout.PropertyField(visualizeWaveFoam);

					EditorGUI.indentLevel--;
				}

				EditorGUI.indentLevel--;
				
                #endregion
            }

			#endregion

			#region Mesh Settings

			meshFoldout = GUIHelper.Foldout(meshFoldout, "Mesh Settings");

			if (meshFoldout)
			{
				EditorGUI.indentLevel++;

				EditorGUILayout.PropertyField(isEquilateral);

				if (isEquilateral.boolValue == true)
				{
					float size = xSize.floatValue;
					size = EditorGUILayout.FloatField(new GUIContent("Size", "ASDLJKJ Size of the water grid mesh."), size);

					zSize.floatValue = xSize.floatValue = size;
				}
				else
				{
					float x = xSize.floatValue;
					float z = zSize.floatValue;

					// SerializedProperties cannot be passed as ref values, so we use temp variables to store data and send it back to the SerializedProperty
					GUIHelper.DisplayDualHorizontalFields(ref x, new GUIContent("X Size", "X size of the water grid mesh."),
															ref z, new GUIContent("Z Size", "Z size of the water grid mesh."));
					
					xSize.floatValue = x;
					zSize.floatValue = z;
				}

				EditorGUILayout.PropertyField(subdivision);

				EditorGUI.indentLevel--;
			}

            #endregion

            #region Shader Settings

            shaderFoldout = GUIHelper.Foldout(shaderFoldout, "Shader Settings");

			if (shaderFoldout)
			{
				EditorGUI.indentLevel++;

				#region Depth and Coloring

				depthAndColoringFoldout = GUIHelper.Foldout(depthAndColoringFoldout, "Depth and Coloring");

				if (depthAndColoringFoldout)
				{
					EditorGUI.indentLevel++;

					EditorGUILayout.PropertyField(depthColor);
					EditorGUILayout.PropertyField(shallowColor);
					EditorGUILayout.PropertyField(foamColor);
					EditorGUILayout.PropertyField(depthDistance);
					EditorGUILayout.PropertyField(depthBlending);

					EditorGUILayout.Space(10);

					EditorGUILayout.PropertyField(specularity);
					EditorGUILayout.PropertyField(specularIntensity);

					EditorGUI.indentLevel--;
				}

				#endregion

				#region Foam

				foamFoldout = GUIHelper.Foldout(foamFoldout, "Foam");

				if (foamFoldout)
				{
					EditorGUI.indentLevel++;

					#region Foam Noise Texture Display

					Texture2D noise1 = (Texture2D)foamNoise.objectReferenceValue;
					Vector2 noiseT1 = foamNoiseTiling.vector2Value;
					Vector2 noiseO1 = foamNoiseOffset.vector2Value;

					// SerializedProperties cannot be passed as ref values, so we use temp variables to store data and send it back to the SerializedProperty
					GUIHelper.TextureDisplay(new GUIContent("Foam Noise Texture", "Noise texture used to generate foam."), ref noise1, ref noiseT1, ref noiseO1);

					foamNoise.objectReferenceValue = noise1;
					foamNoiseTiling.vector2Value = noiseT1;
					foamNoiseOffset.vector2Value = noiseO1;

					#endregion

					EditorGUILayout.PropertyField(foamScrollSpeed);
					EditorGUILayout.PropertyField(foamNoiseCutoff);
					EditorGUILayout.PropertyField(foamMinMaxDistance);

					#region Foam Min Max Distance Slider

					float min = foamMinMaxDistance.vector2Value.x;
					float max = foamMinMaxDistance.vector2Value.y;

					// SerializedProperties cannot be passed as ref values, so we use temp variables to store data and send it back to the SerializedProperty
					EditorGUILayout.MinMaxSlider(ref min, ref max, 0, 200);

					foamMinMaxDistance.vector2Value = new Vector2(min, max);

					#endregion

					EditorGUILayout.Space(10);

					#region Foam Distortion Texture Display

					Texture2D noise2 = (Texture2D)foamDistortionTexture.objectReferenceValue;
					Vector2 noiseT2 = foamDistTiling.vector2Value;
					Vector2 noiseO2 = foamDistOffset.vector2Value;

					// SerializedProperties cannot be passed as ref values, so we use temp variables to store data and send it back to the SerializedProperty
					GUIHelper.TextureDisplay(new GUIContent("Foam Flow Map Texture", "Red and green channels of this texture are used to offset the foam noise texture to create distortion in the foam."), ref noise2, ref noiseT2, ref noiseO2);
					
					foamDistortionTexture.objectReferenceValue = noise2;
					foamDistTiling.vector2Value = noiseT2;
					foamDistOffset.vector2Value = noiseO2;

					#endregion

					EditorGUILayout.PropertyField(foamDistortionAmount);

					EditorGUI.indentLevel--;
				}
				#endregion

				#region Wave Settings

				waveFoldout = GUIHelper.Foldout(waveFoldout, "Waves");

				if (waveFoldout)
				{
					EditorGUI.indentLevel++;

					EditorGUILayout.PropertyField(waveDirection);

					EditorGUILayout.Space(10);

					#region Dual Horizontal Wave Properties

					float waveA1 = waveAmplitude1.floatValue;
					float waveA2 = waveAmplitude2.floatValue;

					// SerializedProperties cannot be passed as ref values, so we use temp variables to store data and send it back to the SerializedProperty
					GUIHelper.DisplayDualHorizontalFields(ref waveA1, new GUIContent("Wave Amplitude 1", "Controls the height of the primary waves."),
						ref waveA2, new GUIContent("Wave Amplitude 2", "Controls the height of the secondary waves."));
					
					waveAmplitude1.floatValue = waveA1;
					waveAmplitude2.floatValue = waveA2;

					float waveF1 = waveFrequency1.floatValue;
					float waveF2 = waveFrequency2.floatValue;

					// SerializedProperties cannot be passed as ref values, so we use temp variables to store data and send it back to the SerializedProperty
					GUIHelper.DisplayDualHorizontalFields(ref waveF1, new GUIContent("Wave Frequency 1", "Controls how frequently a primary wave appears. Lower values spread waves further out and vice versa."),
						ref waveF2, new GUIContent("Wave Frequency 2", "Controls how frequently a secondary wave appears. Lower values spread waves further out and vice versa."));
					
					waveFrequency1.floatValue = waveF1;
					waveFrequency2.floatValue = waveF2;

					float waveS1 = waveSpeed1.floatValue;
					float waveS2 = waveSpeed2.floatValue;

					// SerializedProperties cannot be passed as ref values, so we use temp variables to store data and send it back to the SerializedProperty
					GUIHelper.DisplayDualHorizontalFields(ref waveS1, new GUIContent("Wave Speed 1", "Controls how fast primary waves move."),
						ref waveS2, new GUIContent("Wave Speed 2", "Controls how fast secondary waves move."));
					
					waveSpeed1.floatValue = waveS1;
					waveSpeed2.floatValue = waveS2;

					#endregion

					EditorGUI.indentLevel--;
				}

				#endregion

				#region Displacement Noise Settings

				displacementNoiseFoldout = GUIHelper.Foldout(displacementNoiseFoldout, "Displacement Noise");

				if (displacementNoiseFoldout)
				{
					EditorGUI.indentLevel++;

					EditorGUILayout.PropertyField(noiseAmplitude);
					EditorGUILayout.PropertyField(noiseSpeed);
					EditorGUILayout.PropertyField(noiseScale);

					EditorGUI.indentLevel--;
				}

				EditorGUI.indentLevel--;

                #endregion
            }

			#endregion

			#region Particle Settings

			particleFoldout = GUIHelper.Foldout(particleFoldout, "Particle Settings");

			if (particleFoldout)
			{
				EditorGUI.indentLevel++;

				EditorGUILayout.PropertyField(underwaterParticles);
				EditorGUILayout.PropertyField(defaultParticleQueue);

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