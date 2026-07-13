////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned
{
	public class MythicAsset : Asset
	{
		private static GameObject GetOrLoad(ref GameObject loadedAsset, ref IDeferredAsset<GameObject> deferredAsset)
		{
			if (deferredAsset != null)
			{
				loadedAsset = deferredAsset.getOrLoad();
				deferredAsset = null;
			}
			return loadedAsset;
		}

		public string particleTagName
		{
			get;
			protected set;
		}

		protected GameObject _systemArea;
		private IDeferredAsset<GameObject> deferredSystemArea;
		public GameObject systemArea => GetOrLoad(ref _systemArea, ref deferredSystemArea);

		protected GameObject _systemHook;
		private IDeferredAsset<GameObject> deferredSystemHook;
		public GameObject systemHook => GetOrLoad(ref _systemHook, ref deferredSystemHook);

		protected GameObject _systemFirst;
		private IDeferredAsset<GameObject> deferredSystemFirst;
		public GameObject systemFirst => GetOrLoad(ref _systemFirst, ref deferredSystemFirst);

		protected GameObject _systemThird;
		private IDeferredAsset<GameObject> deferredSystemThird;
		public GameObject systemThird => GetOrLoad(ref _systemThird, ref deferredSystemThird);

		/// <summary>
		/// If true, vest and backpack spawn System_Area instead of System_Hook.
		/// </summary>
		public bool ShouldBodyCosmeticsUseAreaPrefab
		{
			get;
			protected set;
		}

		public override EAssetType assetCategory => EAssetType.MYTHIC;

		public override void PopulateAsset(in PopulateAssetParameters p)
		{
			base.PopulateAsset(in p);

			if (id < 500 && !OriginAllowsVanillaLegacyId && !p.data.ContainsKey("Bypass_ID_Limit"))
			{
				throw new System.NotSupportedException("ID < 500");
			}

			if (!Dedicator.IsDedicatedServer)
			{
				particleTagName = p.localization.format("Particle_Tag_Name");
				if (string.IsNullOrEmpty(particleTagName))
				{
					particleTagName = name;
				}

				p.bundle.loadDeferred("System_Area", out deferredSystemArea);
				p.bundle.loadDeferred("System_Hook", out deferredSystemHook);
				p.bundle.loadDeferred("System_First", out deferredSystemFirst);
				p.bundle.loadDeferred("System_Third", out deferredSystemThird);

				ShouldBodyCosmeticsUseAreaPrefab = p.data.ParseBool("Body_Cosmetics_Use_System_Area");

				if (Assets.shouldValidateAssets)
				{
					if (systemArea != null) // Shirt or Pants
					{
						AssetValidation.ValidateLayersEqualRecursive(this, systemArea, LayerMasks.ENEMY);
						ValidateRecursively(systemArea.transform);
					}

					if (systemHook != null) // Hat, Mask, Glasses, Backpack, or Vest
					{
						AssetValidation.ValidateLayersEqualRecursive(this, systemHook, LayerMasks.ENEMY);
						ValidateRecursively(systemHook.transform);
					}

					if (systemFirst != null) // 1st-person Weapon
					{
						AssetValidation.ValidateLayersEqualRecursive(this, systemFirst, LayerMasks.VIEWMODEL);
						ValidateRecursively(systemFirst.transform);
					}

					if (systemThird != null) // 3rd-person Weapon
					{
						AssetValidation.ValidateLayersEqualRecursive(this, systemThird, LayerMasks.ITEM);
						ValidateRecursively(systemThird.transform);
					}
				}

				bool shouldCheckPrefabsNow = Assets.shouldDeferLoadingAssets == false || p.bundle is not MasterBundle;
				if (shouldCheckPrefabsNow && systemArea == null && systemHook == null && systemFirst == null && systemThird == null)
				{
					Assets.ReportError(this, "missing all effect prefabs");
				}
			}
		}

		private void ValidateRecursively(Transform transform)
		{
			ParticleSystem ps = transform.GetComponent<ParticleSystem>();
			if (ps != null)
			{
				ParticleSystem.CollisionModule collisionModule = ps.collision;
				if (collisionModule.enabled)
				{
					const int reasonableMask = RayMasks.RESOURCE
						| RayMasks.LARGE
						| RayMasks.MEDIUM
						| RayMasks.ENVIRONMENT
						| RayMasks.GROUND
						| RayMasks.VEHICLE
						| RayMasks.BARRICADE
						| RayMasks.STRUCTURE;
					if (collisionModule.collidesWith != reasonableMask)
					{
						ReportAssetError($"particle system {transform.GetSceneHierarchyPath()} collision mask includes unexpected layers");
					}

					if (!MathfEx.IsNearlyZero(collisionModule.colliderForce))
					{
						ReportAssetError($"particle system {transform.GetSceneHierarchyPath()} should have zero collider force scale");
					}
				}

				if (!ps.useAutoRandomSeed)
				{
					ReportAssetError($"particle system {transform.GetSceneHierarchyPath()} auto random seed is OFF");
				}
			}

			foreach (Transform child in transform)
			{
				ValidateRecursively(child);
			}
		}
	}
}
