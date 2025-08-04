// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.IO;

namespace Microsoft.Xna.Framework.Audio
{
    public class XactSound
    {
        public readonly bool _complexSound;
        public readonly XactClip[] _soundClips;
        public readonly int _waveBankIndex;
        public readonly int _trackIndex;
        public readonly float _volume;
        public readonly float _pitch;
        public readonly uint _categoryID;
        public readonly SoundBank _soundBank;
        public readonly bool _useReverb;

        public SoundEffectInstance _wave;
        public bool _streaming;

        public readonly int[] RpcCurves;
        public readonly int[][] ClipRpcCurves;
        RpcValues _rpcValues;
        
        public XactSound(SoundBank soundBank, int waveBankIndex, int trackIndex)
        {
            _complexSound = false;

            _soundBank = soundBank;
            _waveBankIndex = waveBankIndex;
            _trackIndex = trackIndex;
            RpcCurves = new int[0];
            ClipRpcCurves = new int[0][];
        }

        public XactSound(AudioEngine engine, SoundBank soundBank, BinaryReader soundReader)
        {
            _soundBank = soundBank;
            //ocsong
            //00007e50:                  03 0200 7600 0000 3700  ....u.....v...7.
            //00007e60: 0113 0004 5202 0000 8402 0000 b602 0000  ....R...........
            //00007e70: 7b01 0000 b47d 7e00 0011 0220 4e01 0100  {....}~.... N...
            //00007e80: 0020 0000 ff0c 0000 40ff 0000 0000 010a  . ......@.......

            var flags = soundReader.ReadByte(); //07
            //  spud: 0502 00da 0000 00e5 0007 2500
            //ocsong: 0302 0076 0000 0037 0001 1300
            // jocko: 0702 00a7 0000 003c 0001 1800

            //spud has track rpcs but not sound ones
            //ocsong has sound rpcs but not track ones
            //jocko has both
            //therefore:
            // 03 = 0 0 0 1 1
            // 05 = 0 0 1 0 1
            // 07 = 0 0 1 1 1
            // 13 = 1 0 0 1 1
            //      | | | | is complex
            //      | | | has sound RPCs
            //      | | has track RPCs
            //      | ?
            //      has DSPs
            Console.WriteLine($"  sound flags: {flags:x}");
            _complexSound = (flags & 0x1) != 0; //1
            var hasSoundRPCs = (flags & 0x2) != 0;
            var hasClipRPCs = (flags & 0x4) != 0;
            var hasRPCs = (flags & 0x0E) != 0; //1
            if (hasRPCs && !(hasSoundRPCs || hasClipRPCs))
                Console.WriteLine($"  has 0x8 RPCs!");
            var hasDSPs = (flags & 0x10) != 0; //0
            // 13 = 0 0 0 0 1 1 0 1
            // 0E = 0 0 0 0 1 1 1 0
            //  & = 0 0 0 0 1 1 0 0 != 0
            Console.WriteLine($"  sound {_complexSound} {hasSoundRPCs} {hasClipRPCs} {hasRPCs} {hasDSPs}");

            _categoryID = soundReader.ReadUInt16();//0200
            _volume = XactHelpers.ParseVolumeFromDecibels(soundReader.ReadByte()); //a7
            _pitch = soundReader.ReadInt16() / 1000.0f; //0000
            soundReader.ReadByte(); //priority 0
            soundReader.ReadUInt16(); // filter stuff? 3c00
            
            var numClips = 0;
            if (_complexSound)
                numClips = soundReader.ReadByte(); //01
            else 
            {
                _trackIndex = soundReader.ReadUInt16();
                _waveBankIndex = soundReader.ReadByte();
            }

            if (!hasRPCs) {
                RpcCurves = new int[0];
                ClipRpcCurves = new int[0][];
            }
            else
            {
                var current = soundReader.BaseStream.Position;
                // Cue gnome-poof: complex soundOffset 6a2d flags 5
                //00006a20:                                 13 0800   ....:..........
                //00006a30: 5a00 0000 3e00 0113 0004 7b01 0000 5202  Z...>.....{...R.
                //00006a40: 0000 8402 0000 b602 0000                 ................

                //13 0800 5a00 0000 3e00 0113 0004 7b01 0000 5202 0000 8402 0000 b602 0000
                //fl cat  v pitch p ff   #cdl-dlsound-rpcs------------------------------->

                //  seek past dataLength to 6a4a
                //00006a40:                          0700 01cb 0500  ................
                //00006a50: 00                                       ..Zj.... N.... .

                //  reading clip at pos 6a51
                //00006a50:   b4 5a6a 0000 1102 204e 0101 0000 2000  ..Zj.... N.... .
                //00006a60: 00ff 0c05 001b 0000 0000 0003 0200 4c00  ..............L.
                //00006a70: 0000 3700 0113 0004 7b01 0000 5202 0000  ..7.....{...R...
                //00006a80: 8402 0000 b602 0000 b491 6a00 0011 0220  ..........j....
                //00006a90: 4e01 0100 0020 0000 ff0c 0000 32ff 0000  N.... ......2...
                //00006aa0: 0000 1305 006e 0000 0057 0001 1300 047b  .....n...W.....{



                // TODO explain
                var dataLength = soundReader.ReadUInt16(); //0x1800

                //Cue boss-jocko: complex soundOffset 407b flags 5
                //00004070:                            07 0200 a700             .....
                //00004080: 0000 3c00 0118 0001 7b01 0000 0435 0500  ..<.....{....5..
                //00004090: 0003 0500 0067 0500 0099 0500 00         .....g.......   
                    //dataLength portion
                    //01 7b01 0000 0435 0500 0003 0500 0067 0500 0099 0500 00

                    //  seek past dataLength to 409d
                    //00004090:                                 b4 a640               ..@
                    //000040a0: 0000 1102 204e 0101 0000 2000 00ff 0c00  .... N.... .....
                    //000040b0: 0027 ff00 0000 0013 0900 8200 0000 3e00  .'............>.

                //Cue spud-music: complex soundOffset 7c8c flags 5
                    //  seek past dataLength to 7cbb
                    //  reading clip at pos 7cbb
                    //  reading clip at pos 7cc4
                    //  reading clip at pos 7ccd
                    //  reading clip at pos 7cd6
                    //  reading clip at pos 7cdf
                    //  reading clip at pos 7ce8
                    //  reading clip at pos 7cf1
                    //Cue ocsong: complex soundOffset 7e57 flags 5

                    //Cue spud-music: complex soundOffset 7c8c flags 5
                    //00007c80:                               0502 00da              ....
                    //00007c90: 0000 00e5 0007 2500 01e8 0200 00         ......%......   

                    //0502 00da 0000 00e5 0007 2500 01e8 0200 00

                    //Unknown dataLength section, 30 bytes
                    //00007c90:                                 01 1a03               ...
                    //00007ca0: 0000 0170 0300 0001 c603 0000 011c 0400  ...p............
                    //00007cb0: 0001 7b04 0000 01d1 0400 00              ..{........     

                    //rpc curve var Spud Music offset 2e8 // attached to the cue? yes
                    //rpc curve var Spud Music offset 31a x 1a03
                    //rpc curve var Spud Music offset 370 x 70 03
                    //rpc curve var Spud Music offset 3c6 x c603
                    //rpc curve var Spud Music offset 41c x 1c 04
                    //rpc curve var Spud Music offset 47b x 7b04
                    //rpc curve var Spud Music offset 4d1 x d1 04

                    //Assuming offsets are 32bit like the rest...
                    // 011a 0300 0001 7003 0000 01c6 0300 0001 1c04 0000 017b 0400 0001 d104 0000
                    // ? [--------] ? [-------]  ?[--------] ? [-------]  ?[--------] ? [-------]
                    //     |            |            |            |           |            ^ rpc curve offset
                    //     |            |            |            |           rpc curve offset
                    //     |            |            |            |         
                    //     |            |            |            rpc curve offset
                    //     |            |            |          
                    //     |            |            rpc curve offset
                    //     |            |         
                    //     |            rpc curve offset
                    //     |        
                    //     rpc curve offset

                    //ocsong dataLength region
                    //3 rpc entries on the Sound + 1 category entry; 1 track with no rpc curves
                    //04 5202 0000 8402 0000 b602 0000 7b01 0000
                    // | [---?---] [----?--] [---?---] [-- ? --]
                    // 4 32 bit entries follow
                    // underwater filter = 252
                    // 284=underwater filter
                    // 2b6=underwater filter
                    // 17b=Volume

                //  seek past dataLength to 7cbb - clips start here
                //clip 1
                //00007cb0:                            b4 fa7c 0000             ..|..
                //00007cc0: 1102 204e                                .. N..}.... N..}
                //00007cc0:           b40b 7d00 0011 0220 4e         .. N..}.... N..}
                //00007cc0:                                 b4 1c7d  .. N..}.... N..}
                //00007cd0: 0000 1102 204e                           .... N.-}.... N.
                //00007cd0:                b42d 7d00 0011 0220 4e    .... N.-}.... N.
                //00007cd0:                                      b4  .... N.-}.... N.
                //00007ce0: 3e7d 0000 1102 204e                      >}.... N.O}....
                //00007ce0:                     b44f 7d00 0011 0220  >}.... N.O}....
                //00007cf0: 4e                                       N.`}.... N      
                //00007cf0:   b4 607d 0000 1102 204e                 N.`}.... N      

                //more clip details starting with clip 1
                //00007cf0:                          0101 0000 2000            .... .
                //00007d00: 00ff 0c05 003f ff00 0000 0001 0100 0020  .....?.........
                //00007d10: 0000 ff0c 0600 3fff 0000 0000 0101 0000  ......?.........
                //00007d20: 2000 00ff 0c00 003f ff00 0000 0001 0100   ......?........
                //00007d30: 0020 0000 ff0c 0100 3fff 0000 0000 0101  . ......?.......
                //00007d40: 0000 2000 00ff 0c02 003f ff00 0000 0001  .. ......?......
                //00007d50: 0100 0020 0000 ff0c 0300 3fff 0000 0000  ... ......?.....
                //00007d60: 0101 0000 2000 00ff 0c04 003f ff00 0000  .... ......?....
                //00007d70: 0001 0a00 bf00 0000 3d00 01b4 847d 0000  ........=....}..
                //00007d80: c05d e803 0103 0000 2000 00ff 0c00 0000  .]...... .......
                //00007d90: 0000 0400 0300 ffff ffff 1500 3100 ff16  ............1...
                //00007da0: 0031 00ff 1700 3100 ff18 0031 00ff 0009  .1....1....1....
                //00007db0: 00b4 0000 000c 0073 0001 0009 00af 0000  .......s........
                //00007dc0: 000c 0074 0001 0005 00a5 0000 000c 0033  ...t...........3
                //00007dd0: 0021 0005 005a 0000 000c 0035 0021 0005  .!...Z.....5.!..
                //00007de0: 0067 0000 000c 0034 0021 0005 0076 0000  .g.....4.!...v..
                //00007df0: 000c 0036 0021 010a 006e 0000 0055 0001  ...6.!...n...U..
                //00007e00: b409 7e00 00c0 5de8 0301 0600 0020 0000  ..~...]...... ..
                //00007e10: ff0c 0000 0000 0038 ff64 0097 b700 007a  .......8.d.....z
                //00007e20: 4400 007a 4400 00f0 4100 00f0 4100 3004  D..zD...A...A.0.
                //00007e30: 0003 00ff ffff ff2a 002f 00ff 2700 2f00  .......*./..'./.
                //00007e40: ff28 002f 00ff 2900 2f00 ff00 0700 b400  .(./..)./.......
                //00007e50: 0000 0c00 7500 01                        ....u.....v...7.
                //Cue ocsong: simple soundOffset 7e57 flags 5

                //ocsong
                //00007e50:                  03 0200 7600 0000 3700  ....u.....v...7.
                //00007e60: 0113 00                                  ....R...........

                //dataLength = 13 00, 19 bytes
                //00007e60:        04 5202 0000 8402 0000 b602 0000     .R...........
                //00007e70: 7b01 0000 b47d                            {...            

                //01 1a03 0000 0170 0300 0001 c603 0000 011c 0400 0001 7b04 0000 01d1 0400 00
                //04 5202 0000 8402 0000 b602 0000 7b01 0000

                //00007e60:        04 5202 0000 8402 0000 b602 0000  ....R...........
                //00007e70: 7b01 0000 b47d 7e00 0011 0220 4e01 0100  {....}~.... N...
                //00007e80: 0020 0000 ff0c 0000 40ff 0000 0000 010a  . ......@.......

                //boss-jocko
                    //rpcs
                    //rpc curve var Volume offset 17b
                    //rpc curve var Jocko Music offset 503
                    //rpc curve var Jocko Music offset 535
                    //rpc curve var Jocko Music offset 567
                    //rpc curve var Jocko Music offset 599
                    //dataLength portion
                    //01 7b01 0000 0435 0500 0003 0500 0067 0500 0099 0500 00
                    //   [-------]   [--------][--------][--------][--------]
                    //   volume      jocko2    jocko1    jocko3    jocko4

                //Cue ocsong: complex soundOffset 7e57 flags 5
                //00007e50:                  03 0200 7600 0000 3700  ....u.....v...7.
                //00007e60: 0113 0004 5202 0000 8402 0000 b602 0000  ....R...........
                //00007e70: 7b01 0000                                {....}~.... N...
                //  seek past dataLength to 7e74
                    //03 0200 7600 0000 3700 0113 00 04 5202 0000 8402 0000 b602 0000 7b01 0000                               
                    //fl cat  v pitch p ff   #cdl dl rpcs on track 0-------------------------->

                //Cue mal-theme: complex soundOffset 10ca flags 5
                    //000010c0:                          0302 00b4 0000  ................
                    //000010d0: 002b 0001 0700 017b 0100 00              .+.....{........

                    //03 0200 b400 0000 2b00 0107 0001 7b01 0000
                    //fl cat  v pitch p ff   #cdatal rpcs on track 0-------------------------->
                //  seek past dataLength to 10db

                //Tracks (clips) within a sound can use different RPC curves
                if (_complexSound) {
                    if (hasSoundRPCs) {
                        RpcCurves = ReadRPCs(engine, soundReader);
                    }
                    else RpcCurves = new int[0];

                    if (hasClipRPCs) {
                        ClipRpcCurves = new int[numClips][];
                        for (int c = 0; c < numClips; c++) {
                            ClipRpcCurves[c] = ReadRPCs(engine, soundReader);
                        }
                    }
                    else ClipRpcCurves = new int[0][];
                } else {
                    RpcCurves = ReadRPCs(engine, soundReader);
                    ClipRpcCurves = new int[0][];
                }

                // Just in case seek to the right spot.
                soundReader.BaseStream.Seek(current + dataLength, SeekOrigin.Begin);
                Console.WriteLine($"  seek past dataLength to {soundReader.BaseStream.Position:x}");
            }

            if (!hasDSPs)
                _useReverb = false;
            else
            {
                // The file format for this seems to follow the pattern for 
                // the RPC curves above, but in this case XACT only supports
                // a single effect...  Microsoft Reverb... so just set it.
                _useReverb = true;
                soundReader.BaseStream.Seek(7, SeekOrigin.Current);
            }

            if (_complexSound)
            {
                _soundClips = new XactClip[numClips];
                for (int i = 0; i < numClips; i++)
                    _soundClips[i] = new XactClip(soundBank, soundReader, _useReverb);
            }

            var category = engine.Categories[_categoryID];
            category.AddSound(this);
        }

