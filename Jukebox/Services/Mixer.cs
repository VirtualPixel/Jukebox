using System;
using System.Collections.Generic;
using Photon.Voice;
using UnityEngine;
using UnityEngine.Audio;
using Jukebox.Configuration;
using Jukebox.Models;

namespace Jukebox.Services
{
    // The mixer. Clips are written into your own voice stream on the way to the encoder, on
    // top of the microphone, so you keep talking through them and the whole room hears them
    // with nothing installed. The mixers ride the recorder's voice object as pre-processors on
    // the audio thread, so the game thread only ever appends to a list under a lock. You hear
    // each clip too, through the game's own voice mixer, since the game keeps your own voice
    // silent locally and you would otherwise play deaf.
    internal static class Mixer
    {
        private const int Rate = Library.Rate;
        // A clip cannot restart on top of itself inside this window, and only this many clips
        // can sound at once, so a flickering trigger never stacks copies into a wall of noise.
        private const float RepeatWindow = 0.3f;
        private const int MaxAtOnce = 5;

        private static readonly Dictionary<string, float> lastStarted = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> turn = new Dictionary<string, int>();

        private sealed class Cue
        {
            public string Name = "";
            public float[] Data = Array.Empty<float>();
            public double Pos;
            public float Gain = 1f;
            public float Rate = 1f;
            public bool Loop;
            // A death jingle or a game over: it plays whatever else is going, and nothing that
            // starts after it pushes it out.
            public bool Sticky;
        }

        // Your voice stays on top. The buffer handed to the mixer is your microphone, so its
        // level is measured before anything is added; while you are talking, every clip is held
        // to at most DuckRatio of that level (the game's own sounds are not in this stream and
        // are not touched). The envelope opens at once and lets go over about half a second, so
        // a pause between words does not pump the music up and down.
        private static float voiceEnvelope;
        private static float duck = Levels.MaxGain;
        private const float VoiceFloor = 0.008f;
        private const float ReleaseSeconds = 0.6f;

        private static readonly List<Cue> cues = new List<Cue>();
        private static readonly HashSet<string> missingWarned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object gate = new object();
        private static object? attachedVoice;
        // The rate the microphone actually opened at. The recorder asks for 48 kHz but the
        // device has the last word, and the mixer runs ahead of the resampler.
        private static volatile int streamRate = Rate;
        // The transmit gate as the game set it, and whether a clip is holding it open past that.
        private static volatile bool micOpen = true;
        private static volatile bool holding;
        private static readonly Dictionary<string, AudioClip> monitorClips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private static AudioSource? monitor;
        private static AudioSource? monitorLoop;
        private static string monitorLoopName = "";
        private static string lastPath = "";

        public static bool MicOpen
        {
            set => micOpen = value;
        }

        public static bool Holding
        {
            get => holding;
            set => holding = value;
        }

        public static bool Attached => attachedVoice != null;

        public static bool Playing
        {
            get
            {
                lock (gate)
                {
                    return cues.Count > 0;
                }
            }
        }

        // Nothing plays until you are somewhere the room can see you: the truck, a level, the
        // shop, the arena. Not the main menu, not the lobby menu, not a loading screen.
        public static bool OnStage =>
            !PluginConfig.QuietInMenus.Value || (InPlace && GameDirector.instance
                && GameDirector.instance.currentState == GameDirector.gameState.Main);

        // Somewhere the room can see you, whatever state the game director is in.
        public static bool InPlace => !SemiFunc.MenuLevel() && !SemiFunc.RunIsLobbyMenu();

        // Whether clips go to the others straight instead of through your voice.
        public static bool Direct => PluginConfig.DirectSound.Value && Room.EveryoneHasJukebox;

        // Seconds of a clip (the first entry of a list, trim stripped), 0 when unknown. A cue
        // pointed at a file of the player's own answers with that file's length, since that is
        // what is going to play.
        public static float Seconds(string name)
        {
            string first = Bare(name);
            string? mapped = Soundboard.Redirect(first);
            if (mapped == "")
            {
                return 0f;
            }
            float[]? data = Library.Samples(mapped != null ? Bare(mapped) : first);
            return data == null ? 0f : data.Length / (float)Rate;
        }

