////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SDG.Unturned
{
	/// <summary>
	/// Repeatable standalone rendering stress scene. It intentionally uses primitives,
	/// because game bundle assets and gameplay systems require a loaded level.
	/// </summary>
	internal sealed class PerformanceStressScenario : MonoBehaviour
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void loadFromCommandLine()
		{
			if (shouldLoadFromCommandLine && SceneManager.GetActiveScene().name != sceneName)
				SceneManager.LoadScene(sceneName);
		}

		private void Awake()
		{
			setUltraQuality();
			createCameraAndLighting();
			createTerrain();
			createWater();
			createMeshesAndMaterials();
			createInstances();
			StartCoroutine(captureRoutine());
		}

		private void LateUpdate()
		{
			float elapsed = Time.unscaledTime;
			float radians = elapsed * cameraAngularSpeed;
			cameraTransform.position = new Vector3(Mathf.Cos(radians) * cameraRadius, cameraHeight, Mathf.Sin(radians) * cameraRadius);
			cameraTransform.LookAt(new Vector3(0.0f, 12.0f, 0.0f));

			for (int index = 0; index < agentMatrices.Length; ++index)
			{
				Vector3 position = agentPositions[index];
				position.y = 1.0f + Mathf.Sin(elapsed * 2.0f + index) * 0.1f;
				agentMatrices[index] = Matrix4x4.TRS(position, Quaternion.Euler(0.0f, elapsed * 40.0f + index * 17.0f, 0.0f), agentScales[index]);
			}

			drawInstances(treeTrunkMesh, treeMaterial, treeMatrices);
			drawInstances(treeLeafMesh, leafMaterial, treeLeafMatrices);
			drawInstances(cubeMesh, structureMaterial, structureMatrices);
			drawInstances(cylinderMesh, propMaterial, propMatrices);
			drawInstances(capsuleMesh, agentMaterial, agentMatrices);
		}

		private IEnumerator captureRoutine()
		{
			yield return new WaitForSecondsRealtime(warmupSeconds);
			PerformanceMetricsCapture.beginAutomaticStressCapture(captureSeconds);
			yield return new WaitForSecondsRealtime(captureSeconds);
			UnturnedLog.info("Performance stress scenario completed. CSV saved in persistentDataPath/PerformanceCaptures");
		}

		private static void setUltraQuality()
		{
			for (int index = 0; index < QualitySettings.names.Length; ++index)
			{
				if (string.Equals(QualitySettings.names[index], "Ultra", StringComparison.OrdinalIgnoreCase))
				{
					QualitySettings.SetQualityLevel(index, true);
					return;
				}
			}
		}

		private void createCameraAndLighting()
		{
			GameObject cameraObject = new GameObject("Stress Camera", typeof(Camera), typeof(AudioListener));
			Camera camera = cameraObject.GetComponent<Camera>();
			camera.tag = "MainCamera";
			camera.renderingPath = RenderingPath.DeferredShading;
			camera.farClipPlane = 850.0f;
			camera.allowHDR = true;
			camera.allowMSAA = false;
			cameraTransform = camera.transform;

			GameObject lightObject = new GameObject("Stress Sun", typeof(Light));
			Light light = lightObject.GetComponent<Light>();
			light.type = LightType.Directional;
			light.shadows = LightShadows.Soft;
			light.intensity = 1.1f;
			lightObject.transform.rotation = Quaternion.Euler(48.0f, -32.0f, 0.0f);
			RenderSettings.ambientMode = AmbientMode.Skybox;
			RenderSettings.ambientIntensity = 1.0f;
		}

		private void createTerrain()
		{
			TerrainData terrainData = new TerrainData
			{
				heightmapResolution = terrainResolution,
				size = new Vector3(terrainSize, terrainHeight, terrainSize)
			};
			float[,] heights = new float[terrainResolution, terrainResolution];
			for (int z = 0; z < terrainResolution; ++z)
			{
				for (int x = 0; x < terrainResolution; ++x)
				{
					float wave = Mathf.Sin(x * 0.11f) + Mathf.Cos(z * 0.09f);
					heights[z, x] = 0.08f + wave * 0.015f;
				}
			}
			terrainData.SetHeights(0, 0, heights);
			Terrain terrain = Terrain.CreateTerrainGameObject(terrainData).GetComponent<Terrain>();
			terrain.name = "Stress Terrain 1024m";
			terrain.drawInstanced = true;
			terrain.shadowCastingMode = ShadowCastingMode.On;
		}

		private void createWater()
		{
			GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
			water.name = "Stress Water";
			water.transform.position = new Vector3(0.0f, terrainHeight * 0.07f, 0.0f);
			water.transform.localScale = Vector3.one * (terrainSize / 10.0f);
			UnityEngine.Object.Destroy(water.GetComponent<Collider>());
		}

		private void createMeshesAndMaterials()
		{
			cubeMesh = getPrimitiveMesh(PrimitiveType.Cube);
			cylinderMesh = getPrimitiveMesh(PrimitiveType.Cylinder);
			capsuleMesh = getPrimitiveMesh(PrimitiveType.Capsule);
			treeTrunkMesh = cylinderMesh;
			treeLeafMesh = getPrimitiveMesh(PrimitiveType.Sphere);
			Material template = Resources.Load<Material>("Materials/AlwaysInclude/VanillaStandard/Specular/Standard_SpecOff_ReflOff");
			if (template == null)
			{
				UnturnedLog.error("Performance stress scenario requires bundled Standard material");
				enabled = false;
				return;
			}
			treeMaterial = createMaterial(template, new Color(0.23f, 0.12f, 0.04f));
			leafMaterial = createMaterial(template, new Color(0.12f, 0.36f, 0.08f));
			structureMaterial = createMaterial(template, new Color(0.45f, 0.39f, 0.30f));
			propMaterial = createMaterial(template, new Color(0.30f, 0.30f, 0.30f));
			agentMaterial = createMaterial(template, new Color(0.40f, 0.22f, 0.18f));
		}

		private void createInstances()
		{
			System.Random random = new System.Random(12345);
			treeMatrices = createMatrices(treeCount, random, 380.0f, new Vector3(1.5f, 9.0f, 1.5f));
			treeLeafMatrices = new Matrix4x4[treeMatrices.Length];
			for (int index = 0; index < treeMatrices.Length; ++index)
			{
				Vector3 position = treeMatrices[index].GetColumn(3);
				treeLeafMatrices[index] = Matrix4x4.TRS(position + Vector3.up * 7.0f, Quaternion.identity, new Vector3(6.0f, 7.0f, 6.0f));
			}
			structureMatrices = createMatrices(structureCount, random, 260.0f, new Vector3(12.0f, 9.0f, 12.0f));
			propMatrices = createMatrices(propCount, random, 330.0f, new Vector3(1.0f, 3.0f, 1.0f));
			agentMatrices = createMatrices(agentCount, random, 120.0f, new Vector3(1.0f, 1.0f, 1.0f));
			agentPositions = new Vector3[agentMatrices.Length];
			agentScales = new Vector3[agentMatrices.Length];
			for (int index = 0; index < agentMatrices.Length; ++index)
			{
				agentPositions[index] = agentMatrices[index].GetColumn(3);
				agentScales[index] = (index & 1) == 0 ? new Vector3(1.0f, 1.7f, 1.0f) : new Vector3(1.6f, 0.8f, 0.9f);
			}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
			Debug.Assert(treeMatrices.Length == treeCount && agentMatrices.Length == agentCount, "Stress scenario instance generation changed");
#endif
		}

		private static Matrix4x4[] createMatrices(int count, System.Random random, float radius, Vector3 scale)
		{
			Matrix4x4[] matrices = new Matrix4x4[count];
			for (int index = 0; index < matrices.Length; ++index)
			{
				float x = ((float)random.NextDouble() * 2.0f - 1.0f) * radius;
				float z = ((float)random.NextDouble() * 2.0f - 1.0f) * radius;
				matrices[index] = Matrix4x4.TRS(new Vector3(x, terrainHeight * 0.09f, z), Quaternion.Euler(0.0f, random.Next(360), 0.0f), scale);
			}
			return matrices;
		}

		private static Mesh getPrimitiveMesh(PrimitiveType primitiveType)
		{
			GameObject primitive = GameObject.CreatePrimitive(primitiveType);
			Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
			UnityEngine.Object.Destroy(primitive);
			return mesh;
		}

		private static Material createMaterial(Material template, Color color)
		{
			Material material = new Material(template) { color = color, enableInstancing = true };
			return material;
		}

		private static void drawInstances(Mesh mesh, Material material, Matrix4x4[] matrices)
		{
			for (int offset = 0; offset < matrices.Length; offset += maxInstancesPerDraw)
			{
				int count = Mathf.Min(maxInstancesPerDraw, matrices.Length - offset);
				Graphics.DrawMeshInstanced(mesh, 0, material, matrices, count, null, ShadowCastingMode.On, true, 0, null, LightProbeUsage.Off);
			}
		}

		private const string sceneName = "PerformanceStressTest";
		private const int terrainResolution = 129;
		private const float terrainSize = 1024.0f;
		private const float terrainHeight = 80.0f;
		private const int treeCount = 800;
		private const int structureCount = 240;
		private const int propCount = 480;
		private const int agentCount = 160;
		private const int maxInstancesPerDraw = 1023;
		private const float warmupSeconds = 10.0f;
		private const int captureSeconds = 30;
		private const float cameraRadius = 360.0f;
		private const float cameraHeight = 65.0f;
		private const float cameraAngularSpeed = 0.13f;
		private static readonly CommandLineFlag shouldLoadFromCommandLine = new CommandLineFlag(false, "-PerformanceStressTest");

		private Transform cameraTransform;
		private Mesh cubeMesh;
		private Mesh cylinderMesh;
		private Mesh capsuleMesh;
		private Mesh treeTrunkMesh;
		private Mesh treeLeafMesh;
		private Material treeMaterial;
		private Material leafMaterial;
		private Material structureMaterial;
		private Material propMaterial;
		private Material agentMaterial;
		private Matrix4x4[] treeMatrices;
		private Matrix4x4[] treeLeafMatrices;
		private Matrix4x4[] structureMatrices;
		private Matrix4x4[] propMatrices;
		private Matrix4x4[] agentMatrices;
		private Vector3[] agentPositions;
		private Vector3[] agentScales;
	}
}
