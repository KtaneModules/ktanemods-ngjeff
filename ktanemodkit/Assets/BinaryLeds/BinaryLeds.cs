using UnityEngine;

public class BinaryLeds : MonoBehaviour
{
	public GameObject[] leds;
	public Material LedOffMaterial;
	public Material LedOnMaterial;
	public KMSelectable[] wires;

	//	private int [] testSequence = {1,2,3,4,5,6,7,8,9,10};
	// DONE list:
	// Don't cut an already cut wire
	// Speed up when a wrong wire is cut
	// add an SVD to the manual page
	// add random initial offset, so even if a module is same sequence it looks different

	// TODO LIST:
	// 1.Serial # increments LED value?
	// blink the lights?--they go off towards the end of the period allotted for it.

	private const float PERCENT_INTERVAL_SHOWN = 1.0f;  // How long the lights are on in the interval.
	private const int NUM_SEQUENCES = 8;
	private const int SEQUENCE_LENGTH = 14;
	private float BLINK_DELAY = 1.0f;  // The time it takes to move from one number to another.  

	private int [,] sequences = {{17,15,6,2,24,8,26,25,21,24,1,15,18,8},
		{18,15,15,31,12,6,19,21,11,16,19,2,1,29},
		{8,25,1,15,20,15,9,3,6,24,1,24,5,26},
		{21,27,6,12,27,20,20,1,19,15,3,13,9,28},
		{3,21,14,22,7,28,16,27,22,17,26,2,31,15},
		{8,22,30,19,1,25,31,16,9,7,6,13,9,7},
		{5,18,12,7,5,12,31,16,10,10,17,9,9,25},
		{4,20,18,25,20,4,24,29,17,16,12,16,29,19},
	};
	//Solutions are 0=red, 1=green, 2=blue
	private int [,] solutions = {{5,3,7},
		{11,8,4},
		{9,1,2},
		{8,12,7},
		{10,5,8},
		{0,10,6},
		{2,5,9},
		{9,5,10},
	};

	private int sequenceIndex;  //the sequence we're using on this instantiation of the module
	private float counterStartTime; 
	private int initialOffset;

	private enum WireNames
	{
		RED = 0,
		GREEN = 1,
		BLUE = 2
	}

	private WireNames [] colorIndices = { WireNames.RED, WireNames.GREEN, WireNames.BLUE};
	private Color [] wireColors = { new Color(1,0,0,1), new Color(0,1,0,1), new Color (0,0,1,1) };

    int correctIndex;
    bool isActivated = false;

    void Start()
    {
        Init();
        GetComponent<KMBombModule>().OnActivate += ActivateModule;
    }

    void Init()
    {
		// Set up which pattern we're using, along with what solutions exist
		sequenceIndex = Random.Range(0, NUM_SEQUENCES);
		initialOffset = Random.Range (0, SEQUENCE_LENGTH);
		Debug.Log ("Using sequence number " + sequenceIndex + " with offset " + initialOffset);

		// Set up the LED blink pattern.
		counterStartTime = Time.time;
		int timeIndex = GetIndexFromTime (Time.time, BLINK_DELAY);
		ApplyToLeds (sequences[sequenceIndex, timeIndex]);

		// Prep the wires
		ShuffleColorArray (colorIndices);
		for (int i = 0; i < wires.Length; i++) {
			// Set up an object we can pass down to our delegates.  Must be by reference
			WireDelegateInfo info = new WireDelegateInfo ();
			info.wire = wires [i];
			info.color = colorIndices [i];
			info.isCut = false;

			SetWireColor (wires [i], wireColors [(int)colorIndices[i]]);
			wires[i].OnInteract += delegate () { SnipWire(info); OnCutLogic(info); return false; };
		}
	}

	void Update()
	{
		// Only on updates, we'll hide the LEDs if we're close to the end of an interval
		float percentOfIntervalDone = ((Time.time - counterStartTime) % BLINK_DELAY) / BLINK_DELAY;
		if (percentOfIntervalDone > PERCENT_INTERVAL_SHOWN) {
			ZeroLeds ();
		} else {
			int timeIndex = GetIndexFromTime (Time.time, BLINK_DELAY);
			ApplyToLeds (sequences [sequenceIndex, timeIndex]);
		}
	}

