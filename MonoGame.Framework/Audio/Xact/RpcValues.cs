// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.IO;

namespace Microsoft.Xna.Framework.Audio
{
    struct RpcValues {
        public float volume = 1f;
        public float pitch = 0f;
        public float reverbMix = 1f;
        public float? filterFrequency = null;
        public float? filterQFactor = null;

        public RpcValues() {
            volume = 1f;
            pitch = 0f;
            reverbMix = 1f;
            filterFrequency = null;
            filterQFactor = null;
        }

        public RpcValues(
            int[] rpcCurves, RpcVariable[] cueVariables, AudioEngine engine
        ): this()
        {
            if (rpcCurves.Length > 0)
            {
                for (var i = 0; i < rpcCurves.Length; i++)
                {
                    var rpcCurve = engine.RpcCurves[rpcCurves[i]];

                    // Some curves are driven by global variables and others by cue instance variables.
                    float value;
                    if (rpcCurve.IsGlobal)
                        value = rpcCurve.Evaluate(engine.GetGlobalVariable(rpcCurve.Variable));
                    else
                        value = rpcCurve.Evaluate(cueVariables[rpcCurve.Variable].Value);

                    // Process the final curve value based on the parameter type it is.
                    switch (rpcCurve.Parameter)
                    {
                        case RpcParameter.Volume:
                            volume *= XactHelpers.ParseVolumeFromDecibels(value / 100.0f);
                            break;

                        case RpcParameter.Pitch:
                            pitch += value / 1000.0f;
                            break;

                        case RpcParameter.ReverbSend:
                            reverbMix *= XactHelpers.ParseVolumeFromDecibels(value / 100.0f);
                            break;

                        case RpcParameter.FilterFrequency:
                            filterFrequency = value;
                            break;

                        case RpcParameter.FilterQFactor:
                            filterQFactor = value;
                            break;

                        default:
                            throw new ArgumentOutOfRangeException("rpcCurve.Parameter");
                    }
                }
            }
        }

        public void Clamp() {
            pitch = MathHelper.Clamp(pitch, -1.0f, 1.0f);
            if (volume < 0.0f)
                volume = 0.0f;
        }

        public void Mix(RpcValues other) {
            volume *= other.volume;
            pitch += other.pitch;
            reverbMix *= other.reverbMix;
            filterFrequency = other.filterFrequency;
            filterQFactor = other.filterQFactor;
        }

        public override string ToString() {
            return $"{volume} {pitch} {reverbMix} {filterFrequency} {filterQFactor}";
        }
    }
}
