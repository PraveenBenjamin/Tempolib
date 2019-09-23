using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using AJ.Utilities;

public class EqTestScript : MonoBehaviour {

	private AudioClip _clip;

	[SerializeField]
	private AudioSource _player;

	[SerializeField]
	private FrequencyVisualizer _eq;

	SphereJellifier _sphere;

	[SerializeField]
	private int _fftType = 2;


	[SerializeField]
	private int _sampleCount = 1024;

	private float[] _audioSamples;
	private double[] _dAudioSamples;

	[SerializeField]
	private AudioClip[] _clips;

	Rect buttonRect = new Rect (0, 0, Screen.width * 0.1f, Screen.width * 0.025f);





	private void SetAudioClip(int clipIndex)
	{
		AudioClip clip = _clips [clipIndex];
		clip.LoadAudioData ();
		_sampleCount = Utilities.NextNearestPowerOf2 (clip.frequency / 60) * clip.channels;
		_audioSamples = new float[_sampleCount];
		_dAudioSamples = new double[(int)(_sampleCount/2)];
		_eq.SampleBand = clip.frequency/2;
		_player.clip = clip;
		_player.Play ();

		_clip = clip;

		System.GC.Collect ();
	}

	private void Start()
	{
		Screen.sleepTimeout = SleepTimeout.NeverSleep;
		SetAudioClip (0);
		_sphere = FindObjectOfType<SphereJellifier> ();
		_sphere.Init (_clip.frequency / 2);
		buttonRect = new Rect (0, 0, Screen.width * 0.1f, Screen.width * 0.025f);
	}




	// Update is called once per frame
	void Update () {
		if (_player.isPlaying) {
			float t = _player.time;
			_clip.GetData (_audioSamples, _player.timeSamples);
			//assume there are only 2 channels for now.
			for (int i = 0; i < _audioSamples.Length/2; ++i) {
				//bin left and right channel
				//_dAudioSamples [i] = (double)(Mathf.Abs(_audioSamples [i*2]));
				_dAudioSamples [i] = (double)(_audioSamples [i*2]);
				_dAudioSamples [i] += (double)(_audioSamples [(i*2) + 1]);
				//_dAudioSamples [i] *= 0.5f;

			}
			//_dAudioSamples = Array.ConvertAll (_audioSamples, x => (double)x);
			AudioAnalyzer._instance.TimetoFreq (ref _dAudioSamples, _fftType);
			_eq.FreqData = _dAudioSamples;

			_sphere.UpdateJellifier (ref _dAudioSamples);
		}
	}

	private void OnGUI()
	{
		buttonRect.x = 0;
		buttonRect.y = 0;
		buttonRect.width = Screen.width * 0.1f;
		buttonRect.height = Screen.width * 0.025f;

		for (int i = 0; i < _clips.Length; ++i) {
			if (GUI.Button (buttonRect, _clips [i].name)) {
				SetAudioClip (i);
			}
			buttonRect.y += Screen.width * 0.025f;
		}
	}
}
