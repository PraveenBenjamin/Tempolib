using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SphereJellifier : MonoBehaviour {

	private MeshFilter _filter;
	private Mesh _toManipulate;
	[SerializeField]
	private Material _skyboxMat;
	private int sampleRate = 0;
	int[] _binRange = {100,200,300,400,500,600,700,800,900,1000,1200,1400,1600,1800,2000,2400,2800,3200,4000,5000,6000,7000,8000,9000,10000,12000,14000,16000,18000};
	Vector3[] verts = null;
	Color[] colors = null;

	public void Init(int sampleRate)
	{
		this.sampleRate = sampleRate;
		_filter = GetComponent<MeshFilter> ();
		_toManipulate = _filter.mesh;
		_toManipulate.colors = new Color[_toManipulate.vertexCount];
		verts = _toManipulate.vertices;
		colors = _toManipulate.colors;
		//_skyboxMat = Camera.main.GetComponent<Skybox> ().GetComponent<Renderer> ().sharedMaterial;
	}

	public void UpdateJellifier(ref double[] fftOutput)
	{
		int batchSize = fftOutput.Length / _binRange.Length;
		double[] bins = null;
		AudioAnalyzer.BinFFTOutput (ref fftOutput,ref bins, _binRange, sampleRate);

		for (int i = 0; i < _toManipulate.vertexCount-1; ++i) {
			for (; i % batchSize < batchSize && i < _toManipulate.vertexCount; ++i) {
				
				float distToBe = (float)((bins[i/batchSize]/(float)fftOutput.Length));
				distToBe = Mathf.Clamp (distToBe, 0.01f, float.MaxValue);
				distToBe += 0.5f;
				Vector3 currentDist = verts [i];
				if (currentDist.magnitude != distToBe) {
					verts [i] = Vector3.Lerp (Vector3.zero, currentDist.normalized, distToBe);
					colors [i] = Color.Lerp (Color.green * 0.6f, Color.red*2, distToBe-0.5f);
				}
			}
		}
		_toManipulate.colors = colors;
		_toManipulate.vertices = verts;

		Vector3 rot = _filter.gameObject.transform.rotation.eulerAngles;
		rot.y += Time.deltaTime * 20;
		_filter.gameObject.transform.rotation = Quaternion.Euler (rot);
		float normalizedBassEnergy = Mathf.Clamp01 ((float)(bins [0] * 0.75f + bins [1] * 0.25f) / fftOutput.Length);
		_skyboxMat.SetColor("_SkyTint", Color.Lerp (Color.yellow, Color.red*1.5f,normalizedBassEnergy ));
		_skyboxMat.SetColor("_GroundColor", Color.Lerp (Color.green*0.25f, Color.blue*0.25f, normalizedBassEnergy));
		_skyboxMat.SetFloat("_Exposure", Mathf.Clamp (normalizedBassEnergy,0.25f,1.0f));
	}
}
