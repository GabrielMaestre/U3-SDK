////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using NUnit.Framework;
using SDG.Framework.Foliage;
using SDG.Framework.Landscapes;
using SDG.Unturned;
using UnityEngine;

internal class MathTests
{
	[Test]
	public void AngleDegreesNearlyEqual()
	{
		Assert.IsTrue(MathfEx.IsAngleDegreesNearlyEqual(-0.0f, 0.0f), "zero nearly equal");
		Assert.IsTrue(MathfEx.IsAngleDegreesNearlyEqual(0.0f, 360.0f), "zero and 360 nearly equal");
		Assert.IsTrue(MathfEx.IsAngleDegreesNearlyEqual(-360.0f, 360.0f), "negative 360 and 360 nearly equal");
		Assert.IsTrue(MathfEx.IsAngleDegreesNearlyEqual(-360.0f, 720.0f), "negative 360 and 720 nearly equal");
		Assert.IsTrue(MathfEx.IsAngleDegreesNearlyEqual(-180, 180.0f), "negative 180 and 180 nearly equal");
		Assert.IsFalse(MathfEx.IsAngleDegreesNearlyEqual(-90.0f, 90.0f), "negative 90 and 90 not nearly equal");
		Assert.IsFalse(MathfEx.IsAngleDegreesNearlyEqual(35.0f, 40.0f), "35 and 40 not nearly equal");
	}

	[Test]
	public void RoundScale()
	{
		Assert.AreEqual(Vector3.one, Vector3.one.GetRoundedIfNearlyEqualToOne(), "one equals one");
		Assert.AreEqual(Vector3.one, new Vector3(1.0001f, 1.0f, 1.0f).GetRoundedIfNearlyEqualToOne(), "x nearly equals one");
		Assert.AreEqual(new Vector3(1.0f, -2.0f, 3.0f), new Vector3(1.0001f, -2.0f, 3.0f).GetRoundedIfNearlyEqualToOne(), "round positive x only");
		Assert.AreEqual(new Vector3(-1.0f, -2.0f, 3.0f), new Vector3(-1.0001f, -2.0f, 3.0f).GetRoundedIfNearlyEqualToOne(), "round negative x only");
		Assert.AreEqual(new Vector3(5.0f, 1.0f, 3.0f), new Vector3(5.0f, 1.0001f, 3.0f).GetRoundedIfNearlyEqualToOne(), "round positive y only");
		Assert.AreEqual(new Vector3(5.0f, -1.0f, 3.0f), new Vector3(5.0f, -1.0001f, 3.0f).GetRoundedIfNearlyEqualToOne(), "round negative y only");
		Assert.AreEqual(new Vector3(5.0f, 16.3f, 1.0f), new Vector3(5.0f, 16.3f, 1.0001f).GetRoundedIfNearlyEqualToOne(), "round positive z only");
		Assert.AreEqual(new Vector3(5.0f, 16.3f, -1.0f), new Vector3(5.0f, 16.3f, -1.0001f).GetRoundedIfNearlyEqualToOne(), "round negative z only");
	}

	[Test]
	public void RoundAxisAlignedQuaternion()
	{
		Assert.AreEqual(Quaternion.identity, Quaternion.identity.GetRoundedIfNearlyAxisAligned(), "identity equals identity");
		Assert.AreEqual(Quaternion.Euler(90.0f, 0.0f, 0.0f), Quaternion.Euler(89.99f, 0.0f, 0.0f).GetRoundedIfNearlyAxisAligned(), "round nearly 90 around x");
		Assert.AreEqual(Quaternion.Euler(0.0f, 90.0f, 0.0f), Quaternion.Euler(0.0f, 89.99f, 0.0f).GetRoundedIfNearlyAxisAligned(), "round nearly 90 around y");
		Assert.AreEqual(Quaternion.Euler(0.0f, 0.0f, 90.0f), Quaternion.Euler(0.0f, 0.0f, 89.99f).GetRoundedIfNearlyAxisAligned(), "round nearly 90 around z");
		Assert.AreNotEqual(Quaternion.Euler(0.0f, 90.0f, 0.0f), Quaternion.Euler(5.0f, 89.99f, 5.0f).GetRoundedIfNearlyAxisAligned(), "do not round y if other axes are not aligned");
		Assert.AreNotEqual(Quaternion.Euler(0.0f, 0.0f, 90.0f), Quaternion.Euler(5.0f, 5.0f, 89.99f).GetRoundedIfNearlyAxisAligned(), "do not round z if other axes are not aligned");
	}