        // The first entry of a comma list with any level trim taken off, which is the name the
        // clip is filed under.
        private static string Bare(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "";
            }
            string first = name.Split(',')[0];
            int at = first.LastIndexOf('@');
            return (at > 0 ? first.Substring(0, at) : first).Trim();
        }

        // A moment's entry may list several files, comma separated. They take turns in order,
        // so a list of two alternates and a list of five cycles, and the same clip never plays
        // twice in a row while there is another to reach for.
        private static string Pick(string name)
        {
            if (name.IndexOf(',') < 0)
            {
                return name.Trim();
            }
            string[] options = name.Split(',');
            turn.TryGetValue(name, out int index);
            turn[name] = (index + 1) % options.Length;
            return options[index].Trim();
        }

        // "smb_coin@0.5" plays that file at half the usual level; a bright clip that still
        // reads loud after levelling can be trimmed right in the config.
        private static string Trim(string name, ref float gain)
        {
            int at = name.LastIndexOf('@');
            if (at > 0 && float.TryParse(name.Substring(at + 1), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float trim))
            {
                gain = Levels.Trimmed(gain, trim);
                return name.Substring(0, at).Trim();
            }
            return name;
        }

        // Drops every copy of a clip, looping or not, from the stream and from your own copy.
        // Whoever started it asked for it under the name the mod uses, so a cue the player has
        // pointed somewhere else has to come off under both names.
        public static void Stop(string name)
        {
            string first = Bare(name);
            if (first.Length == 0)
            {
                return;
            }
            Drop(first);
            string? mapped = Soundboard.Redirect(first);
            if (mapped == null)
            {
                return;
            }
            foreach (string part in mapped.Split(','))
            {
                Drop(Bare(part));
            }
        }

