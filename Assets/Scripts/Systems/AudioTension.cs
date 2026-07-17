using UnityEngine;

namespace MadFact
{
    /// <summary>
    /// The "Tension" mechanic. All sound is synthesized procedurally so the project
    /// ships with zero audio files.
    ///  - Mathematical error -> low-pass brown-noise hum that swells with the error.
    ///  - High error adds pitch wow-and-flutter (a motor struggling to turn).
    ///  - Perfect match -> silence, punctuated by a satisfying VHS clunk.
    ///  - Cash register / coin / refund buzzer for the economy.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioTension : MonoBehaviour
    {
        public static AudioTension I { get; private set; }

        const int Freq = 44100;

        AudioSource _noise;   // looping tension hum
        AudioSource _sfx;     // one-shots
        AudioClip _brown, _clunk, _chaching, _coin, _buzzer, _beep, _whir;

        float _targetError;   // 0 (perfect) .. ~4 (awful)
        float _curVol;
        float _flutterPhase;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);

            _brown    = MakeBrownNoise(2.0f);
            _clunk    = MakeClunk();
            _chaching = MakeChaChing();
            _coin     = MakeCoin();
            _buzzer   = MakeBuzzer();
            _beep     = MakeBeep();
            _whir     = MakeWhir();

            _noise = GetComponent<AudioSource>();
            _noise.clip = _brown;
            _noise.loop = true;
            _noise.volume = 0f;
            _noise.playOnAwake = false;
            _noise.spatialBlend = 0f;
            _noise.Play();

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;
        }

        void Update()
        {
            // map error -> volume. silence below a small deadzone.
            float e = _targetError;
            float wanted = Mathf.InverseLerp(0.10f, 2.2f, e);   // 0..1
            wanted = Mathf.Clamp01(wanted) * 0.55f;
            _curVol = Mathf.MoveTowards(_curVol, wanted, Time.unscaledDeltaTime * 1.6f);
            _noise.volume = _curVol;

            // wow & flutter only kicks in at high error
            float flutterAmt = Mathf.Clamp01(Mathf.InverseLerp(1.2f, 3.0f, e));
            if (flutterAmt > 0.001f)
            {
                _flutterPhase += Time.unscaledDeltaTime * (6f + 10f * flutterAmt);
                _noise.pitch = 1f + Mathf.Sin(_flutterPhase) * 0.18f * flutterAmt;
            }
            else _noise.pitch = Mathf.MoveTowards(_noise.pitch, 1f, Time.unscaledDeltaTime * 2f);
        }

        // ---- Public control ----------------------------------------------
        /// <summary>Set the current mathematical error (0 = perfect). Drives the hum.</summary>
        public void SetError(float error) => _targetError = Mathf.Max(0f, error);
        public void Silence() => _targetError = 0f;

        public void Clunk()    { _sfx.PlayOneShot(_clunk, 0.9f); }
        public void ChaChing() { _sfx.PlayOneShot(_chaching, 0.8f); }
        public void Coin()     { _sfx.PlayOneShot(_coin, 0.7f); }
        public void Buzzer()   { _sfx.PlayOneShot(_buzzer, 0.7f); }
        public void Beep()     { _sfx.PlayOneShot(_beep, 0.5f); }
        public void Whir()     { _sfx.PlayOneShot(_whir, 0.6f); }

        // ---- Synthesis ----------------------------------------------------
        static AudioClip Make(string name, float seconds, System.Func<int, float, float> sample, bool loop = false)
        {
            int n = Mathf.Max(1, (int)(seconds * Freq));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i, i / (float)Freq), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Freq, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeBrownNoise(float seconds)
        {
            int n = (int)(seconds * Freq);
            var data = new float[n];
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = Random.Range(-1f, 1f);
                last = (last + 0.02f * w);
                last = Mathf.Clamp(last, -1f, 1f) * 0.998f;
                data[i] = last * 3.2f;
            }
            // normalise + seam crossfade for clean looping
            float peak = 0.0001f; for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float g = 0.8f / peak;
            int fade = (int)(0.05f * Freq);
            for (int i = 0; i < n; i++)
            {
                float v = data[i] * g;
                if (i < fade) { float t = i / (float)fade; v = Mathf.Lerp(data[n - fade + i] * g, v, t); }
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create("brown", n, 1, Freq, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeClunk() => Make("clunk", 0.35f, (i, t) =>
        {
            float env = Mathf.Exp(-t * 16f);
            float body = Mathf.Sin(2f * Mathf.PI * 68f * t) * 0.7f;
            float click = (t < 0.02f) ? Random.Range(-1f, 1f) * 0.6f : 0f;
            return (body + click) * env;
        });

        static AudioClip MakeChaChing() => Make("chaching", 0.55f, (i, t) =>
        {
            // ratchet then two bright bells
            float ratchet = (t < 0.07f) ? (Mathf.Repeat(t * 90f, 1f) > 0.5f ? 0.4f : -0.4f) * 0.5f : 0f;
            float b1 = t > 0.04f ? Mathf.Sin(2f * Mathf.PI * 1318.5f * t) * Mathf.Exp(-(t - 0.04f) * 6f) : 0f;
            float b2 = t > 0.14f ? Mathf.Sin(2f * Mathf.PI * 1760f * t) * Mathf.Exp(-(t - 0.14f) * 6f) : 0f;
            float shimmer = t > 0.14f ? Mathf.Sin(2f * Mathf.PI * 2637f * t) * Mathf.Exp(-(t - 0.14f) * 9f) * 0.3f : 0f;
            return ratchet + (b1 + b2) * 0.5f + shimmer;
        });

        static AudioClip MakeCoin() => Make("coin", 0.18f, (i, t) =>
        {
            float env = Mathf.Exp(-t * 18f);
            float a = Mathf.Sin(2f * Mathf.PI * 2349f * t);
            float b = Mathf.Sin(2f * Mathf.PI * 3136f * t) * 0.5f;
            return (a + b) * env * 0.7f;
        });

        static AudioClip MakeBuzzer() => Make("buzzer", 0.4f, (i, t) =>
        {
            float env = t < 0.34f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.34f) / 0.06f);
            float s1 = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 120f * t));
            float s2 = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 119f * t)); // beating roughness
            return (s1 * 0.5f + s2 * 0.5f) * 0.5f * env;
        });

        static AudioClip MakeBeep() => Make("beep", 0.06f, (i, t) =>
        {
            float env = Mathf.Sin(Mathf.PI * (t / 0.06f));
            return Mathf.Sin(2f * Mathf.PI * 880f * t) * env * 0.6f;
        });

        static AudioClip MakeWhir() => Make("whir", 0.5f, (i, t) =>
        {
            // a motor spinning up (rising saw)
            float f = Mathf.Lerp(60f, 240f, t / 0.5f);
            float env = Mathf.Sin(Mathf.PI * (t / 0.5f));
            float saw = Mathf.Repeat(f * t, 1f) * 2f - 1f;
            return saw * env * 0.4f;
        });
    }
}
