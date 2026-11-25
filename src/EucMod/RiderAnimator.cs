using System;
using GTA;
using GTA.Native;

namespace EucMod
{
    /// <summary>
    /// Drives the rider's body from the custom clip dictionary (tools/blender/anim/build_euc_anims.py).
    /// Every clip is a "scrub" clip: it is played frozen and its phase is set each frame, so the
    /// posture follows the riding state continuously instead of looping canned motion.
    ///   lean  0 = full carve left, 0.5 = cruise, 1 = full carve right
    ///   brake 0 = cruise, 1 = hard-stop squat (also used briefly as the landing squat)
    ///   air   0 = cruise, 1 = airborne tuck
    /// Phase 0 of brake/air is the same pose as lean 0.5, so switching clips blends seamlessly.
    /// Falls back to the plain ini idle animation if the custom dictionary isn't installed.
    /// </summary>
    internal sealed class RiderAnimator
    {
        readonly Config cfg;
        readonly Action<string> log;

        string playing;
        float leanPhase = 0.5f, brakePhase, airPhase, loadWait, fallbackTimer;
        bool braking;

        public bool UsingCustom { get; private set; } = true;

        public RiderAnimator(Config cfg, Action<string> log)
        {
            this.cfg = cfg;
            this.log = log;
        }

        public void Start()
        {
            playing = null;
            leanPhase = 0.5f;
            brakePhase = airPhase = loadWait = fallbackTimer = 0f;
            braking = false;
            Function.Call(Hash.REQUEST_ANIM_DICT, UsingCustom ? cfg.RiderAnimDict : cfg.AnimDict);
        }

        /// <param name="lean">-1 full carve left .. +1 full carve right</param>
        /// <param name="brake">0..1 how hard the rider is braking (or absorbing a landing)</param>
        public void Update(Ped ped, float dt, float lean, float brake, bool airborne)
        {
            float k = 1f - (float)Math.Exp(-10f * dt);
            leanPhase += (0.5f + 0.5f * Clamp(lean, -1f, 1f) - leanPhase) * k;
            brakePhase += (Clamp(brake, 0f, 1f) - brakePhase) * k;
            airPhase += ((airborne ? 1f : 0f) - airPhase) * (1f - (float)Math.Exp(-7f * dt));
            braking = braking ? brakePhase > 0.08f : brakePhase > 0.2f;   // hysteresis: no flicker between clips

            if (!UsingCustom) { Fallback(ped, dt); return; }

            string dict = cfg.RiderAnimDict;
            if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict))
            {
                Function.Call(Hash.REQUEST_ANIM_DICT, dict);
                loadWait += dt;
                if (loadWait > 3f)
                {
                    UsingCustom = false;
                    log($"anim: '{dict}' did not load (DLC not installed?) - falling back to {cfg.AnimDict}/{cfg.AnimName}");
                }
                return;
            }

            string clip;
            float phase;
            if (airPhase > 0.03f) { clip = "air"; phase = airPhase; }
            else if (braking) { clip = "brake"; phase = brakePhase; }
            else { clip = "lean"; phase = leanPhase; }

            if (clip != playing || !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped, dict, clip, 3))
            {
                Function.Call(Hash.TASK_PLAY_ANIM, ped, dict, clip, 6f, -6f, -1, 1, 0f, false, false, false);
                if (playing == null) log($"anim: playing custom '{dict}/{clip}'");
                playing = clip;
            }
            Function.Call(Hash.SET_ENTITY_ANIM_SPEED, ped, dict, clip, 0f);
            Function.Call(Hash.SET_ENTITY_ANIM_CURRENT_TIME, ped, dict, clip, Clamp(phase, 0.001f, 0.999f));
        }

        void Fallback(Ped ped, float dt)
        {
            fallbackTimer -= dt;
            if (fallbackTimer > 0f) return;
            fallbackTimer = 0.5f;
            if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, cfg.AnimDict))
            {
                Function.Call(Hash.REQUEST_ANIM_DICT, cfg.AnimDict);
                return;
            }
            if (!Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped, cfg.AnimDict, cfg.AnimName, 3))
                Function.Call(Hash.TASK_PLAY_ANIM, ped, cfg.AnimDict, cfg.AnimName, 4f, -4f, -1, 1, 0f, false, false, false);
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