        private static void Drop(string name)
        {
            if (name.Length == 0)
            {
                return;
            }
            lock (gate)
            {
                cues.RemoveAll(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            }
            if (monitorLoop && string.Equals(monitorLoopName, name, StringComparison.OrdinalIgnoreCase))
            {
                monitorLoop!.Stop();
                monitorLoopName = "";
            }
        }

        public static void StopAll()
        {
            lock (gate)
            {
                cues.Clear();
            }
            if (monitor)
            {
                monitor!.Stop();
            }
            if (monitorLoop)
            {
                monitorLoop!.Stop();
                monitorLoopName = "";
            }
        }

        // A shared sound is one the whole room gets from the same event (the level start, the
        // extraction, the music): when a friend with the mod is close enough to hear, and in a
        // lower seat, theirs plays and yours stays quiet, so nobody hears it twice.
        public static void Play(string name, float gain, float pitch, bool loop, bool shared, bool outLoud, bool sticky)
        {
            // A sticky clip is a death or a game over, which the game marks as a state of its
            // own; it only has to be somewhere the room can see you.
            if (string.IsNullOrWhiteSpace(name) || !Gate.Active || !(sticky ? InPlace : OnStage))
            {
                return;
            }
            // A loop is never skipped here: with everyone on the mod it is yours alone, and
            // through the voice the caller decides for itself whose turn it is.
            if (shared && !loop && Room.NearbyLowerSeat(14f, Library.SpaceOf(name)))
            {
                Jukebox.LogInfo("jukebox: " + name + " is a friend's to play.");
                return;
            }
            name = Trim(Pick(name), ref gain);
            // The soundboard door: a cue the player has pointed at a file of their own plays
            // that file instead, wherever the cue was asked for.
            string? mapped = Soundboard.Redirect(name);
            if (mapped == "")
            {
                // Set to off in cues.cfg.
                return;
            }
            if (mapped != null)
            {
                name = Trim(Pick(mapped), ref gain);
            }
            if (name.Length == 0)
            {
                return;
            }
            gain *= PluginConfig.Volume.Value;
            float now = Time.unscaledTime;
            if (!loop && !sticky && lastStarted.TryGetValue(name, out float last) && now - last < RepeatWindow)
            {
                return;
            }
            lastStarted[name] = now;
            float[]? data = Library.Samples(name);
            if (data == null)
            {
                if (missingWarned.Add(name))
                {
                    Jukebox.LogAlways("jukebox: nothing is filed under " + name + ".");
                }
                return;
            }
            // With everyone in the room on the mod, nothing goes through your voice: your own
            // copy plays here, a one-shot is sent to them and plays at your Semibot on their
            // side, and a loop (music) is yours alone, since they have their own.
            // A clip of your own (your sounds folder, your sets) is not on anyone else's disk, so
            // it goes through the voice even in a room where everyone has the mod.
            bool direct = Direct && !Library.LocalOnly(name);
            // A loop through the voice is a song from your mouth for the whole lobby, and the
            // people without the mod did not ask for one. Your own copy plays either way; the
            // effects and the lines still go out.
            bool quietLoop = loop && !outLoud && !direct && !PluginConfig.MusicThroughVoice.Value && Photon.Pun.PhotonNetwork.InRoom;
            string path = direct ? "direct" : quietLoop ? "voice, loops quiet" : "voice";
            if (path != lastPath)
            {
                lastPath = path;
                Jukebox.LogAlways("jukebox: " + path + " (" + Room.Others + " others with the mod, host " + (Room.HostHasMod ? "modded" : "plain") + ").");
            }
            bool toVoice = !direct && !quietLoop;
            if (toVoice && attachedVoice == null)
            {
                // No voice stream to go into (singleplayer, or the recorder is between
                // restarts). Queued, it would sit there and burst out on the next one.
                Say("jukebox: " + name + " not sent, no voice stream right now.");
                toVoice = false;
            }
            if (toVoice)
            {
                lock (gate)
                {
                    if (loop)
                    {
                        cues.RemoveAll(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                    }
                    // A loop is only ever dropped by Stop, and a sticky clip never.
                    int victim = cues.Count >= MaxAtOnce ? cues.FindIndex(c => !c.Loop && !c.Sticky) : -1;
                    Admit admit = VoiceGate.Room(cues.Count, MaxAtOnce, victim >= 0, sticky);
                    if (admit == Admit.MakeRoom)
                    {
                        cues.RemoveAt(victim);
                    }
                    toVoice = admit != Admit.LocalOnly;
                    if (toVoice)
                    {
                        if (cues.Count == 0)
                        {
                            // The level from the last clip is stale; the short path skips the
                            // measuring while nothing plays.
                            voiceEnvelope = 0f;
                            duck = Levels.MaxGain;
                        }
                        cues.Add(new Cue { Name = name, Data = data, Gain = Mathf.Clamp(gain, 0f, 2f), Rate = Mathf.Clamp(pitch, 0.5f, 2f), Loop = loop, Sticky = sticky });
                    }
                }
            }
            if (toVoice)
            {
                holding = true;
                Say("jukebox: " + name + " to voice, " + data.Length + " samples at " + Rate + " into " + streamRate + " Hz, gate "
                    + (micOpen ? "open" : "held open for it, mic kept shut") + ".");
            }
            // With HearWhatGoesOut on you hear the voice stream itself, so the local copy of
            // anything sent into it would only cover up whether it arrived.
            if (!(toVoice && PluginConfig.HearWhatGoesOut.Value))
            {
                Monitor(name, data, gain, pitch, loop);
            }
            if (direct && (!loop || outLoud))
            {
                Net.Sound(name, Mathf.Clamp(gain, 0f, 2f), Mathf.Clamp(pitch, 0.5f, 2f));
            }
            Jukebox.LogInfo("jukebox: " + name);
        }

        // A friend's sound, played at their Semibot (or their head, when they are dead) through
        // the same mixer their voice comes through, at the level their voice would carry it.
        private static readonly Dictionary<int, AudioSource> mouths = new Dictionary<int, AudioSource>();
        private static readonly System.Reflection.FieldInfo? voiceSource = HarmonyLib.AccessTools.Field(typeof(PlayerVoiceChat), "audioSource");

        public static void PlayFrom(int actor, string name, float gain, float pitch)
        {
            float[]? data = string.IsNullOrWhiteSpace(name) ? null : Library.Samples(name.Trim());
            if (!Gate.Active || !OnStage || data == null)
            {
                return;
            }
            PlayerAvatar? who = null;
            foreach (PlayerAvatar other in SemiFunc.PlayerGetAll())
            {
                if (other && other.photonView && other.photonView.Owner != null && other.photonView.Owner.ActorNumber == actor)
                {
                    who = other;
                    break;
                }
            }
            if (who == null)
            {
                return;
            }
            Transform at = who.isDisabled && who.playerDeathHead ? who.playerDeathHead.transform : who.transform;
            if (!mouths.TryGetValue(actor, out AudioSource mouth) || !mouth)
            {
                GameObject go = new GameObject("Jukebox mouth");
                mouth = go.AddComponent<AudioSource>();
                mouth.playOnAwake = false;
                mouth.spatialBlend = 1f;
                mouth.dopplerLevel = 0f;
                mouth.rolloffMode = AudioRolloffMode.Logarithmic;
                mouth.minDistance = 1.5f;
                mouth.maxDistance = 30f;
                // Their voice's own falloff, when it can be read.
                if (who.voiceChat && voiceSource?.GetValue(who.voiceChat) is AudioSource theirs && theirs)
                {
                    mouth.rolloffMode = theirs.rolloffMode;
                    mouth.minDistance = theirs.minDistance;
                    mouth.maxDistance = theirs.maxDistance;
                    mouth.spread = theirs.spread;
                }
                mouths[actor] = mouth;
            }
            PlayerVoiceChat own = PlayerVoiceChat.instance;
            if (own && own.mixerMicrophoneSound && mouth.outputAudioMixerGroup != own.mixerMicrophoneSound)
            {
                mouth.outputAudioMixerGroup = own.mixerMicrophoneSound;
            }
            mouth.transform.SetParent(at, false);
            mouth.transform.localPosition = who.isDisabled ? Vector3.zero : Vector3.up * 1.4f;
            mouth.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            mouth.PlayOneShot(Clip(name.Trim(), data), Levels.Clamp(gain) * Levels.RoomGain);
        }

        // A change of audio device resets Unity's audio: every clip made at runtime is gone
        // and every source is stopped, so the copies you hear of your own sounds went quiet
        // until the game was restarted. Forget the lot and it is all rebuilt on the next play.
        public static void OnAudioReset()
        {
            foreach (AudioClip clip in monitorClips.Values)
            {
                if (clip) UnityEngine.Object.Destroy(clip);
            }
            monitorClips.Clear();
            if (monitor) UnityEngine.Object.Destroy(monitor);
            if (monitorLoop) UnityEngine.Object.Destroy(monitorLoop);
            monitor = null;
            monitorLoop = null;
            foreach (AudioSource mouth in mouths.Values)
            {
                if (mouth) UnityEngine.Object.Destroy(mouth);
            }
            mouths.Clear();
            Jukebox.LogInfo("jukebox: audio device changed, clips rebuilt.");
        }

        private static AudioClip Clip(string name, float[] data)
        {
            if (!monitorClips.TryGetValue(name, out AudioClip clip) || !clip)
            {
                clip = AudioClip.Create(name, data.Length, 1, Rate, false);
                clip.SetData(data, 0);
                monitorClips[name] = clip;
            }
            return clip;
        }

        // The voices' group read off the game's voice prefab, for singleplayer, where there is
        // no voice chat to take it from. Loaded once; the prefab is never instantiated.
        private static AudioMixerGroup? prefabVoices;
        private static bool prefabLooked;

        private static AudioMixerGroup? PrefabVoices()
        {
            if (!prefabLooked)
            {
                prefabLooked = true;
                GameObject? prefab = Resources.Load<GameObject>("Voice");
                PlayerVoiceChat? chat = prefab ? prefab!.GetComponent<PlayerVoiceChat>() : null;
                prefabVoices = chat ? chat!.mixerMicrophoneSound : null;
                if (!prefabVoices)
                {
                    Jukebox.LogAlways("jukebox: no voice group on the voice prefab, your own copy plays straight out.");
                }
            }
            return prefabVoices;
        }

        // The local copy. A 2D source on the plugin object, routed into the same mixer group
        // the room's voices come through, so it carries the voice bus processing and volume.
        // The room hears the clip at the game's per-player voice gain (0.5) and then with
        // distance falloff, so the copy is scaled by that gain and a typical few-metres falloff
        // to land where a teammate standing near you would hear it.
        private static void Monitor(string name, float[] data, float gain, float pitch, bool loop)
        {
            if (!PluginConfig.HearOwnSounds.Value)
            {
                return;
            }
            if (!monitor)
            {
                monitor = Jukebox.Instance.gameObject.AddComponent<AudioSource>();
                monitor.spatialBlend = 0f;
                monitor.playOnAwake = false;
                monitor.loop = false;
                monitorLoop = Jukebox.Instance.gameObject.AddComponent<AudioSource>();
                monitorLoop.spatialBlend = 0f;
                monitorLoop.playOnAwake = false;
                monitorLoop.loop = true;
            }
            PlayerVoiceChat own = PlayerVoiceChat.instance;
            bool voiceChat = own && own.mixerMicrophoneSound;
            AudioMixerGroup? voices = voiceChat ? own.mixerMicrophoneSound : PrefabVoices();
            AudioMixerGroup? group = Levels.CopyBus(voiceChat, voices) == Bus.Voice ? voices : null;
            if (monitor!.outputAudioMixerGroup != group)
            {
                monitor.outputAudioMixerGroup = group;
                monitorLoop!.outputAudioMixerGroup = group;
            }
            AudioClip clip = Clip(name, data);
            float level = Levels.Copy(gain, PluginConfig.HearOwnSoundsVolume.Value);
            if (loop)
            {
                monitorLoop!.Stop();
                monitorLoop.clip = clip;
                monitorLoop.volume = level;
                monitorLoop.pitch = 1f;
                monitorLoop.Play();
                monitorLoopName = name;
                return;
            }
            monitor!.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            monitor.PlayOneShot(clip, level);
        }

        // Called whenever the recorder shows a new voice object (every restart rebuilds it).
        // rate and channels are the microphone's, which is what the mixer sees; a rate of 0
        // is taken as the clips' own.
        public static bool Attach(object voice, int rate, int channels)
        {
            if (voice == attachedVoice)
            {
                // Someone else handed it over first, without the mic's rate.
                if (rate > 0 && rate != streamRate)
                {
                    streamRate = rate;
                    Say("jukebox: voice stream is " + rate + " Hz.");
                }
                return true;
            }
            if (channels != 1)
            {
                Say("jukebox: the voice stream has " + channels + " channels, clips stay out of it.");
                return false;
            }
            // Set before the processor goes on, since it starts mixing on the next buffer.
            streamRate = rate > 0 ? rate : Rate;
            if (voice is LocalVoiceAudioFloat floats)
            {
                floats.AddPreProcessor(new FloatMixer());
            }
            else if (voice is LocalVoiceAudioShort shorts)
            {
                shorts.AddPreProcessor(new ShortMixer());
            }
            else
            {
                return false;
            }
            attachedVoice = voice;
            Say("jukebox: mixer attached to the voice stream, " + streamRate + " Hz.");
            return true;
        }

        // The recorder stopped. Whatever was queued would otherwise wait for the next stream.
        public static void Detach()
        {
            if (attachedVoice == null)
            {
                return;
            }
            attachedVoice = null;
            lock (gate)
            {
                cues.Clear();
            }
            holding = false;
        }

        // A clip's trip into the voice, logged when you asked to check it or asked for logging.
        private static void Say(string message)
        {
            if (PluginConfig.HearWhatGoesOut.Value)
            {
                Jukebox.LogAlways(message);
            }
            else
            {
                Jukebox.LogInfo(message);
            }
        }

        private static void Mix(float[] buf)
        {
            lock (gate)
            {
                bool sounding = cues.Count > 0;
                bool mic = micOpen;
                bool talking = Listen(buf) > VoiceFloor;
                if (!VoiceGate.KeepMic(mic, talking, sounding))
                {
                    Array.Clear(buf, 0, buf.Length);
                }
                if (!sounding)
                {
                    return;
                }
                int rate = streamRate;
                float gainScale = DuckFor(mic && talking);
                for (int c = cues.Count - 1; c >= 0; c--)
                {
                    Cue cue = cues[c];
                    float[] data = cue.Data;
                    double pos = cue.Pos;
                    double step = VoiceGate.Step(cue.Rate, Rate, rate);
                    int last = data.Length - 1;
                    float gain = Levels.InStream(cue.Gain, gainScale);
                    for (int i = 0; i < buf.Length; i++, pos += step)
                    {
                        if (pos >= last)
                        {
                            if (!cue.Loop || last <= 0)
                            {
                                break;
                            }
                            pos = VoiceGate.Wrap(pos, last);
                        }
                        int a = (int)pos;
                        float t = (float)(pos - a);
                        float sample = data[a] + (data[a + 1] - data[a]) * t;
                        buf[i] = Mathf.Clamp(buf[i] + sample * gain, -1f, 1f);
                    }
                    cue.Pos = pos;
                    if (pos >= last && !cue.Loop)
                    {
                        cues.RemoveAt(c);
                    }
                }
            }
        }

        // Your level in this buffer, before anything is added: opens at once and lets go over
        // about half a second, so a pause between words is still talking.
        private static float Listen(float[] buf)
        {
            double sum = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                sum += buf[i] * buf[i];
            }
            float rms = buf.Length > 0 ? (float)Math.Sqrt(sum / buf.Length) : 0f;
            float release = (float)Math.Exp(-(double)buf.Length / (streamRate * ReleaseSeconds));
            voiceEnvelope = Math.Max(rms, voiceEnvelope * release);
            return voiceEnvelope;
        }

        // The factor every clip is scaled by for this buffer, from your voice level in it.
        private static float DuckFor(bool voiceGoesOut)
        {
            if (!PluginConfig.DuckUnderVoice.Value)
            {
                return Levels.MaxGain;
            }
            duck = Levels.DuckStep(duck, Levels.DuckTarget(voiceGoesOut, PluginConfig.DuckRatio.Value, voiceEnvelope));
            return duck;
        }

        private sealed class FloatMixer : IProcessor<float>
        {
            public float[] Process(float[] buf)
            {
                Mix(buf);
                return buf;
            }

            public void Dispose()
            {
            }
        }

        private sealed class ShortMixer : IProcessor<short>
        {
            private float[] scratch = Array.Empty<float>();

            public short[] Process(short[] buf)
            {
                if (micOpen && !Playing)
                {
                    return buf;
                }
                if (scratch.Length != buf.Length)
                {
                    scratch = new float[buf.Length];
                }
                for (int i = 0; i < buf.Length; i++)
                {
                    scratch[i] = buf[i] / 32768f;
                }
                Mix(scratch);
                for (int i = 0; i < buf.Length; i++)
                {
                    buf[i] = (short)Mathf.RoundToInt(Mathf.Clamp(scratch[i], -1f, 1f) * 32767f);
                }
                return buf;
            }

            public void Dispose()
            {
            }
        }
    }
}
