// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.IO;

namespace Microsoft.Xna.Framework.Audio
{
    public class XactClip
    {
        public readonly float _defaultVolume;
        public float _volumeScale;
        public float _volume;

        public readonly ClipEvent[] _events;
        public float _time;
        public int _nextEvent;

        public readonly bool FilterEnabled;
        public readonly FilterMode FilterMode;
        public readonly float FilterQ;
        public readonly ushort FilterFrequency;

        public readonly bool UseReverb;
        //public readonly int[] RpcCurves;

        public XactClip (SoundBank soundBank, BinaryReader clipReader, bool useReverb)
        {
#pragma warning disable 0219
            Console.WriteLine($"  reading clip at pos {clipReader.BaseStream.Position:x}");
            State = SoundState.Stopped;

            UseReverb = useReverb;
            //clip 2
            //00007cc0:           b40b 7d00 0011 0220 4eb4 1c7d  .. N..}.... N..}
            //00007cd0: 0000 1102 204e b42d 7d00 0011 0220 4eb4  .... N.-}.... N.

            //clip 1:  b4 fa7c 0000 1102 204e
            //clip 2:  b4 0b7d 0000 1102 204e
            //            ^-------^ clip offset
            var volumeDb = XactHelpers.ParseDecibels(clipReader.ReadByte()); //b4
            _defaultVolume = XactHelpers.ParseVolumeFromDecibels(volumeDb);
            var clipOffset = clipReader.ReadUInt32(); //fa7c 0000
                                                      //0b7d 0000

            // Read the filter info.
            var filterQAndFlags = clipReader.ReadUInt16();//0x1102 // ?
            FilterEnabled = (filterQAndFlags & 1) == 1;//?
            FilterMode = (FilterMode)((filterQAndFlags >> 1) & 3);
            FilterQ = (filterQAndFlags >> 3) * 0.01f;
            FilterFrequency = clipReader.ReadUInt16();//204e

            var oldPosition = clipReader.BaseStream.Position;
            clipReader.BaseStream.Seek(clipOffset, SeekOrigin.Begin);
            //more clip details starting with clip 1
            //00007cf0:                          0101 0000 2000  N.`}.... N.... .
            //00007d00: 00ff 0c05 003f ff00 00                   .....?.........

            //clip 2 starts 0b7d 0000?              
            //00007d00:                            01 0100 0020  .....?.........
            //00007d10: 0000 ff0c 0600 3fff 0000 0000 0101 0000  ......?.........

            //0101 0000 2000 00ff 0c05 003f ff00 00
            //0101 0000 2000 00ff 0c06 003f ff00 00
            //                       ^ track ind
            //clip 7
            //0101 0000 2000 00ff 0c04 003f ff00 00
            var numEvents = clipReader.ReadByte(); //01
            _events = new ClipEvent[numEvents];
            
            for (var i=0; i<numEvents; i++) 
            {
                var eventInfo = clipReader.ReadUInt32();//01 0000 20 = 0x20000001
                var randomOffset = clipReader.ReadUInt16() * 0.001f; //0000

                // TODO: eventInfo still has 11 bits that are unknown!
                // 2000 0001
                // ...  0 0 0 0 0 0 0 1
                //    & 0 0 0 1 1 1 1 1 = 1
                var eventId = eventInfo & 0x1F; //1
                // 2000 0001
                // 0 0 1 0 0 0 0 0 0 0 0 0 0 0 0 0 : 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1
                // >>5     0 0 1 0 0 0 0 0 0 0 0 0 0 0 0 0 : 0 0 0 0 0 0 0 0 0 0 0 0
                var timeStamp = ((eventInfo >> 5) & 0xFFFF) * 0.001f; //0
                // >>21    0 0 1
                var unk = eventInfo >> 21;

                switch (eventId) {
                case 0:
                    // Stop Event
                    throw new NotImplementedException("Stop event");

                case 1:
                {
                    // Unknown!
                    var u = clipReader.ReadByte();//ff

                    // Event flags
                    var eventFlags = clipReader.ReadByte();//0c
                    var playRelease = (eventFlags & 0x01) == 0x01;
                    var panEnabled = (eventFlags & 0x02) == 0x02;
                    var useCenterSpeaker = (eventFlags & 0x04) == 0x04;

                    int trackIndex = clipReader.ReadUInt16();//05 00
                    int waveBankIndex = clipReader.ReadByte();//3f
                    var loopCount = clipReader.ReadByte();//ff
                    var panAngle = clipReader.ReadUInt16() / 100.0f;//0
                    var panArc = clipReader.ReadUInt16() / 100.0f;//0
                    
                    _events[i] = new PlayWaveEvent(
                        this,
                        timeStamp, 
                        randomOffset,
                        soundBank, 
                        new[] { waveBankIndex }, 
                        new[] { trackIndex },
                        null,
                        0,
                        VariationType.Ordered, 
                        null,
                        null,
                        null,
                        loopCount,
                        false);

                    break;
                }

                case 3:
                {
                    // Unknown!
                    var u = clipReader.ReadByte();

                    // Event flags
                    var eventFlags = clipReader.ReadByte();
                    var playRelease = (eventFlags & 0x01) == 0x01;
                    var panEnabled = (eventFlags & 0x02) == 0x02;
                    var useCenterSpeaker = (eventFlags & 0x04) == 0x04;

                    var loopCount = clipReader.ReadByte();
                    var panAngle = clipReader.ReadUInt16() / 100.0f;
                    var panArc = clipReader.ReadUInt16() / 100.0f;

                    // The number of tracks for the variations.
                    var numTracks = clipReader.ReadUInt16();

                    // Not sure what most of this is.
                    var moreFlags = clipReader.ReadByte();
                    var newWaveOnLoop = (moreFlags & 0x40) == 0x40;
                    
                    // The variation playlist type seems to be 
                    // stored in the bottom 4bits only.
                    var variationType = (VariationType)(moreFlags & 0x0F);

                    // Unknown!
                    var u2 = clipReader.ReadBytes(5);
                    foreach (var b in u2) {
                    }

                    // Read in the variation playlist.
                    var waveBanks = new int[numTracks];
                    var tracks = new int[numTracks];
                    var weights = new byte[numTracks];
                    var totalWeights = 0;
                    for (var j = 0; j < numTracks; j++)
                    {
                        tracks[j] = clipReader.ReadUInt16();
                        waveBanks[j] = clipReader.ReadByte();
                        var minWeight = clipReader.ReadByte();
                        var maxWeight = clipReader.ReadByte();
                        weights[j] = (byte)(maxWeight - minWeight);
                        totalWeights += weights[j];
                    }

                    _events[i] = new PlayWaveEvent(
                        this,
                        timeStamp,
                        randomOffset,
                        soundBank, 
                        waveBanks, 
                        tracks,
                        weights,
                        totalWeights,
                        variationType,
                        null,
                        null,
                        null,
                        loopCount,
                        newWaveOnLoop);

                    break;
                }

                case 4:
                {
                    // Unknown!
                    var u = clipReader.ReadByte();

                    // Event flags
                    var eventFlags = clipReader.ReadByte();
                    var playRelease = (eventFlags & 0x01) == 0x01;
                    var panEnabled = (eventFlags & 0x02) == 0x02;
                    var useCenterSpeaker = (eventFlags & 0x04) == 0x04;

                    int trackIndex = clipReader.ReadUInt16();
                    int waveBankIndex = clipReader.ReadByte();
                    var loopCount = clipReader.ReadByte();
                    var panAngle = clipReader.ReadUInt16() / 100.0f;
                    var panArc = clipReader.ReadUInt16() / 100.0f;

                    // Pitch variation range
                    var minPitch = clipReader.ReadInt16() / 1000.0f;
                    var maxPitch = clipReader.ReadInt16() / 1000.0f;

                    // Volume variation range
                    var minVolume = XactHelpers.ParseVolumeFromDecibels(clipReader.ReadByte());
                    var maxVolume = XactHelpers.ParseVolumeFromDecibels(clipReader.ReadByte());

                    // Filter variation
                    var minFrequency = clipReader.ReadSingle();
                    var maxFrequency = clipReader.ReadSingle();
                    var minQ = clipReader.ReadSingle();
                    var maxQ = clipReader.ReadSingle();

                    // Unknown!
                    var u2 = clipReader.ReadByte();

                    var variationFlags = clipReader.ReadByte();

                    // Enable pitch variation
                    Vector2? pitchVar = null;
                    if ((variationFlags & 0x10) == 0x10)
                        pitchVar = new Vector2(minPitch, maxPitch - minPitch);

                    // Enable volume variation
                    Vector2? volumeVar = null;
                    if ((variationFlags & 0x20) == 0x20)
                        volumeVar = new Vector2(minVolume, maxVolume - minVolume);

                    // Enable filter variation
                    Vector4? filterVar = null;
                    if ((variationFlags & 0x40) == 0x40)
                        filterVar = new Vector4(minFrequency, maxFrequency - minFrequency, minQ, maxQ - minQ);

                    _events[i] = new PlayWaveEvent(
                        this,
                        timeStamp,
                        randomOffset,
                        soundBank,
                        new[] { waveBankIndex },
                        new[] { trackIndex }, 
                        null,
                        0,
                        VariationType.Ordered,
                        volumeVar,
                        pitchVar, 
                        filterVar,
                        loopCount,
                        false);

                    break;
                }

                case 6:
                {
                    // Unknown!
                    var u = clipReader.ReadByte();

                    // Event flags
                    var eventFlags = clipReader.ReadByte();
                    var playRelease = (eventFlags & 0x01) == 0x01;
                    var panEnabled = (eventFlags & 0x02) == 0x02;
                    var useCenterSpeaker = (eventFlags & 0x04) == 0x04;

                    var loopCount = clipReader.ReadByte();
                    var panAngle = clipReader.ReadUInt16() / 100.0f;
                    var panArc = clipReader.ReadUInt16() / 100.0f;

                    // Pitch variation range
                    var minPitch = clipReader.ReadInt16() / 1000.0f;
                    var maxPitch = clipReader.ReadInt16() / 1000.0f;

                    // Volume variation range
                    var minVolume = XactHelpers.ParseVolumeFromDecibels(clipReader.ReadByte());
                    var maxVolume = XactHelpers.ParseVolumeFromDecibels(clipReader.ReadByte());

                    // Filter variation range
                    var minFrequency = clipReader.ReadSingle();
                    var maxFrequency = clipReader.ReadSingle();
                    var minQ = clipReader.ReadSingle();
                    var maxQ = clipReader.ReadSingle();

                    // Unknown!
                    var u2 = clipReader.ReadByte();

                    // TODO: Still has unknown bits!
                    var variationFlags = clipReader.ReadByte();

                    // Enable pitch variation
                    Vector2? pitchVar = null;
                    if ((variationFlags & 0x10) == 0x10)
                        pitchVar = new Vector2(minPitch, maxPitch - minPitch);

                    // Enable volume variation
                    Vector2? volumeVar = null;
                    if ((variationFlags & 0x20) == 0x20)
                        volumeVar = new Vector2(minVolume, maxVolume - minVolume);

                    // Enable filter variation
                    Vector4? filterVar = null;
                    if ((variationFlags & 0x40) == 0x40)
                        filterVar = new Vector4(minFrequency, maxFrequency - minFrequency, minQ, maxQ - minQ);

                    // The number of tracks for the variations.
                    var numTracks = clipReader.ReadUInt16();

                    // Not sure what most of this is.
                    var moreFlags = clipReader.ReadByte();
                    var newWaveOnLoop = (moreFlags & 0x40) == 0x40;

                    // The variation playlist type seems to be 
                    // stored in the bottom 4bits only.
                    var variationType = (VariationType)(moreFlags & 0x0F);

                    // Unknown!
                    var u3 = clipReader.ReadBytes(5);
                    foreach (var b in u3) {
                    }

                    // Read in the variation playlist.
                    var waveBanks = new int[numTracks];
                    var tracks = new int[numTracks];
                    var weights = new byte[numTracks];
                    var totalWeights = 0;
                    for (var j = 0; j < numTracks; j++)
                    {
                        tracks[j] = clipReader.ReadUInt16();
                        waveBanks[j] = clipReader.ReadByte();
                        var minWeight = clipReader.ReadByte();
                        var maxWeight = clipReader.ReadByte();
                        weights[j] = (byte)(maxWeight - minWeight);
                        totalWeights += weights[j];
                    }

                    _events[i] = new PlayWaveEvent(
                        this,
                        timeStamp,
                        randomOffset,
                        soundBank,
                        waveBanks,
                        tracks,
                        weights,
                        totalWeights,
                        variationType,
                        volumeVar,
                        pitchVar, 
                        filterVar,
                        loopCount,
                        newWaveOnLoop);

                    break;
                }

                case 7:
                    // Pitch Event
                    throw new NotImplementedException("Pitch event");

                case 8:
                {
                    // Unknown!
                    clipReader.ReadBytes(2);

                    // Event flags
                    var eventFlags = clipReader.ReadByte();
                    var isAdd = (eventFlags & 0x01) == 0x01;

                    // The replacement or additive volume.
                    var decibles = clipReader.ReadSingle() / 100.0f;
                    var volume = XactHelpers.ParseVolumeFromDecibels(decibles + (isAdd ? volumeDb : 0));

                    // Unknown!
                    clipReader.ReadBytes(9);

                    _events[i] = new VolumeEvent(   this, 
                                                    timeStamp, 
                                                    randomOffset, 
                                                    volume);
                    break;
                }

                case 17:
                    // Volume Repeat Event
                    throw new NotImplementedException("Volume repeat event");

                case 9:
                    // Marker Event
                    throw new NotImplementedException("Marker event");

                default:
                    throw new NotSupportedException("Unknown event " + eventId);
                }
            }
            
            var unkz = clipReader.ReadByte();

            clipReader.BaseStream.Seek (oldPosition, SeekOrigin.Begin);
#pragma warning restore 0219
        }

