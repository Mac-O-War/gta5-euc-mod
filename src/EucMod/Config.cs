using System;
using System.Globalization;
using GTA;
using GTA.Math;
using Keys = System.Windows.Forms.Keys;

namespace EucMod
{
    /// <summary>All tunables, loaded from EucMod.ini next to the script. Edit + press Insert in-game to reload.</summary>
    internal sealed class Config
    {
        // [Keys]
        public Keys SpawnKey = Keys.F7;

        // [Radio]
        public bool RadioHeadphones;            // Mode = Speaker (default) | Headphones
        public Keys RadioToggleKey = Keys.R;
        public Keys RadioNextKey = Keys.OemCloseBrackets;   // ]
        public Keys RadioPrevKey = Keys.OemOpenBrackets;    // [
        public bool RadioWhenParked = true;     // speaker keeps playing on a parked wheel
        public int RadioStartStation;
        public string RadioEmitter = "SE_Script_Placed_Prop_Emitter_Boombox";  // static emitter used in Speaker mode

        // [Model]
        public string RootModel = "prop_roadcone02a";           // invisible anchor the rider and visual attach to
        public string[] VisualModels = { "euc_offroad", "prop_rub_tyre_01" };  // first one that exists is used
        public Vector3 VisualOffset = Vector3.Zero;
        public string WheelModel = "euc_offroad_wheel";          // spinning wheel prop (only with the real body model)
        public float WheelAxleHeight = 0.26f;                     // m above the ground contact
        public float WheelRollRadius = 0.27f;                     // m, sets spin speed
        public float WheelSpinSign = 1f;                          // flip to -1 if the wheel spins backwards

        // [Lights]
        public System.Windows.Forms.Keys LightsKey = System.Windows.Forms.Keys.L;
        public Vector3 HeadlightOffset = new Vector3(0f, 0.24f, 0.56f);   // x right, y forward, z up (m)
        public Vector3 TaillightOffset = new Vector3(0f, -0.27f, 0.55f);
        public float HeadlightRange = 30f;
        public float HeadlightBrightness = 8f;
        public float HeadlightConeDeg = 30f;
        public float HeadlightDroop = 0.12f;                      // aim the beam down at the road
        // [ModelRotations] per-model pitch, roll, yaw fix-ups in degrees (built-in placeholders face odd ways)
        readonly System.Collections.Generic.Dictionary<string, Vector3> modelRotations =
            new System.Collections.Generic.Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase)
            {
                ["prop_rub_tyre_01"] = new Vector3(0f, 0f, 90f),
            };

        public Vector3 RotationFor(string model) =>
            modelRotations.TryGetValue(model, out var r) ? r : Vector3.Zero;

        // [Rider]
        public float RiderHeight = 1.25f;                         // ped origin above ground when standing on the pedals
        public string AnimDict = "anim@heists@heist_corona@single_team";
        public string AnimName = "single_team_loop_boss";       // fallback idle if the custom clips are missing
        public string RiderAnimDict = "euc_rider";              // custom clip dictionary shipped in the DLC
        public float CarveFullLeanDeg = 35f;                    // physical lean that maps to the full carve pose

        // [Ride]
        public float MaxSpeedKmh = 50f;
        public float MaxReverseKmh = 8f;
        public float Accel = 3.5f;          // m/s^2
        public float Brake = 7f;            // m/s^2
        public float CoastDecel = 0.6f;     // m/s^2
        public float TurnRateLow = 180f;    // deg/s when nearly stopped
        public float TurnFalloff = 7f;      // m/s; turn rate halves at this speed
        public float TiltbackRatio = 0.9f;  // fraction of max speed where tilt-back starts
        public bool CutoutEnabled = true;
        public float CutoutSeconds = 1.5f;  // pushing into tilt-back this long = overlean cut-out
        public float CrashSpeedKmh = 18f;   // hitting something faster than this throws you off
        public float HardLandingSpeed = 9f; // m/s vertical
        public float StepHeight = 0.4f;     // max curb height you can roll up
        public float RangeKm = 40f;
        public float ParkedChargePerSecond = 0.005f;

