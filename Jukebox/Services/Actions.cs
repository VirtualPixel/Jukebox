using System;
using System.Collections.Generic;
using System.IO;
using Jukebox.Configuration;
using Jukebox.Models;

namespace Jukebox.Services
{
    // The moments of the game, played as sounds. A patch on the real trigger calls Fire with
    // the action's name; the look you have on, cues.cfg and the game decide what plays (see
    // Resolver). The clip goes out exactly like any other Jukebox clip: into your voice for a
    // plain room, straight to friends who have the mod, and your own copy for you.
    internal static class Actions
    {
        // The same action inside this window is one moment, not two (a double call from the
        // game, or a crouch that bounced).
        private const float Debounce = 0.15f;

        private static readonly Dictionary<string, float> last = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> lastClip = new Dictionary<string, string>(StringComparer.Ordinal);

        public static string? Look => Wardrobe.Api.CurrentLook;

        // Whether an action has a sound of yours (or is set to off) right now, which is when
        // the game's own sound for it is silenced.
        public static bool Mapped(string action) => Gate.Active && SoundSets.Resolve(action, Look).Source != CueSource.Game;

        // shared: a moment the whole room sees at once (a level starting), played by one friend
        // with the mod rather than by everyone. sticky: a death, nothing pushes it out.
        public static bool Fire(string action, bool shared = false, bool sticky = false)
        {
            if (!Gate.Active)
            {
                return false;
            }
            SoundPick pick = SoundSets.Resolve(action, Look);
            if (pick.Clips.Count == 0)
            {
                return pick.Source == CueSource.Off;
            }
            Play(action, pick.Clips, pick.Source + (Look != null ? ", " + Look : ""), shared, sticky);
            return true;
        }

        // The player's own file for a moment a mod voices itself (Smash & Grab spending a life):
        // no pack, no cues.cfg, no game sound. False when there is no such file.
        public static bool FireUserSound(string look, string action)
        {
            IReadOnlyList<string> clips = SoundSets.UserClips(look, action);
            if (clips.Count == 0 || !Gate.Active)
            {
                return false;
            }
            Play(action, clips, "your file, " + look, false, action == "death");
            return true;
        }

        private static void Play(string action, IReadOnlyList<string> clips, string from, bool shared, bool sticky)
        {
            float now = UnityEngine.Time.unscaledTime;
            if (last.TryGetValue(action, out float at) && now - at < Debounce)
            {
                return;
            }
            last[action] = now;
            string clip = Choose(action, clips);
            Jukebox.LogInfo("action: " + action + " -> " + clip + " (" + from + ")");
            Mixer.Play(clip, 1f, 1f, false, shared, false, sticky);
        }

        // The first of two actions that has a sound, else the second: a valuable's first grab
        // plays valuable_grab when you gave it one, and grab when you did not.
        public static void FireEither(string first, string second)
        {
            if (Mapped(first))
            {
                Fire(first);
            }
            else
            {
                Fire(second);
            }
        }

        private static string Choose(string action, IReadOnlyList<string> clips)
        {
            lastClip.TryGetValue(action, out string previous);
            string clip = Variant.Pick(clips, previous, n => UnityEngine.Random.Range(0, n));
            lastClip[action] = clip;
            return clip;
        }

        // ACTIONS.txt next to cues.cfg, written when it is missing or the list has changed.
        public static void WriteReadme()
        {
            string path = Path.Combine(Path.GetDirectoryName(Soundboard.CuesFile) ?? ".", "ACTIONS.txt");
            string text = ActionList.Readme(SoundSets.Folder, Soundboard.CuesFile);
            try
            {
                if (!File.Exists(path) || File.ReadAllText(path) != text)
                {
                    File.WriteAllText(path, text);
                }
            }
            catch (IOException e)
            {
                Jukebox.LogAlways("actions: could not write " + path + ": " + e.Message);
            }
        }
    }
}