        public void Update(float dt)
        {
            if (State != SoundState.Playing)
                return;

            _time += dt;

            // Play the next event.
            while (_nextEvent < _events.Length)
            {
                var evt = _events[_nextEvent];
                if (_time < evt.TimeStamp)
                    break;

                evt.Play();
                ++_nextEvent;
            }

            // Update all the active events.
            var isPlaying = _nextEvent < _events.Length;
            for (var i = 0; i < _nextEvent; i++)
            {
                var evt = _events[i];
                isPlaying |= evt.Update(dt);
            }

            // Update the state.
            if (!isPlaying)
                State = SoundState.Stopped;
        }

        public void SetFade(float fadeInDuration, float fadeOutDuration)
        {
            foreach (var evt in _events)
            {
                if (evt is PlayWaveEvent)
                    evt.SetFade(fadeInDuration, fadeOutDuration);
            }
        }
        
        public void UpdateState(float volume, float pitch, float reverbMix, float? filterFrequency, float? filterQFactor)
        {
            _volumeScale = volume;
            var trackVolume = _volume * _volumeScale;

            foreach (var evt in _events)
                evt.SetState(trackVolume, pitch, reverbMix, filterFrequency, filterQFactor);
        }

        public void Play()
        {
            _time = 0.0f;
            _nextEvent = 0;
            SetVolume(_defaultVolume);
            State = SoundState.Playing; 
            Update(0);
        }

        public void Resume()
        {
            foreach (var evt in _events)
                evt.Resume();

            State = SoundState.Playing;
        }
        
        public void Stop()
        {
            foreach (var evt in _events)
                evt.Stop();

            State = SoundState.Stopped;
        }
        
        public void Pause()
        {
            foreach (var evt in _events)
                evt.Pause();

            State = SoundState.Paused;
        }

        public SoundState State { get; set; }

        /// <summary>
        /// Set the combined volume scale from the parent objects.
        /// </summary>
        /// <param name="volume">The volume scale.</param>
        public void SetVolumeScale(float volume)
        {
            _volumeScale = volume;
            UpdateVolumes();
        }

        /// <summary>
        /// Set the volume for the clip.
        /// </summary>
        /// <param name="volume">The volume level.</param>
        public void SetVolume(float volume)
        {
            _volume = volume;
            UpdateVolumes();
        }

        public void UpdateVolumes()
        {
            var volume = _volume * _volumeScale;
            foreach (var evt in _events)
                evt.SetTrackVolume(volume);
        }

        public void SetPan(float pan)
        {
            foreach (var evt in _events)
                evt.SetTrackPan(pan);
        }
    }
}