        private int[] ReadRPCs(AudioEngine engine, BinaryReader soundReader) {
            var numRPCs = soundReader.ReadByte();
            var rpcs = new int[numRPCs];
            for (var r = 0; r < numRPCs; r++)
                rpcs[r] = engine.GetRpcIndex(soundReader.ReadUInt32());
            return rpcs;
        }

        public void SetFade(float fadeInTime, float fadeOutTime)
        {
            if (fadeInTime == 0.0f &&
                fadeOutTime == 0.0f )
                return;

            if (_complexSound)
            {
                foreach (var sound in _soundClips)
                    sound.SetFade(fadeInTime, fadeOutTime);
            }
            else
            {
                // TODO:
            }
        }

        public void Play(AudioEngine engine)
        {
            var category = engine.Categories[_categoryID];

            var curInstances = category.GetPlayingInstanceCount();
            if (curInstances >= category.maxInstances)
            {
                var prevSound = category.GetOldestInstance();

                if (prevSound != null)
                {
                    prevSound.SetFade(0.0f, category.fadeOut);
                    prevSound.Stop(AudioStopOptions.Immediate);
                    SetFade(category.fadeIn, 0.0f);
                }
            }

            if (_complexSound) 
            {
                foreach (XactClip clip in _soundClips)
                {
                    clip.Play();
                }
            } 
            else 
            {
                if (_wave != null)
                {
                    if (_streaming)
                        _wave.Dispose();
					else
						_wave._isXAct = false;					
                    _wave = null;
                }
                _wave = _soundBank.GetSoundEffectInstance(_waveBankIndex, _trackIndex, out _streaming);

                if (_wave == null)
                {
                    // We couldn't create a sound effect instance, most likely
                    // because we've reached the sound pool limits.
                    return;
                }

                float finalVolume = _volume * category._volume[0];
                float finalPitch = _pitch + _rpcValues.pitch;
                float finalMix = _useReverb ? _rpcValues.reverbMix : 0.0f;

                _wave.Pitch = finalPitch;
                _wave.Volume = finalVolume;
                _wave.PlatformSetReverbMix(finalMix);
                _wave.Play();
            }
        }

