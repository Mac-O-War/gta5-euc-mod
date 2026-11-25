using System;
using System.Drawing;
using System.IO;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using Control = GTA.Control;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

namespace EucMod
{
    /// <summary>
    /// Electric unicycle, script-driven: an invisible anchor prop carries the visual model and the
    /// standing rider. Speed, steering, ground following and collisions are simulated here instead of
    /// using GTA's two-wheel bike physics, so the wheel can behave like a real EUC (self-levelling,
    /// tilt-back, overlean cut-out).
    /// </summary>
    public class EucScript : Script
    {
        enum State { None, Parked, Riding }

        // Shape-test flags: 1 map, 2 vehicles, 8 peds, 16 objects.
        const int ObstacleFlags = 1 | 2 | 8 | 16;
        const int GroundFlags = 1 | 2 | 16;
        const float SnapDown = 0.3f;     // follow the ground down this far per frame before going airborne
        const float MountRange = 1.8f;
        const float RadioRange = 5f;     // radio keys work this close to a parked wheel
        const float Deg2Rad = (float)(Math.PI / 180.0);

        static readonly Control[] RideBlockedControls =
        {
            Control.MoveLeftRight, Control.MoveUpDown, Control.MoveLeftOnly, Control.MoveRightOnly,
            Control.MoveUpOnly, Control.MoveDownOnly, Control.Sprint, Control.Jump, Control.Enter,
            Control.Attack, Control.Aim, Control.Cover, Control.Duck, Control.Reload, Control.SelectWeapon,
            Control.MeleeAttackLight, Control.MeleeAttackHeavy, Control.MeleeAttackAlternate,
        };

        Config cfg;
        readonly TextElement hud;
        readonly EucRadio radio;
        readonly RiderAnimator animator;

        State state = State.None;
        Prop root, visual;

        Vector3 pos;            // ground contact point under the axle
        Vector3 fwd = new Vector3(0, 1, 0);
        float heading;          // degrees, GTA convention
        float speed;            // m/s along heading, negative = reversing
        float vz;               // vertical velocity while airborne
        bool airborne;
        float airTime;
        float lastAccel;
        float battery = 1f;
        float overleanTimer, beepTimer;
        float landing;          // 0..1 landing-squat impulse, decays after touchdown
        float pitch, roll;      // smoothed rider lean (applied to the anchor the rider is attached to)
        float wheelRoll;        // smoothed wheel lean (differs from the rider's in slow turns)
        float visualLift;       // raises the visual so its lowest point touches the ground
        Vector3 visualRotation; // per-model rotation fix-up from the ini

        public EucScript()
        {
            cfg = Config.Load(Path.Combine(BaseDirectory, "EucMod.ini"));
            hud = new TextElement("", new PointF(1265f, 625f), 0.55f, Color.White, GTA.UI.Font.ChaletLondon, Alignment.Right, true, true);
            radio = new EucRadio(cfg, Log);
            animator = new RiderAnimator(cfg, Log);
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
        }

        // ---------------------------------------------------------------- events

        // Every entry point is wrapped: an exception escaping into SHVDN can take the whole game down.

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == cfg.SpawnKey && state != State.Riding)
            {
                Guard("spawn", () =>
                {
                    if (state == State.None) Spawn();
                    else Despawn();
                });
                return;
            }