	// Given a time, calculate the index that should be used.
	int GetIndexFromTime(float currentTime, float timePerIndex){
		int elapsedIntervals = (int) ((currentTime - counterStartTime) / timePerIndex);
		elapsedIntervals += initialOffset; // Shift over by a pre-set number in the beginning.

		// the first N intervals are 0-(N-1), then N down to 1.  It cycles after that.
		if ((elapsedIntervals / (SEQUENCE_LENGTH - 1) % 2) == 0) {
			return elapsedIntervals % (SEQUENCE_LENGTH - 1);
		} else {
			return (SEQUENCE_LENGTH - 1) - (elapsedIntervals % (SEQUENCE_LENGTH - 1));
		}
	}

    void ActivateModule()
    {
        isActivated = true;
    }

	void SnipWire(WireDelegateInfo wireInfo)
	{
		Renderer wireRend = wireInfo.wire.GetComponent<Renderer>();
		wireRend.enabled = false;
		GetBrokenWireOfWire(wireInfo.wire).GetComponent<MeshRenderer>().enabled = true;
	}

	void OnCutLogic(WireDelegateInfo info)
    {
		if (info.isCut) { // Don't do anything if you already cut the wire
			return;
		}

		int timeIndex = GetIndexFromTime (Time.time, 1);
		Debug.Log("Cutting wire " + info.color + ".  Required time index is " + solutions[sequenceIndex, (int)info.color] + ", current time is " + timeIndex);
		if (!isActivated)
		{
			Debug.Log("Cut wire before module has been activated!");
			GetComponent<KMBombModule>().HandleStrike();
			ReduceBlinkDelay ();
		}
		else
		{
			if (solutions[sequenceIndex, (int)info.color] == timeIndex)
			{
				GetComponent<KMBombModule>().HandlePass();
			}
			else
			{
				GetComponent<KMBombModule>().HandleStrike();
				ReduceBlinkDelay ();
			}
		}

        GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSnip, transform);
        GetComponent<KMSelectable>().AddInteractionPunch();
		info.isCut = true;
    }

	// Reduce in two stages.  First to .66, and then to .5.  Nothing after that.
	void ReduceBlinkDelay()
	{
		if (this.BLINK_DELAY > 0.9f) {
			this.BLINK_DELAY = .66f;
		} else {
			this.BLINK_DELAY = .5f;
		}
	}

	void ZeroLeds()
	{
		for (int i = 4; i >= 0; i--) {
			Renderer rend = leds[i].GetComponent<Renderer>();
			rend.material = LedOffMaterial;
		}
	}

	void ApplyToLeds(int num)
	{
		if (num > 31) {
			Debug.Log ("Error, number out of range");
			num = 0;
		}

		for (int i = 4; i >= 0; i--) {
			Renderer rend = leds[i].GetComponent<Renderer>();
			if (num % 2 == 1) {
				rend.material = LedOnMaterial;
			} else {
				rend.material = LedOffMaterial;
			}
			num = num / 2;
		}
	}

	void SetWireColor(KMSelectable wire, Color color)
	{
		// set the wire model
		Renderer rend = wire.GetComponent<Renderer>();
		rend.material.SetColor ("_Color", color);

		// set the broken wire model as well
		GetBrokenWireOfWire(wire).GetComponent<MeshRenderer>().material.SetColor("_Color", color);
	}

	void ShuffleColorArray(WireNames[] colorArray) {
		WireNames temp;
		int chosenColorIndex = 0;
		for (int i = 0; i < colorArray.Length; i++) {
			chosenColorIndex = GetRandomInt (i, colorArray.Length);
			temp = colorArray[i];  // swap it in.
			colorArray [i] = colorArray [chosenColorIndex];
			colorArray [chosenColorIndex] = temp;
		}
	}


	// access the BrokenWire gameObject of a wire.
	GameObject GetBrokenWireOfWire(KMSelectable wire) {
		Debug.Assert (wire.gameObject.transform.childCount == 2, "Wires need to have a highlight and broken wire object");
		return wire.gameObject.transform.GetChild (1).gameObject;
	}

	//retrieve an integer from [inclusiveMin, exclusiveMax) 
	int GetRandomInt(int inclusiveMin, int exclusiveMax)
	{
		int answer;
		do
		{
			answer = (int) Random.Range(inclusiveMin, exclusiveMax); // grab a color to finalize in this spot.
		} while(answer == exclusiveMax); // If we happen to hit the actual end of the array, just reroll.

		return answer;
	}

	// Class for sharing info into wire delegates
	class WireDelegateInfo
	{
		public KMSelectable wire;
		public WireNames color;
		public bool isCut;
	}
}
