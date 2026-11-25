using System;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

namespace EucMod
{
    /// <summary>
    /// Headlight (a real spotlight on the road plus a glow at the lamp) and a red taillight that
    /// brightens when braking. Script lights are drawn every frame; nothing is spawned.
    /// Mode cycles Auto (on 19:00-07:00) / On / Off with the lights key.
    /// </summary>
    internal sealed class EucLights
    {
        enum Mode { Auto, On, Off }

        readonly Config cfg;
        Mode mode = Mode.Auto;

        public EucLights(Config cfg) { this.cfg = cfg; }

        public void CycleMode()
        {
            mode = mode == Mode.Auto ? Mode.On : mode == Mode.On ? Mode.Off : Mode.Auto;
            Screen.ShowSubtitle($"Lights: {mode}", 1500);
        }

        bool IsOn()
        {
            if (mode != Mode.Auto) return mode == Mode.On;
            int h = Function.Call<int>(Hash.GET_CLOCK_HOURS);
            return h >= 19 || h < 7;
        }

        /// <param name="body">the EUC body prop the lamps are mounted on</param>
        /// <param name="braking">true while braking: taillight goes bright</param>
        public void Draw(Entity body, Vector3 forward, bool braking)
        {
            if (body == null || !body.Exists()) return;
            bool on = IsOn();

            if (on)
            {
                var h = cfg.HeadlightOffset;
                var lamp = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, body, h.X, h.Y, h.Z);
                var dir = new Vector3(forward.X, forward.Y, -cfg.HeadlightDroop);
                dir.Normalize();
                Function.Call(Hash.DRAW_SPOT_LIGHT, lamp.X, lamp.Y, lamp.Z, dir.X, dir.Y, dir.Z,
                    255, 245, 225, cfg.HeadlightRange, cfg.HeadlightBrightness, 0f, cfg.HeadlightConeDeg, 1f);
                // small glow so the lamp itself reads as lit
                var glow = lamp + forward * 0.05f;
                Function.Call(Hash.DRAW_LIGHT_WITH_RANGE, glow.X, glow.Y, glow.Z, 255, 245, 225, 0.6f, 4f);
            }

            if (on || braking)
            {
                var t = cfg.TaillightOffset;
                var tail = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, body, t.X, t.Y, t.Z) - forward * 0.05f;
                Function.Call(Hash.DRAW_LIGHT_WITH_RANGE, tail.X, tail.Y, tail.Z, 255, 0, 0,
                    braking ? 1.6f : 0.8f, braking ? 12f : 3f);
            }
        }
    }
}