        public void Update(float dt)
        {
            if (_complexSound)
            {
                foreach (var sound in _soundClips)
                    sound.Update(dt);
            }
            else
            {
                if (_wave != null && _wave.State == SoundState.Stopped)
                {
                    if (_streaming)
                        _wave.Dispose();
					else
						_wave._isXAct = false;					
                    _wave = null;
                }
            }
        }

        public void StopAll(AudioStopOptions options)
        {
            if (_complexSound)
            {
                foreach (XactClip clip in _soundClips)
                    clip.Stop();
            }
            else
            {
                if (_wave != null)
                {
                    _wave.Stop();
                    if (_streaming)
                        _wave.Dispose();
 					else
						_wave._isXAct = false;					
                   _wave = null;
                }
            }
        }
        
        public void Stop(AudioStopOptions options)
        {
            if (_complexSound)
            {
                foreach (var sound in _soundClips)
                    sound.Stop();
            }
            else
            {
                if (_wave != null)
                {
                    _wave.Stop();
                    if (_streaming)
                        _wave.Dispose();
					else
						_wave._isXAct = false;					
                    _wave = null;
                }
            }
        }
        
        public void Pause()
        {
            if (_complexSound)
            {
                foreach (var sound in _soundClips)
                {
                    if (sound.State == SoundState.Playing)
                        sound.Pause();
                }
            }
            else
            {
                if (_wave != null && _wave.State == SoundState.Playing)
                    _wave.Pause();
            }
        }
                
