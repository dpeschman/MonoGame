// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.IO;

namespace Microsoft.Xna.Framework.Audio
{
    public enum VariationType
    {
        Ordered,
        OrderedFromRandom,
        Random,
        RandomNoImmediateRepeats,
        Shuffle
    };

    public class PlayWaveEvent : ClipEvent
    {
        public readonly SoundBank _soundBank;

        public readonly VariationType _variation;

        public readonly int _loopCount;

        public readonly bool _newWaveOnLoop;

        public readonly int[] _tracks;
        public readonly int[] _waveBanks;
        
        public readonly byte[] _weights;
        public readonly int _totalWeights;

        public float _trackVolume;
        public float _trackPitch;
        public float _trackFilterFrequency;
        public float _trackFilterQFactor;

        public float _clipVolume;
        public float _clipPitch;
        public float _clipReverbMix;

        public readonly Vector4? _filterVar;
        public readonly Vector2? _volumeVar;
        public readonly Vector2? _pitchVar;

        public int _wavIndex;
        public int _loopIndex;

        public SoundEffectInstance _wav;
        public bool _streaming;

        public PlayWaveEvent(   XactClip clip, float timeStamp, float randomOffset, SoundBank soundBank,
                                int[] waveBanks, int[] tracks, byte[] weights, int totalWeights,
                                VariationType variation, Vector2? volumeVar, Vector2? pitchVar, Vector4? filterVar,
                                int loopCount, bool newWaveOnLoop)
            : base(clip, timeStamp, randomOffset)
        {
            _soundBank = soundBank;
            _waveBanks = waveBanks;
            _tracks = tracks;
            _weights = weights;
            _totalWeights = totalWeights;
            _volumeVar = volumeVar;
            _pitchVar = pitchVar;
            _filterVar = filterVar;
            _wavIndex = -1;
            _loopIndex = 0;

            _trackVolume = 1.0f;
            _trackPitch = 0;
            _trackFilterFrequency = 0;
            _trackFilterQFactor = 0;

            _clipVolume = 1.0f;
            _clipPitch = 0;
            _clipReverbMix = 0;

            _variation = variation;
            _loopCount = loopCount;
            _newWaveOnLoop = newWaveOnLoop;
        }

        public override void Play() 
        {
            if (_wav != null)
            {
                if (_wav.State != SoundState.Stopped)
                    _wav.Stop();
                if (_streaming)
                    _wav.Dispose();
				else					
					_wav._isXAct = false;					
                _wav = null;
            }

            Play(true);
        }

