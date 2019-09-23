using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FrequencyVisualizer : MonoBehaviour {


	private double[] _freqData;
	public double[] FreqData
	{
		set
		{
			_freqData = value;
		}
	}

	private int _sampleBand;
	public int SampleBand
	{
		set
		{
			_sampleBand = value;
		}
	}

	//int[] _binRange = {200,400,800,1600,2400,3200,4000,4800,5600,6400,7200,8000,8800,9600,10400,11200,12000,12800,13600,14400,15200,16000,16800,17600,18400,19200};
	int[] _binRange = {100,200,300,400,500,600,700,800,900,1000,1200,1400,1600,1800,2000,2400,2800,3200,4000,5000,6000,7000,8000,9000,10000,12000,14000,16000,18000};
	float[] _barCounts = null;

	[SerializeField]
	float _barCountDecayRate = 0.25f;

	[SerializeField]
	bool _visualizeGainBin = true;

	[SerializeField]
	GUIStyle _guiStyle;

	[SerializeField]
	Color _boxCol;

	float canvasStartX;
	float canvasStartY;
	float canvasWidth;
	float canvasHeight;
	int maxBarCount;
	Rect barRect;
	Rect labelRect;
	double[] bins = null;

	private void Awake()
	{
		_barCounts = new float[_binRange.Length+1];
		bins = new double[_barCounts.Length];
		canvasStartX = Screen.width * 0.24f;
		canvasStartY = Screen.height * 0.75f;
		canvasWidth = Screen.width * 0.5f;
		canvasHeight = Screen.height * 0.5f;
		maxBarCount = 25;

		barRect = new Rect (canvasStartX, canvasStartY, canvasWidth / _binRange.Length, canvasHeight / maxBarCount);
		labelRect = new Rect (canvasStartX, canvasStartY + barRect.height * 3, barRect.width, barRect.height);
	}

	public static float Sinerp(float start, float end, float value,int levels = 1)
	{
		if (levels < 1)
			levels = 1;

		float toReturn = value;
		for (int i = 0; i < levels; ++i) {
			toReturn = Mathf.Lerp(start, end, Mathf.Sin(toReturn * Mathf.PI * 0.5f));
		}
		return toReturn;
	}


	int guiCallsAllowed = 2;
	private void Update()
	{
		for (int i = 0; i < _barCounts.Length; ++i) {
			_barCounts[i] -= _barCountDecayRate;
		}
		System.Array.Clear (bins,0,bins.Length);
		AudioAnalyzer.BinFFTOutput (ref _freqData, ref bins, _binRange, _sampleBand);//new double[_binRange.Length];
		guiCallsAllowed = 2;
	}

	public void OnGUI()
	{
		if (guiCallsAllowed <= 0 || bins == null || _freqData == null || _freqData.Length <= 0)
			return;

		--guiCallsAllowed;

		if(!GUI.color.Equals(_boxCol))
			GUI.color = _boxCol;

		//visualize bands

		barRect.x = canvasStartX;
		barRect.y = canvasStartY;
		barRect.width = canvasWidth / _binRange.Length;
		barRect.height = canvasHeight / maxBarCount;

		labelRect.x = canvasStartX;
		labelRect.y = canvasStartY + barRect.height * 3;
		labelRect.width = canvasWidth / _binRange.Length;
		labelRect.height = canvasHeight / maxBarCount;

		double totalEnergy = 0;
		for (int i = 0; i < bins.Length; ++i) {
			totalEnergy += bins [i];
		}

		for (int i = 0; i < bins.Length; ++i) {

			//draw bars
			float barCount = Mathf.Clamp((float)(bins[i]/_freqData.Length),0,maxBarCount);

			barCount = (float)Sinerp (0.01f, 1.0f, barCount,1);//levelsToLerp);
			barCount *= maxBarCount;
			if (barCount > _barCounts [i])
				_barCounts [i] = barCount;

			if (_barCounts [i] > 0) {
				float barRectHeight = barRect.height;
				barRect.height = barRectHeight * _barCounts [i];
				barRect.y -= barRect.height;
				GUI.Box (barRect, "",_guiStyle);
				barRect.height = barRectHeight;
				barRect.y = canvasStartY;
			}

			barRect.x += barRect.width*1.05f;
		}



	}
}
