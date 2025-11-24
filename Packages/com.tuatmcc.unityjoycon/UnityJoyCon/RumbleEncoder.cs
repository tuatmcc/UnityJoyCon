using UnityEngine;

namespace UnityJoyCon
{
    internal static class RumbleEncoder
    {
        private const float HFMin = 81.75177f, HFMax = 1252.572266f; // HF ≈81.75–1252.57 Hz
        private const float LFMin = 40.875885f, LFMax = 626.286133f; // LF ≈40.87–626.28 Hz

        public static byte[] EncodeSide(float lfHz, float hfHz, float lfAmp, float hfAmp)
        {
            hfHz = Mathf.Clamp(hfHz, HFMin, HFMax);
            lfHz = Mathf.Clamp(lfHz, LFMin, LFMax);
            hfAmp = Mathf.Clamp01(hfAmp);
            lfAmp = Mathf.Clamp(lfAmp, 0f, 0.98f);

            var encHF = Mathf.RoundToInt(Mathf.Log(hfHz / 10f, 2f) * 32f);
            var encLF = Mathf.RoundToInt(Mathf.Log(lfHz / 10f, 2f) * 32f);

            var hf = (ushort)((encHF - 0x60) * 4);
            var lf = (byte)(encLF - 0x40);

            var idxH = EncodeAmpIndex(hfAmp);
            var idxL = EncodeAmpIndex(lfAmp);

            var ha = (ushort)(idxH * 2);
            var la = (ushort)(idxL / 2 + 0x40);

            var b = new byte[4];
            b[0] = (byte)(hf & 0xFF);
            b[1] = (byte)(((hf >> 8) & 0xFF) + (ha & 0xFF));
            b[2] = (byte)(lf + ((la >> 8) & 0xFF));
            b[3] = (byte)(la & 0xFF);
            return b;
        }

        private static int EncodeAmpIndex(float amp)
        {
            amp = Mathf.Clamp01(amp);
            return amp switch
            {
                > 0.23f => Mathf.RoundToInt(Mathf.Log(amp * 8.7f, 2f) * 32f),
                > 0.12f => Mathf.RoundToInt(Mathf.Log(amp * 17f, 2f) * 16f),
                _ => 0
            };
        }
    }
}