        public void Play(bool pickNewWav)
        {
            var trackCount = _tracks.Length;

            // Do we need to pick a new wav to play first?
            if (pickNewWav)
            {
                switch (_variation)
                {
                    case VariationType.Ordered:
                        _wavIndex = (_wavIndex + 1) % trackCount;
                        break;

                    case VariationType.OrderedFromRandom:
                        _wavIndex = (_wavIndex + 1) % trackCount;
                        break;

                    case VariationType.Random:
                        if (_weights == null || trackCount == 1)
                            _wavIndex = XactHelpers.Random.Next() % trackCount;
                        else
                        {
                            var sum = XactHelpers.Random.Next(_totalWeights);
                            for (var i=0; i < trackCount; i++)
                            {
                                sum -= _weights[i];
                                if (sum <= 0)
                                {
                                    _wavIndex = i;
                                    break;
                                }
                            }
                        }
                        break;

                    case VariationType.RandomNoImmediateRepeats:
                    {
                        if (_weights == null || trackCount == 1)
                            _wavIndex = XactHelpers.Random.Next() % trackCount;
                        else
                        {
                            var last = _wavIndex;
                            var sum = XactHelpers.Random.Next(_totalWeights);
                            for (var i=0; i < trackCount; i++)
                            {
                                sum -= _weights[i];
                                if (sum <= 0)
                                {
                                    _wavIndex = i;
                                    break;
                                }
                            }

                            if (_wavIndex == last)
                                _wavIndex = (_wavIndex + 1) % trackCount;
                        }
                        break;
                    }

                    case VariationType.Shuffle:
                        // TODO: Need some sort of deck implementation.
                        _wavIndex = XactHelpers.Random.Next() % trackCount;
                        break;
                };
            }

            _wav = _soundBank.GetSoundEffectInstance(_waveBanks[_wavIndex], _tracks[_wavIndex], out _streaming);
            if (_wav == null)
            {
                // We couldn't create a sound effect instance, most likely
                // because we've reached the sound pool limits.
                return;
            }

            // Do all the randoms before we play.
            if (_volumeVar.HasValue)
                _trackVolume = _volumeVar.Value.X + ((float)XactHelpers.Random.NextDouble() * _volumeVar.Value.Y);
            if (_pitchVar.HasValue)
                _trackPitch = _pitchVar.Value.X + ((float)XactHelpers.Random.NextDouble() * _pitchVar.Value.Y);
            if (_clip.FilterEnabled)
            {
                if (_filterVar.HasValue)
                {
                    _trackFilterFrequency = _filterVar.Value.X + ((float)XactHelpers.Random.NextDouble() * _filterVar.Value.Y);
                    _trackFilterQFactor = _filterVar.Value.Z + ((float)XactHelpers.Random.NextDouble() * _filterVar.Value.W);
                }
                else
                {
                    _trackFilterFrequency = _clip.FilterFrequency;
                    _trackFilterQFactor = _clip.FilterQ;                
                }
            }
 
            // This is a shortcut for infinite looping of a single track.
            _wav.IsLooped = _loopCount == 255 && trackCount == 1;

            // Update all the wave states then play.
            UpdateState();
            _wav.Play();
        }

        public override void Stop()
        {
            if (_wav != null)
            {
                _wav.Stop();
                if (_streaming)
                    _wav.Dispose();
				else
                	_wav._isXAct = false;				
                _wav = null;
            }
            _loopIndex = 0;
        }

        public override void Pause() 
        {
            if (_wav != null)
                _wav.Pause();
        }

        public override void Resume()
        {
            if (_wav != null && _wav.State == SoundState.Paused)
                _wav.Resume();
        }

        public override void SetTrackVolume(float volume)
        {
            _clipVolume = volume;
            if (_wav != null)
                _wav.Volume = _trackVolume * _clipVolume;
        }

        public override void SetTrackPan(float pan)
        {
            if (_wav != null)
                _wav.Pan = pan;
        }

        public override void SetState(float volume, float pitch, float reverbMix, float? filterFrequency, float? filterQFactor)
        {
            _clipVolume = volume;
            _clipPitch = pitch;
            _clipReverbMix = reverbMix;

            // The RPC filter overrides the randomized track filter.
            if (filterFrequency.HasValue)
                _trackFilterFrequency = filterFrequency.Value;
            if (filterQFactor.HasValue)
                _trackFilterQFactor = filterQFactor.Value;

            if (_wav != null)
                UpdateState();
        }

        public void UpdateState()
        {
            _wav.Volume = _trackVolume * _clipVolume;
            _wav.Pitch = _trackPitch + _clipPitch;

            if (_clip.UseReverb)
                _wav.PlatformSetReverbMix(_clipReverbMix);
            if (_clip.FilterEnabled)
                _wav.PlatformSetFilter(_clip.FilterMode, _trackFilterQFactor, _trackFilterFrequency);
        }

        public override void SetFade(float fadeInDuration, float fadeOutDuration)
        {
            // TODO
        }

        public override bool Update(float dt)
        {
            if (_wav != null && _wav.State == SoundState.Stopped)
            {
                // If we're not looping or reached our loop 
                // limit then we can stop.
                if (_loopCount == 0 || _loopIndex >= _loopCount)
                {
                    if (_streaming)
                        _wav.Dispose();
					else
	                    _wav._isXAct = false;						
                    _wav = null;
                    _loopIndex = 0;
                }
                else
                {
                    // Increment the loop count if it isn't infinite.
                    if (_loopCount != 255)
                        ++_loopIndex;

                    // Play the next track.
                    Play(_newWaveOnLoop);
                }
            }

            return _wav != null;
        }
    }
}

