using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ALib.Core;
using GLib.AssetManagement;
using AJ.Utilities;
using AJ.WeaponSystems;

public class DebugScript : MonoBehaviour {

	[SerializeField]
	private AudioClip[] _debugClips;

	private double[] _fftRawBins;

	private double[] _fftSegregated;

	private Vector2Int[] _frequenciesToMonitor = 
	{
		new Vector2Int(0,32),
		new Vector2Int(32,64),
		new Vector2Int(64,128),
		new Vector2Int(128,256),
		new Vector2Int(256,512),
		new Vector2Int(512,1024),
		new Vector2Int(1024,2048),
		new Vector2Int(2048,4096),
		new Vector2Int(4096,8192),
		new Vector2Int(8192,16384),
		new Vector2Int(16384,22050)
		/*new Vector2Int(0,100),
		new Vector2Int(100,200),
		new Vector2Int(200,300),
		new Vector2Int(300,400),
		new Vector2Int(400,500),
		new Vector2Int(500,600),
		new Vector2Int(600,700),
		new Vector2Int(700,800),
		new Vector2Int(800,900),
		new Vector2Int(900,1000),
		new Vector2Int(1000,1200),
		new Vector2Int(1200,1400),
		new Vector2Int(1400,1600),
		new Vector2Int(1600,1800),
		new Vector2Int(1800,2000),
		new Vector2Int(2000,2400),
		new Vector2Int(2400,2800),
		new Vector2Int(2800,3200),
		new Vector2Int(3200,4000),
		new Vector2Int(4000,5000),
		new Vector2Int(5000,6000),
		new Vector2Int(6000,7000),
		new Vector2Int(7000,8000),
		new Vector2Int(8000,9000),
		new Vector2Int(9000,10000),
		new Vector2Int(10000,12000),
		new Vector2Int(12000,14000),
		new Vector2Int(14000,16000),
		new Vector2Int(16000,18000),
		new Vector2Int(18000,24000)*/
	};


	private Rect _commonRect;
	private float[] _barHeights;
	public float _decayRate = 0.01f;

	[SerializeField]
	private float _bpm;

	private PLPDetector _detector;

	[SerializeField]
	private Texture2D _debugNovTex;
	[SerializeField]
	private Texture2D _debugPLPTex;
	[SerializeField]
	private Texture2D _debugTGTex;

	public void Awake()
	{
		AudioSampler.InstantiateSampler (AudioSamplerType.Local);
		_commonRect = new Rect ();
		_fftSegregated = new double[_frequenciesToMonitor.Length];
		_barHeights = new float[_fftSegregated.Length];

	}


	public void Update()
	{
		if (AudioSampler.Instance.AudioPlaybackState == AudioPlaybackState.Playing) {
			double[] _dAudioSamples = AudioSampler.Instance.GetSamples ();

			if (_detector != null)
				_detector.UpdatePLP (ref _dAudioSamples, AudioSampler.Instance.ChannelCount, AudioSampler.Instance.ClipFrequency);
			
			_fftRawBins = AudioSampler.Instance.GetFFTOutputBins (ref _dAudioSamples);
			System.Array.Clear (_fftSegregated, 0, _fftSegregated.Length);
			AudioSampler.BinFFTOutput (ref _fftRawBins, ref _fftSegregated, 2, AudioSampler.Instance.ClipFrequency, _frequenciesToMonitor);

			AudioSampler.NormalizeFFTOutput (ref _fftSegregated,AudioSampler.Instance.ClipFrequency);
			for (int i = 0; i < _fftSegregated.Length; ++i) {
				float barCount = (float)_fftSegregated[i];
				if (barCount > _barHeights [i])
					_barHeights [i] = barCount;

				if (_barHeights [i] > 0) {
					_barHeights [i] -= Mathf.Min(_decayRate,_barHeights[i]);
				}
			}
		}
	}

	public void SpawnWeapon()
	{
		GameObject wep = PrefabInstantiator.Instance.InstantiatePrefab ("dcube1");
		wep.transform.position = transform.position;
		wep.transform.forward = Vector3.forward;
	}

	public void UpgradeWeapon()
	{
		
	}

	public void DestroyWeapon()
	{
		
	}