        public void Resume()
        {
            if (_complexSound)
            {
                foreach (var sound in _soundClips)
                {
                    if (sound.State == SoundState.Paused)
                        sound.Resume();
                }
            }
            else
            {
                if (_wave != null && _wave.State == SoundState.Paused)
                    _wave.Resume();
            }
        }

        public void UpdateCategoryVolume(float categoryVolume)
        {
            // The different volumes modulate each other.
            var volume = _volume * _rpcValues.volume * categoryVolume;

            if (_complexSound)
            {
                foreach (var clip in _soundClips)
                    clip.SetVolumeScale(volume);
            }
            else
            {
                if (_wave != null)
                    _wave.Volume = volume;
            }
        }

        public void SetCuePan(float pan)
        {
            if (_complexSound)
            {
                foreach (var clip in _soundClips)
                    clip.SetPan(pan);
            }
            else
            {
                if (_wave != null)
                    _wave.Pan = pan;
            }
        }

        public bool Playing 
        {
            get 
            {
                if (_complexSound)
                {
                    foreach (var clip in _soundClips)
                        if (clip.State == SoundState.Playing)
                            return true;

                    return false;
                } 

                return _wave != null && _wave.State == SoundState.Playing;
            }
        }

        public bool Stopped
        {
            get
            {
                if (_complexSound)
                {
                    var notStopped = false;

                    // All clips must be stopped for the sound to be stopped.
                    foreach (var clip in _soundClips)
                    {
                        if (clip.State != SoundState.Stopped)
                            notStopped = true;
                    }

                    return !notStopped;
                }

                // We null the wave when it it stopped.
                return _wave == null;
            }
        }

