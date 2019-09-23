using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GLib.EntityManagement;
using System;
using AJ.Core;
using GLib.AssetManagement;
using ALib.Core;

public enum FrequencyResponseType
{
	None = 0,
	Bass,
	Bass_Mid,
	Mid,
	Mid_Treble,
	Treble,
	Bass_Mid_Treble
}

namespace AJ.WeaponSystems
{
	public abstract class BaseWeaponEntity : BaseAJEntity {

		//TODO, setup a verification system that checks everything that is required for the game to function smoothly
		[SerializeField]
		private string _upgradeAssetUID;
		//private readonly string _upgradeUIDInternal;
		public string UpgradeAssetUID
		{
			get
			{
				return _upgradeAssetUID;
			}
		}

		[SerializeField]
		private string _bulletUID;
		//private readonly string _bulletUIDInternal;
		public string BulletUID
		{
			get
			{
				return _bulletUID;
			}
		}

		[SerializeField]
		private string _activeBulletUID;
		//private readonly string _activeBulletUIDInternal;
		public string ActiveBulletUID
		{
			get
			{
				return _activeBulletUID;
			}
		}

		public bool CanUpgrade
		{
			get
			{
				return !string.IsNullOrEmpty (_upgradeAssetUID);
			}
		}

		[SerializeField]
		private int _minFrequencyResponse = 0;

		[SerializeField]
		private int _maxFrequencyResponse = Constants.MaxBassFrequency;

		[SerializeField]
		private float _baseFireRate;
		//private readonly float _baseFireRateInternal;
		public float BaseFireRate
		{
			get
			{
				return _baseFireRate;
			}
		}


		[SerializeField]
		private float _maxFireRate;
		//private readonly float _maxFireRateInternal;
		public float MaxFireRate
		{
			get
			{
				return _maxFireRate;
			}
		}

		[SerializeField]
		private float _fireRateActual;

		[SerializeField]
		private float _beatDelta;
		//private readonly float _beatDeltaInternal;
		public float BeatDelta
		{
			get
			{
				return _beatDelta;
			}
		}

		private Vector2Int _frequencyRange;
		public Vector2Int FrequencyRange
		{
			get
			{
				return _frequencyRange;
			}
		}

		[SerializeField]
		private Transform[] _bulletSpawnPoints;

		private float _activeWeaponCooldown = 0;


		private float _commonTimer;

		protected override void Awake ()
		{
			base.Awake ();

			_fireRateActual = _baseFireRate;
			_frequencyRange.x = _minFrequencyResponse;
			_frequencyRange.y = _maxFrequencyResponse;

			if(_bulletSpawnPoints == null || _bulletSpawnPoints.Length == 0)
			{
				GameObject sp = new GameObject ("BulletSpawnPoitnDefault");
				sp.transform.SetParent (this.transform, false);
				sp.transform.localPosition = Vector3.zero;
				sp.transform.forward = this.transform.forward;
				_bulletSpawnPoints = new Transform[1];
				_bulletSpawnPoints [0] = sp.transform;
			}
		}

		public FrequencyResponseType GetFrequencySpectrum()
		{
			bool includesBass = _minFrequencyResponse < Constants.MaxBassFrequency;
			bool includesMid = _maxFrequencyResponse > Constants.MaxBassFrequency;
			bool includesTreble = _maxFrequencyResponse > Constants.MaxMidFrequency;


			//TODO :- optimize this
			if (includesBass && includesMid && includesTreble) 
				return FrequencyResponseType.Bass_Mid_Treble;
			
			if (includesBass && includesMid)
				return FrequencyResponseType.Bass_Mid;
			
			if (includesMid && includesTreble)
				return FrequencyResponseType.Mid_Treble;

			if (includesBass)
				return FrequencyResponseType.Bass;

			if (includesMid)
				return FrequencyResponseType.Mid;
			else 
				//only available option
				return FrequencyResponseType.Treble;
		}


		protected override void InitializeEntity ()
		{
			ConstructWeapon (AssetUID);
		}


		public void UpgradeWeapon()
		{
			if (!CanUpgrade)
				return;

			//kill this weapon
			DestroyWeapon (() => {
				//construct its upgrade
				ConstructWeapon (_upgradeAssetUID);
			});
		}

		private void ConstructWeapon(string weaponUID)
		{
			_entityFSM.SetState (EntityStates.Spawning);
		}

		protected int SpawningInit(int status)
		{
			DisplaceBodyFromCenterAnim (false,0);
			_commonTimer = 0;
			return 0;
		}

		protected void SpawningUpdate()
		{
			if (_commonTimer < Constants.WeaponSpawnDestroyTime) {
				_commonTimer += Time.deltaTime;

			} else {
				_commonTimer = Constants.WeaponSpawnDestroyTime;
				_entityFSM.SetState (EntityStates.Existing);
			}

			DisplaceBodyFromCenterAnim (false,_commonTimer / Constants.WeaponSpawnDestroyTime);
		}

		protected int SpawningTerminate(int status)
		{
			return 0;
		}

		protected int ExistingInit(int status)
		{
			TemporalCoordinator.Instance.RegisterStreamEvent (FrequencyRange, StreamListener);
			//TemporalCoordinator.Instance.RegisterBeatEvent (FrequencyRange, BeatDelta,AudioSampler.Instance.FFTBinCount/3, BeatListener);
			BulletPoolManager.Instance.RegisterBulletUID (BulletUID);
			BulletPoolManager.Instance.RegisterBulletUID (ActiveBulletUID);
			return 0;
		}

		protected void ExistingUpdate()
		{
			_commonTimer += Time.deltaTime;
			_activeWeaponCooldown -= Time.deltaTime;
			if (_activeWeaponCooldown < 0)
				_activeWeaponCooldown = 0;
			
			if (_commonTimer > _fireRateActual) {
				_commonTimer = 0;
				Fire ();
			}
		}

		protected int ExistingTerminate(int status)
		{
			TemporalCoordinator.Instance.UnregisterStreamEvent (FrequencyRange, StreamListener);
			//TemporalCoordinator.Instance.UnregisterBeatEvent (FrequencyRange, BeatDelta, BeatListener);
			BulletPoolManager.Instance.UnregisterBulletUID (BulletUID);
			return 0;
		}

		public void FirePublic(bool passive)
		{
			Fire (passive);
		}

		protected virtual void Fire(bool passive = true)
		{
			if (passive)
				return;

			if (!passive) {
				if (_activeWeaponCooldown > 0)
					return;
				
				_activeWeaponCooldown = Constants.MinTimeBetweenActiveFire;
			}
				
			
			string idToUse = passive ? BulletUID : ActiveBulletUID;
			GameObject bullet = null;
			for(int i = 0; i < _bulletSpawnPoints.Length; ++i)
			{
				bullet = BulletPoolManager.Instance.RetrieveBulletFromPool(idToUse);
				bullet.transform.position = _bulletSpawnPoints [i].transform.position;
				bullet.transform.forward = _bulletSpawnPoints [i].transform.forward;
			}

		}

		protected virtual void StreamListener(Vector2Int frequencyRange,float normalizedEnergy,float deltaEnergy = 0)
		{
			_fireRateActual = Mathf.Lerp (BaseFireRate, MaxFireRate, normalizedEnergy);
		}

		protected virtual void BeatListener(Vector2Int frequencyRange,float normalizedEnergy,float deltaEnergy = 0)
		{
			Fire (false);
		}

		public void DestroyWeapon(Action onDestroy)
		{
			_entityFSM.SetState (EntityStates.Dying);
		}



		public override void Reset ()
		{
			
		}
	}
}