	[Test]
	public void FoliageShadowDistanceUsesSquaredThreshold()
	{
		float previousDistance = FoliageSettings.shadowDistance;
		try
		{
			FoliageSettings.shadowDistance = 32.0f;
			Assert.IsTrue(FoliageSettings.shouldCastClutterShadows(32.0f * 32.0f));
			Assert.IsFalse(FoliageSettings.shouldCastClutterShadows((32.0f * 32.0f) + 0.01f));
		}
		finally
		{
			FoliageSettings.shadowDistance = previousDistance;
		}
	}

	[Test]
	public void ShadowDistanceCalculationUsesStablePreset()
	{
		System.Reflection.MethodInfo method = typeof(GraphicsSettings).GetMethod("calculateShadowDistance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
		Assert.IsNotNull(method);
		Assert.AreEqual(225.0f, (float) method.Invoke(null, new object[] { EGraphicQuality.HIGH, 0.5f, 2048.0f }));
		Assert.AreEqual(300.0f, (float) method.Invoke(null, new object[] { EGraphicQuality.HIGH, 1.0f, 2048.0f }));
		Assert.AreEqual(128.0f, (float) method.Invoke(null, new object[] { EGraphicQuality.ULTRA, 1.0f, 128.0f }));
	}

	[Test]
	public void LowestUniqueLodStopsCastingShadows()
	{
		GameObject root = new GameObject("LOD test");
		try
		{
			LODGroup lodGroup = root.AddComponent<LODGroup>();
			MeshRenderer sharedRenderer = new GameObject("Shared").AddComponent<MeshRenderer>();
			sharedRenderer.transform.parent = root.transform;
			MeshRenderer farRenderer = new GameObject("Far").AddComponent<MeshRenderer>();
			farRenderer.transform.parent = root.transform;
			lodGroup.SetLODs(new[]
			{
				new LOD(0.5f, new Renderer[] { sharedRenderer }),
				new LOD(0.1f, new Renderer[] { sharedRenderer, farRenderer })
			});

			lodGroup.DisableShadowsOnLowestUniqueLod();

			Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, sharedRenderer.shadowCastingMode);
			Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, farRenderer.shadowCastingMode);
		}
		finally
		{
			Object.DestroyImmediate(root);
		}
	}

	[Test]
	public void TerrainMaximumLodOnlyChangesInOuterDistanceRing()
	{
		System.Reflection.MethodInfo method = typeof(Landscape).GetMethod("calculateTerrainMaximumLOD", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
		Assert.IsNotNull(method);
		Assert.AreEqual(0, (int) method.Invoke(null, new object[] { 749.0f * 749.0f, 1000.0f }));
		Assert.AreEqual(0, (int) method.Invoke(null, new object[] { 750.0f * 750.0f, 1000.0f }));
		Assert.AreEqual(1, (int) method.Invoke(null, new object[] { 751.0f * 751.0f, 1000.0f }));
	}

	[Test]
	public void SunShaftsRequestsDepthBufferForOcclusion()
	{
		Assert.AreEqual(DepthTextureMode.Depth, new SunShaftsRenderer().GetCameraFlags());
	}

	[Test]
	public void WaterFallbackExposesQualityProperty()
	{
		Shader shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Sources/Shaders/Water_Fallback/Water_Fallback.shader");
		Assert.IsNotNull(shader);
		Material material = new Material(shader);
		try
		{
			Assert.IsTrue(material.HasProperty("_WaterQuality"));
		}
		finally
		{
			Object.DestroyImmediate(material);
		}
	}

	[TestCase("1", true)]
	[TestCase("50", true)]
	[TestCase("0", false)]
	[TestCase("51", false)]
	[TestCase("invalid", false)]
	public void CommandSpeedMultiplierRange(string parameter, bool expected)
	{
		System.Reflection.MethodInfo method = typeof(CommandSpeed).GetMethod("TryParseMultiplier", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
		Assert.IsNotNull(method);
		object[] arguments = { parameter, 0 };
		Assert.AreEqual(expected, method.Invoke(null, arguments));
	}
}
