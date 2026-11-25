using System;
using GTA;
using GTA.Native;
using GTA.UI;

namespace EucMod
{
    /// <summary>
    /// The EUC's built-in Bluetooth speaker.
    /// Speaker mode: a positional radio emitter linked to the wheel (the same mechanism as GTA
    /// Online's boombox), so the music comes from the wheel in 3D.
    /// Headphones mode: GTA's on-foot "mobile radio", non-positional, only while riding.
    /// Sync() is called every tick and only touches the game when something actually changed.
    /// </summary>
    internal sealed class EucRadio
    {
        string Emitter => cfg.RadioEmitter;

        readonly Config cfg;
        readonly Action<string> log;

        public bool On { get; private set; }
        int station;

        // What is currently applied in-game.
        bool playing, playingHeadphones;
        int playingStation = -1, playingEntity;

        public EucRadio(Config cfg, Action<string> log)
        {
            this.cfg = cfg;
            this.log = log;
            station = Math.Max(0, cfg.RadioStartStation);
        }

        public void Toggle()
        {
            On = !On;
            Announce();
        }

        public void Step(int dir)
        {
            int n = StationCount();
            if (n <= 0) return;
            station = ((station + dir) % n + n) % n;
            On = true;
            Announce();
        }

        /// <summary>Make the game match the desired state. Cheap when nothing changed.</summary>
        public void Sync(Entity speaker, bool riding, bool parked)
        {
            bool headphones = cfg.RadioHeadphones;
            bool shouldPlay = riding || (parked && cfg.RadioWhenParked && !headphones);
            bool want = On && shouldPlay && speaker != null && speaker.Exists();

            if (!want) { Stop(); return; }
            if (playing && playingStation == station && playingEntity == speaker.Handle && playingHeadphones == headphones) return;

            Stop();
            string name = StationName(station);
            if (headphones)
            {
                Function.Call(Hash.SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY, true);
                Function.Call(Hash.SET_MOBILE_PHONE_RADIO_STATE, true);
                Function.Call(Hash.SET_RADIO_TO_STATION_NAME, name);
            }
            else
            {
                Function.Call(Hash.LINK_STATIC_EMITTER_TO_ENTITY, Emitter, speaker);
                Function.Call(Hash.SET_EMITTER_RADIO_STATION, Emitter, name);
                Function.Call(Hash.SET_STATIC_EMITTER_ENABLED, Emitter, true);
            }
            playing = true;
            playingHeadphones = headphones;
            playingStation = station;
            playingEntity = speaker.Handle;
            log($"radio: playing '{name}' ({(headphones ? "headphones" : "speaker")}) on entity {speaker.Handle}");
        }

        public void Stop()
        {
            if (!playing) return;
            if (playingHeadphones)
            {
                Function.Call(Hash.SET_MOBILE_PHONE_RADIO_STATE, false);
                Function.Call(Hash.SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY, false);
            }
            else
            {
                Function.Call(Hash.SET_STATIC_EMITTER_ENABLED, Emitter, false);
            }
            playing = false;
            playingStation = -1;
            playingEntity = 0;
        }

        void Announce()
        {
            if (!On) { Screen.ShowSubtitle("Radio off", 1500); return; }
            string name = StationName(station);
            string label = Game.GetLocalizedString(name);
            Screen.ShowSubtitle($"Radio: {(string.IsNullOrEmpty(label) || label == "NULL" ? name : label)}", 2000);
        }

        static int StationCount() => Function.Call<int>(Hash.GET_NUM_UNLOCKED_RADIO_STATIONS);
        static string StationName(int i) => Function.Call<string>(Hash.GET_RADIO_STATION_NAME, i);
    }
}