        // [Visual]
        public float MaxLeanDeg = 10f;
        public float MaxRollDeg = 45f;
        public float LeanResponse = 10f;    // how quickly the turn lean follows (higher = snappier)
        public float SlowTurnWheelTiltDeg = 18f; // slow turns: extra wheel tilt into the turn
        public float CounterLeanDeg = 8f;        // slow turns: rider's body leans away from the turn
        public float CounterLeanFadeSpeed = 4f;  // m/s (~15 km/h) above which slow-turn style is gone
        public float RollSign = 1f;         // flip to -1 if the rider leans out of turns
        public float TiltbackDeg = 5f;
        public bool ShowHud = true;
        public bool UseMph = true;          // [Visual] SpeedUnits = mph | kmh (HUD display only)

        public static Config Load(string path)
        {
            var s = ScriptSettings.Load(path);
            var c = new Config();

            c.SpawnKey = Key(s, "Keys", "SpawnKey", c.SpawnKey);

            c.RadioHeadphones = s.GetValue("Radio", "Mode", "Speaker").Trim().Equals("Headphones", StringComparison.OrdinalIgnoreCase);
            c.RadioToggleKey = Key(s, "Radio", "ToggleKey", c.RadioToggleKey);
            c.RadioNextKey = Key(s, "Radio", "NextKey", c.RadioNextKey);
            c.RadioPrevKey = Key(s, "Radio", "PrevKey", c.RadioPrevKey);
            c.RadioWhenParked = F(s, "Radio", "PlayWhenParked", c.RadioWhenParked ? 1 : 0) != 0;
            c.RadioStartStation = (int)F(s, "Radio", "StartStation", c.RadioStartStation);
            c.RadioEmitter = s.GetValue("Radio", "Emitter", c.RadioEmitter).Trim();

            c.RootModel = s.GetValue("Model", "RootModel", c.RootModel);
            c.VisualModels = Array.ConvertAll(
                s.GetValue("Model", "VisualModels", string.Join(",", c.VisualModels))
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                m => m.Trim());
            c.VisualOffset = V3(s, "Model", "VisualOffset", c.VisualOffset);
            c.WheelModel = s.GetValue("Model", "WheelModel", c.WheelModel).Trim();
            c.WheelAxleHeight = F(s, "Model", "WheelAxleHeight", c.WheelAxleHeight);
            c.WheelRollRadius = Math.Max(0.05f, F(s, "Model", "WheelRollRadius", c.WheelRollRadius));
            c.WheelSpinSign = F(s, "Model", "WheelSpinSign", c.WheelSpinSign);

            c.LightsKey = Key(s, "Lights", "ToggleKey", c.LightsKey);
            c.HeadlightOffset = V3(s, "Lights", "HeadlightOffset", c.HeadlightOffset);
            c.TaillightOffset = V3(s, "Lights", "TaillightOffset", c.TaillightOffset);
            c.HeadlightRange = F(s, "Lights", "HeadlightRange", c.HeadlightRange);
            c.HeadlightBrightness = F(s, "Lights", "HeadlightBrightness", c.HeadlightBrightness);
            c.HeadlightConeDeg = F(s, "Lights", "HeadlightConeDeg", c.HeadlightConeDeg);
            c.HeadlightDroop = F(s, "Lights", "HeadlightDroop", c.HeadlightDroop);

            foreach (var m in c.VisualModels)
                c.modelRotations[m] = V3(s, "ModelRotations", m, c.RotationFor(m));

            c.RiderHeight = F(s, "Rider", "RiderHeight", c.RiderHeight);
            c.AnimDict = s.GetValue("Rider", "AnimDict", c.AnimDict);
            c.AnimName = s.GetValue("Rider", "AnimName", c.AnimName);
            c.RiderAnimDict = s.GetValue("Rider", "RiderAnimDict", c.RiderAnimDict).Trim();
            c.CarveFullLeanDeg = Math.Max(5f, F(s, "Rider", "CarveFullLeanDeg", c.CarveFullLeanDeg));

            c.MaxSpeedKmh = F(s, "Ride", "MaxSpeedKmh", c.MaxSpeedKmh);
            c.MaxReverseKmh = F(s, "Ride", "MaxReverseKmh", c.MaxReverseKmh);
            c.Accel = F(s, "Ride", "Accel", c.Accel);
            c.Brake = F(s, "Ride", "Brake", c.Brake);
            c.CoastDecel = F(s, "Ride", "CoastDecel", c.CoastDecel);
            c.TurnRateLow = F(s, "Ride", "TurnRateLow", c.TurnRateLow);
            c.TurnFalloff = F(s, "Ride", "TurnFalloff", c.TurnFalloff);
            c.TiltbackRatio = F(s, "Ride", "TiltbackRatio", c.TiltbackRatio);
            c.CutoutEnabled = F(s, "Ride", "CutoutEnabled", c.CutoutEnabled ? 1 : 0) != 0;
            c.CutoutSeconds = F(s, "Ride", "CutoutSeconds", c.CutoutSeconds);
            c.CrashSpeedKmh = F(s, "Ride", "CrashSpeedKmh", c.CrashSpeedKmh);
            c.HardLandingSpeed = F(s, "Ride", "HardLandingSpeed", c.HardLandingSpeed);
            c.StepHeight = F(s, "Ride", "StepHeight", c.StepHeight);
            c.RangeKm = F(s, "Ride", "RangeKm", c.RangeKm);
            c.ParkedChargePerSecond = F(s, "Ride", "ParkedChargePerSecond", c.ParkedChargePerSecond);

            c.MaxLeanDeg = F(s, "Visual", "MaxLeanDeg", c.MaxLeanDeg);
            c.MaxRollDeg = F(s, "Visual", "MaxRollDeg", c.MaxRollDeg);
            c.LeanResponse = F(s, "Visual", "LeanResponse", c.LeanResponse);
            c.SlowTurnWheelTiltDeg = F(s, "Visual", "SlowTurnWheelTiltDeg", c.SlowTurnWheelTiltDeg);
            c.CounterLeanDeg = F(s, "Visual", "CounterLeanDeg", c.CounterLeanDeg);
            c.CounterLeanFadeSpeed = Math.Max(0.1f, F(s, "Visual", "CounterLeanFadeSpeed", c.CounterLeanFadeSpeed));
            c.RollSign = F(s, "Visual", "RollSign", c.RollSign);
            c.TiltbackDeg = F(s, "Visual", "TiltbackDeg", c.TiltbackDeg);
            c.ShowHud = F(s, "Visual", "ShowHud", c.ShowHud ? 1 : 0) != 0;
            c.UseMph = !s.GetValue("Visual", "SpeedUnits", "mph").Trim().Equals("kmh", StringComparison.OrdinalIgnoreCase);
            return c;
        }

        static Keys Key(ScriptSettings s, string section, string name, Keys def) =>
            Enum.TryParse(s.GetValue(section, name, def.ToString()).Trim(), true, out Keys k) ? k : def;

        // Parse numbers ourselves with the invariant culture so "1.5" works on every Windows locale.
        static float F(ScriptSettings s, string section, string name, float def)
        {
            var raw = s.GetValue(section, name, def.ToString(CultureInfo.InvariantCulture));
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        static Vector3 V3(ScriptSettings s, string section, string name, Vector3 def)
        {
            var raw = s.GetValue(section, name, "");
            var parts = raw.Split(',');
            if (parts.Length != 3) return def;
            var v = new float[3];
            for (int i = 0; i < 3; i++)
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return def;
            return new Vector3(v[0], v[1], v[2]);
        }
    }
}