	public void OnGUI()
	{
		_commonRect.x = 0;
		_commonRect.y = 0;
		_commonRect.width = Screen.width * 0.05f;
		_commonRect.height = _commonRect.width * 0.5f;


		_commonRect.x += _commonRect.width * 0.5f;
		if (GUI.Button (_commonRect,"SpawnWeapon")) {
			SpawnWeapon ();
		}
		_commonRect.x += _commonRect.width;
		if (GUI.Button (_commonRect,"UpgradeWeapon")) {
			UpgradeWeapon ();
		}

		_commonRect.x += _commonRect.width;
		if (GUI.Button (_commonRect,"DestroyWeapon")) {
			DestroyWeapon ();
		}

		if (_detector != null) {
			_commonRect.x += _commonRect.width;
			GUI.Label (_commonRect, Mathf.FloorToInt((float)_detector.BPM).ToString ());
			_commonRect.x += _commonRect.width;
			GUI.Label (_commonRect, Mathf.RoundToInt((float)(_detector.BPM/60.0f)).ToString ());
			_commonRect.x -= _commonRect.width * 2;
		}

		_commonRect.x = 0;
		_commonRect.y += _commonRect.height;
		for (int i = 0; i < _debugClips.Length; ++i) {
			if(GUI.Button(_commonRect,_debugClips[i].name))
			{
				AudioSampler.Instance.Initialize (_debugClips [i]);
				AudioSampler.Instance.SetPlaybackState (AudioPlaybackState.Playing);
				if (_detector != null)
					_detector.ClearResources ();
				_detector = new PLPDetector ((float sin)=>
					{
						BaseWeaponEntity we = FindObjectOfType<BaseWeaponEntity>();
						GameObject temp = we.gameObject;
						Vector3 pos = temp.transform.position;
						pos.y = sin;
						temp.transform.position = pos;
						we.FirePublic(false);
					},ref _debugNovTex,ref _debugPLPTex,ref _debugTGTex,4, AudioSampler.Instance.FFTBinCount);
			}
			_commonRect.y += _commonRect.height;
		}

		_commonRect.y = _commonRect.height;
		_commonRect.x = _commonRect.width;
		if (GUI.Button (_commonRect, "Play")) {
			AudioSampler.Instance.SetPlaybackState (AudioPlaybackState.Playing);
		}
		_commonRect.x += _commonRect.width;
		if (GUI.Button (_commonRect, "Pause")) {
			AudioSampler.Instance.SetPlaybackState (AudioPlaybackState.Paused);
		}
		_commonRect.x += _commonRect.width;
		if (GUI.Button (_commonRect, "Stop")) {
			AudioSampler.Instance.SetPlaybackState (AudioPlaybackState.Stop);
		}

		if (_fftRawBins == null || _fftSegregated == null || _fftSegregated.Length == 0)
			return;

		int canvasStartX = (int)(Screen.width * 0.2f);
		int canvasWidth = (int) (Screen.width * 0.6f);
		int canvasStartY =  (int)(Screen.height * 0.8f);
		int casvasHeight =  (int)(Mathf.Min(canvasWidth,Screen.height * 0.6f));


		_commonRect.x = canvasStartX;
		_commonRect.y = canvasStartY;
		_commonRect.width = canvasWidth / _fftSegregated.Length;

		/*double maxEnergyPerBin = 0;
		for (int i = 0; i < _fftSegregated.Length; ++i) {
			maxEnergyPerBin += _fftSegregated [i];
		}
		maxEnergyPerBin /= _fftSegregated.Length;*/

		for (int i = 1; i < _fftSegregated.Length; ++i) {
			
			_commonRect.height = _barHeights[i] * casvasHeight;

			if (_commonRect.height > 0) {
				_commonRect.y -= _commonRect.height;
				GUI.Box (_commonRect,"");
				_commonRect.y += _commonRect.height;
			}
			_commonRect.x += _commonRect.width;

		}

		_commonRect.x = canvasStartX;
		_commonRect.y = canvasStartY;
		_commonRect.height = Screen.height * 0.5f;
		for (int i = 0; i < _fftSegregated.Length; ++i) {
			GUI.Label (_commonRect, _fftSegregated [i].ToString ());
			_commonRect.x += _commonRect.width;
		}

	}

}
