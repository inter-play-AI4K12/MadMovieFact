using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MadFact
{
    /// <summary>
    /// Background music bed. Plays the supplied tracks in order and loops the whole
    /// sequence (1, 2, 1, 2, ...). Kept quiet so the SFX and the error-tension hum
    /// stay clearly audible on top.
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager I { get; private set; }

        static readonly string[] TrackPaths = { "Audio/Music/Skeletoni", "Audio/Music/BigHelmet" };

        AudioSource _src;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = false;          // looping is handled by the sequence coroutine
            _src.spatialBlend = 0f;
            _src.volume = 0.30f;
            StartCoroutine(PlaySequence());
        }

        IEnumerator PlaySequence()
        {
            var tracks = new List<AudioClip>();
            foreach (var p in TrackPaths)
            {
                var clip = Resources.Load<AudioClip>(p);
                if (clip != null) tracks.Add(clip);
            }
            if (tracks.Count == 0) yield break;

            int i = 0;
            while (true)
            {
                _src.clip = tracks[i];
                _src.Play();
                // Realtime wait so timescale changes never stall the playlist; the small
                // epsilon prevents re-triggering before the clip has fully finished.
                yield return new WaitForSecondsRealtime(tracks[i].length + 0.1f);
                i = (i + 1) % tracks.Count;
            }
        }
    }
}