        public bool IsPaused
        {
            get
            {
                if (_complexSound) 
                {
                    foreach (var clip in _soundClips)
                        if (clip.State == SoundState.Paused) 
                            return true;

                    return false;
                }

                return _wave != null && _wave.State == SoundState.Paused;
            }
        }

        // Evaluate the runtime parameter controls.
        // 1. Calculate parameters for the sound
        // 2. Calculate parameters for each clip
        // 3. Mix 1 & 2 for each clip
        // 4. Update clip state
        public void UpdateRpcCurves(AudioEngine engine, RpcVariable[] cueVariables)
        {
            _rpcValues = new RpcValues(RpcCurves, cueVariables, engine);
            if (_complexSound) {
                for (int c = 0; c < _soundClips.Length; ++c) {
                    var clipValues = _rpcValues;
                    if (c < ClipRpcCurves.Length) {
                        clipValues.Mix(
                            new RpcValues(
                                ClipRpcCurves[c], cueVariables, engine
                            )
                        );
                    }
                    clipValues.Clamp();
                    _soundClips[c].UpdateState(
                        clipValues.volume * _volume * engine.Categories[_categoryID]._volume[0],
                        clipValues.pitch + _pitch,
                        _useReverb ? clipValues.reverbMix : 0f,
                        clipValues.filterFrequency,
                        clipValues.filterQFactor
                    );
                }
            }

            _rpcValues.Clamp();
            if (_wave != null)
            {
                var finalVolume = _volume * _rpcValues.volume * engine.Categories[_categoryID]._volume[0];
                var finalPitch = _pitch + _rpcValues.pitch;
                _wave.PlatformSetReverbMix(_useReverb ? _rpcValues.reverbMix : 0.0f);
                _wave.Pitch = finalPitch;
                _wave.Volume = finalVolume;
            }
        }
    }
}