            // Radio keys work while riding or standing next to the parked wheel.
            bool radioKey = e.KeyCode == cfg.RadioToggleKey || e.KeyCode == cfg.RadioNextKey || e.KeyCode == cfg.RadioPrevKey;
            if (!radioKey || state == State.None) return;
            Guard("radio", () =>
            {
                if (state == State.Parked && Game.Player.Character.Position.DistanceTo(pos) > RadioRange) return;
                if (e.KeyCode == cfg.RadioToggleKey) radio.Toggle();
                else radio.Step(e.KeyCode == cfg.RadioNextKey ? 1 : -1);
            });
        }

        void OnTick(object sender, EventArgs e)
        {
            if (state == State.None) return;
            Guard("tick", () =>
            {
                if (root == null || !root.Exists()) { ResetState(); return; }

                float dt = Math.Min(Game.LastFrameTime, 0.1f);
                Ped ped = Game.Player.Character;

                if (state == State.Parked) TickParked(ped, dt);
                else TickRiding(ped, dt);

                if (state != State.None) radio.Sync(visual ?? root, state == State.Riding, state == State.Parked);
            });
        }

        void OnAborted(object sender, EventArgs e)
        {
            Guard("abort", () =>
            {
                if (state == State.Riding) DetachRider(Game.Player.Character);
                Despawn();
            });
        }

        void Guard(string what, Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                Log($"{what} failed: {ex}");
                // Drop to a safe state; never rethrow.
                try
                {
                    if (state == State.Riding) Function.Call(Hash.DETACH_ENTITY, Game.Player.Character, true, true);
                    Despawn();
                }
                catch (Exception ex2) { Log($"cleanup failed: {ex2}"); ResetState(); }
                try { Notification.Show($"~r~EucMod error~s~ ({what}), see EucMod.log"); } catch { }
            }
        }

        void Log(string msg)
        {
            try { File.AppendAllText(Path.Combine(BaseDirectory, "EucMod.log"), $"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}"); }
            catch { }
        }

        // ---------------------------------------------------------------- spawn / park

        void Spawn()
        {
            Ped ped = Game.Player.Character;
            if (Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) || !Function.Call<bool>(Hash.IS_PLAYER_CONTROL_ON, Game.Player)
                || !ped.Exists() || ped.IsDead)
            {
                Notification.Show("EUC: wait until you have control of your character.");
                return;
            }
            if (ped.IsInVehicle()) { Notification.Show("EUC: get out of the vehicle first."); return; }

            root = CreateProp(cfg.RootModel, ped.Position);
            if (root == null) { Notification.Show($"EUC: root model '{cfg.RootModel}' not found."); return; }
            root.IsPositionFrozen = true;
            root.IsCollisionEnabled = false;

            foreach (var name in cfg.VisualModels)
            {
                visual = CreateProp(name, ped.Position);
                if (visual == null) continue;
                visualRotation = cfg.RotationFor(name);
                break;
            }
            if (visual != null)
            {
                // Not attached: hiding the anchor would hide attached children too.
                // SetPose moves the visual alongside the anchor every frame instead.
                visual.IsPositionFrozen = true;
                visual.IsCollisionEnabled = false;
                root.IsVisible = false;
                visualLift = GroundLift(visual.Model, visualRotation);
            }
            else
            {
                Notification.Show("EUC: no visual model found, showing the anchor prop instead.");
            }

            heading = ped.Heading;
            fwd = Forward(heading);
            pos = ped.Position + fwd * 1.5f;
            Park();

            Log($"spawn: player at {ped.Position}, euc ground point {pos}");
            Log($"  root '{cfg.RootModel}' handle={root.Handle} pos={root.Position} visible={root.IsVisible} alpha={Function.Call<int>(Hash.GET_ENTITY_ALPHA, root)}");
            if (visual != null)
                Log($"  visual handle={visual.Handle} pos={visual.Position} visible={visual.IsVisible} attached={Function.Call<bool>(Hash.IS_ENTITY_ATTACHED, visual)} model={visual.Model.Hash}");
            Notification.Show($"EUC spawned. ~INPUT_ENTER~ to ride, {cfg.SpawnKey} to remove.");
        }

        void Despawn()
        {
            radio.Stop();
            if (visual != null && visual.Exists()) visual.Delete();
            if (root != null && root.Exists()) root.Delete();
            ResetState();
        }

        void ResetState()
        {
            root = visual = null;
            state = State.None;
        }

        void Park()
        {
            speed = vz = 0f;
            pitch = roll = wheelRoll = 0f;
            airborne = false;
            overleanTimer = 0f;
            var g = World.Raycast(pos + new Vector3(0, 0, 1.5f), pos - new Vector3(0, 0, 20f), (IntersectFlags)GroundFlags, root);
            if (g.DidHit) pos = g.HitPosition;
            SetPose(pos, 0f, 0f, heading);
            state = State.Parked;
        }

        void TickParked(Ped ped, float dt)
        {
            battery = Math.Min(1f, battery + cfg.ParkedChargePerSecond * dt);
            if (ped.IsDead || ped.IsInVehicle() || ped.Position.DistanceTo(pos) > MountRange) return;

            Game.DisableControlThisFrame(Control.Enter);
            Screen.ShowHelpTextThisFrame($"~INPUT_ENTER~ Ride EUC ({battery * 100f:0}%)");
            if (DisabledJustPressed(Control.Enter)) Mount(ped);
        }

        // ---------------------------------------------------------------- mount / dismount / bail

        void Mount(Ped ped)
        {
            ped.Task.ClearAllImmediately();
            ped.CanRagdoll = false;
            Attach(ped, root, new Vector3(0f, 0f, cfg.RiderHeight), Vector3.Zero);
            animator.Start();
            landing = 0f;
            state = State.Riding;
        }

        void DetachRider(Ped ped)
        {
            Function.Call(Hash.DETACH_ENTITY, ped, true, true);
            ped.CanRagdoll = true;
            ped.Task.ClearAllImmediately();
        }

        void Dismount(Ped ped)
        {
            DetachRider(ped);
            var right = new Vector3(fwd.Y, -fwd.X, 0f);
            ped.Position = pos + right * 0.6f + new Vector3(0f, 0f, 1f);
            ped.Heading = heading;
            Park();
        }

        void Bail(Ped ped, string reason)
        {
            var throwVel = fwd * speed + new Vector3(0f, 0f, 1.5f);
            DetachRider(ped);
            Function.Call(Hash.SET_PED_TO_RAGDOLL, ped, 3000, 4000, 0, false, false, false);
            ped.Velocity = throwVel;
            Screen.ShowSubtitle(reason, 2500);
            Park();
        }

        // ---------------------------------------------------------------- riding

        void TickRiding(Ped ped, float dt)
        {
            if (!ped.Exists() || ped.IsDead) { DetachRider(ped); Park(); return; }

            foreach (var c in RideBlockedControls) Game.DisableControlThisFrame(c);
            if (DisabledJustPressed(Control.Enter)) { Dismount(ped); return; }
            if (Function.Call<bool>(Hash.IS_ENTITY_IN_WATER, ped)) { Bail(ped, "Splash. EUCs don't swim."); return; }

            float throttle = -DisabledNormal(Control.MoveUpDown); // stick/W forward = +1
            float steer = DisabledNormal(Control.MoveLeftRight);  // right = +1

            // --- speed
            float maxSpd = cfg.MaxSpeedKmh / 3.6f * BatterySpeedFactor();
            float maxRev = cfg.MaxReverseKmh / 3.6f;
            float tiltStart = maxSpd * cfg.TiltbackRatio;

            float accel;
            if (throttle > 0.05f) accel = speed < 0f ? throttle * cfg.Brake : throttle * cfg.Accel;
            else if (throttle < -0.05f) accel = speed > 0f ? throttle * cfg.Brake : throttle * cfg.Accel * 0.5f;
            else accel = -Math.Sign(speed) * Math.Min(cfg.CoastDecel, Math.Abs(speed) / dt);

            bool tiltback = speed > tiltStart;
            if (tiltback && accel > 0f) accel *= Clamp((maxSpd - speed) / Math.Max(maxSpd - tiltStart, 0.01f), 0f, 1f);

            speed = Clamp(speed + accel * dt, -maxRev, maxSpd);
            lastAccel = accel;

            // Pushing hard into tilt-back for too long = overlean.
            if (tiltback && throttle > 0.9f) overleanTimer += dt;
            else overleanTimer = Math.Max(0f, overleanTimer - dt * 2f);
            if (cfg.CutoutEnabled && overleanTimer > cfg.CutoutSeconds) { Bail(ped, "~r~Overlean cut-out!"); return; }

            // --- steering (EUCs can pivot on the spot)
            float turnRate = cfg.TurnRateLow / (1f + Math.Abs(speed) / cfg.TurnFalloff);
            heading = WrapDeg(heading - steer * turnRate * dt);
            fwd = Forward(heading);

            // --- obstacles
            if (Math.Abs(speed) > 0.05f && HitObstacle(out var hitEntity))
            {
                if (hitEntity is Ped other && other.Exists() && other != ped) other.Ragdoll(3000);
                if (Math.Abs(speed) * 3.6f >= cfg.CrashSpeedKmh) { Bail(ped, "~r~Crash!"); return; }
                speed = 0f;
            }

            // --- ground follow / gravity
            var next = pos + fwd * speed * dt;
            var g = World.Raycast(new Vector3(next.X, next.Y, pos.Z + cfg.StepHeight),
                                  new Vector3(next.X, next.Y, pos.Z - 50f), (IntersectFlags)GroundFlags, root);
            if (!airborne)
            {
                if (g.DidHit && g.HitPosition.Z >= pos.Z - SnapDown)
                {
                    // Track the ground's vertical speed so ramps launch you when the ground falls away.
                    vz = Clamp((g.HitPosition.Z - pos.Z) / dt, -10f, 3f);
                    next.Z = g.HitPosition.Z;
                }
                else
                {
                    airborne = true;
                    airTime = 0f;
                }
            }
            if (airborne)
            {
                airTime += dt;
                vz -= 9.81f * dt;
                next.Z = pos.Z + vz * dt;
                if (g.DidHit && next.Z <= g.HitPosition.Z)
                {
                    if (-vz > cfg.HardLandingSpeed) { pos = g.HitPosition; Bail(ped, "~r~Hard landing!"); return; }
                    landing = Math.Max(landing, Clamp(-vz / 6f, 0.3f, 1f));  // absorb the landing with a squat
                    next.Z = g.HitPosition.Z;
                    vz = 0f;
                    airborne = false;
                }
                else if (airTime > 5f) { Bail(ped, "Lost the ground."); return; }
            }
            pos = next;

            // --- battery
            float load = 1f + Math.Max(0f, accel) / cfg.Accel;
            battery = Math.Max(0f, battery - Math.Abs(speed) * dt / (cfg.RangeKm * 1000f) * load);

            // --- visual lean: forward when accelerating, back on tilt-back, into turns.
            // The wheel self-levels, so slopes don't tilt the rider.
            float leanBack = Clamp(-accel / cfg.Accel, -1f, 1f) * cfg.MaxLeanDeg;
            if (leanBack > 0f && animator.UsingCustom) leanBack *= 0.35f;  // the brake pose already shifts the weight back
            float targetPitch = leanBack + (tiltback ? cfg.TiltbackDeg : 0f);
            // Lean that balances the turn: tan(lean) = v * yawRate / g.
            float yawRateRad = steer * turnRate * Deg2Rad;
            float balanceLean = (float)Math.Atan(speed * yawRateRad / 9.81f) / Deg2Rad;
            balanceLean = Clamp(balanceLean, -cfg.MaxRollDeg, cfg.MaxRollDeg);

            // Slow turns: riders tilt the wheel into the turn with their legs while the body
            // counter-leans to stay over it. 'slow' fades this out as speed rises; 'moving' keeps
            // it off when pivoting on the spot. At speed both converge on the balance lean.
            float slow = 1f - Clamp(Math.Abs(speed) / cfg.CounterLeanFadeSpeed, 0f, 1f);
            float moving = Clamp(Math.Abs(speed) / 0.8f, 0f, 1f);
            float lowSpeedTurn = steer * slow * moving;
            float targetWheelRoll = (balanceLean + lowSpeedTurn * cfg.SlowTurnWheelTiltDeg) * cfg.RollSign;
            float targetRiderRoll = (balanceLean * (1f - slow) - lowSpeedTurn * cfg.CounterLeanDeg) * cfg.RollSign;

            float k = 1f - (float)Math.Exp(-8f * dt);
            float kRoll = 1f - (float)Math.Exp(-cfg.LeanResponse * dt);
            pitch += (targetPitch - pitch) * k;
            roll += (targetRiderRoll - roll) * kRoll;
            wheelRoll += (targetWheelRoll - wheelRoll) * kRoll;
            SetPose(pos, pitch, roll, heading, wheelRoll);

            // --- body posture (custom clips): carve from the physical lean, squat when braking/landing, tuck in the air
            float braking = speed > 0.3f && accel < 0f && throttle < -0.05f
                ? Clamp(-accel / cfg.Brake, 0f, 1f) * Clamp(speed / 3f, 0f, 1f) : 0f;
            landing = Math.Max(0f, landing - dt * 2.5f);
            animator.Update(ped, dt, balanceLean / cfg.CarveFullLeanDeg, Math.Max(braking, landing), airborne);

            // --- feedback
            if (tiltback)
            {
                beepTimer -= dt;
                if (beepTimer <= 0f)
                {
                    Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "5_SEC_WARNING", "HUD_MINI_GAME_SOUNDSET", true);
                    beepTimer = 0.35f;
                }
            }
            if (cfg.ShowHud) DrawHud(tiltback);
        }

        bool HitObstacle(out Entity hitEntity)
        {
            hitEntity = null;
            var dir = speed > 0f ? fwd : -fwd;
            float len = Math.Abs(speed) * Math.Min(Game.LastFrameTime, 0.1f) + 0.3f;
            foreach (float h in new[] { 0.45f, 1.1f })
            {
                var from = pos + new Vector3(0f, 0f, h) + dir * 0.45f;
                var hit = World.Raycast(from, from + dir * len, (IntersectFlags)ObstacleFlags, root);
                // Ignore walkable slopes; only near-vertical surfaces block.
                if (hit.DidHit && hit.SurfaceNormal.Z < 0.6f)
                {
                    hitEntity = hit.HitEntity;
                    return true;
                }
            }
            return false;
        }

        float BatterySpeedFactor() => battery >= 0.2f ? 1f : 0.3f + 3.5f * battery;


        void DrawHud(bool tiltback)
        {
            string spd = cfg.UseMph ? $"{Math.Abs(speed) * 2.23694f:0} mph" : $"{Math.Abs(speed) * 3.6f:0} km/h";
            hud.Caption = $"{spd}  ~n~BAT {battery * 100f:0}%";
            hud.Color = tiltback ? Color.OrangeRed : battery < 0.2f ? Color.Gold : Color.White;
            hud.Draw();
        }

        // ---------------------------------------------------------------- helpers

        Prop CreateProp(string name, Vector3 at)
        {
            var model = new Model(name);
            bool inImage = model.IsInCdImage;
            bool loaded = inImage && model.Request(2000);
            var prop = loaded ? World.CreateProp(model, at, false, false) : null;
            Log($"  CreateProp '{name}': inCdImage={inImage} loaded={loaded} handle={(prop != null ? prop.Handle : 0)}");
            model.MarkAsNoLongerNeeded();
            return prop;
        }

        /// <summary>
        /// How far to raise a model so the lowest corner of its bounding box (after applying the
        /// configured rotation) sits on the ground. Models with their origin at the bottom get ~0.
        /// </summary>
        float GroundLift(Model model, Vector3 rotDeg)
        {
            var (min, max) = model.Dimensions;
            float p = rotDeg.X * Deg2Rad, r = rotDeg.Y * Deg2Rad;
            float minZ = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
                // roll about Y, then pitch about X; yaw doesn't change height
                float x1 = c.X * (float)Math.Cos(r) + c.Z * (float)Math.Sin(r);
                float z1 = -c.X * (float)Math.Sin(r) + c.Z * (float)Math.Cos(r);
                float z2 = c.Y * (float)Math.Sin(p) + z1 * (float)Math.Cos(p);
                minZ = Math.Min(minZ, z2);
            }
            Log($"  visual dims min={min} max={max} size={max - min} -> lift {-minZ:0.000}");
            return -minZ;
        }

        static void Attach(Entity child, Entity parent, Vector3 offset, Vector3 rot)
        {
            Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY, child, parent, 0,
                offset.X, offset.Y, offset.Z, rot.X, rot.Y, rot.Z,
                false, false, false, false, 2, true);
        }

        /// <summary>Root (and the attached rider) gets rollDeg; the wheel model gets wheelRollDeg.</summary>
        void SetPose(Vector3 p, float pitchDeg, float rollDeg, float headingDeg, float? wheelRollDeg = null)
        {
            float wr = wheelRollDeg ?? rollDeg;
            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, root, p.X, p.Y, p.Z, false, false, false);
            Function.Call(Hash.SET_ENTITY_ROTATION, root, pitchDeg, rollDeg, headingDeg, 2, true);

            if (visual != null && visual.Exists())
            {
                var off = cfg.VisualOffset + new Vector3(0f, 0f, visualLift);
                var vp = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, root, off.X, off.Y, off.Z);
                var vr = visualRotation;
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, visual, vp.X, vp.Y, vp.Z, false, false, false);
                Function.Call(Hash.SET_ENTITY_ROTATION, visual, pitchDeg + vr.X, wr + vr.Y, headingDeg + vr.Z, 2, true);
            }
        }

        static float DisabledNormal(Control c) => Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)c);
        static bool DisabledJustPressed(Control c) => Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, (int)c);

        static Vector3 Forward(float headingDeg)
        {
            float r = headingDeg * Deg2Rad;
            return new Vector3(-(float)Math.Sin(r), (float)Math.Cos(r), 0f);
        }

        static float WrapDeg(float d) => ((d % 360f) + 360f) % 360f;
        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
