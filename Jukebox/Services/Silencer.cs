using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jukebox.Services
{
    // The game's own sound for an action you have given a sound of your own (or set to off),
    // quietened on your machine. Only the one Sound object that belongs to the moment, and
    // only while the method that plays it runs: a patch holds it on the way in and lets go on
    // the way out. Played but at volume 0, never skipped, because the game reads the source it
    // gets back.
    //
    // A hold also answers whether the sound actually played, for the moments where the game
    // decides at the last line (a landing on a crate makes no sound, so it is no landing).
    //
    // A few sounds are their own trigger (the map, the flashlight) or come long after the
    // moment (the death sting): those are known by identity every time any Sound plays.
    internal static class Silencer
    {
        private struct Held
        {
            public Sound Sound;
            public string Action;
        }

        private static readonly List<Held> held = new List<Held>();
        private static bool heard;

        public static void Hold(string action, Sound? a, Sound? b = null)
        {
            if (a != null)
            {
                held.Add(new Held { Sound = a, Action = action });
            }
            if (b != null)
            {
                held.Add(new Held { Sound = b, Action = action });
            }
        }

        // Lets go of every hold for the action. True when one of its sounds played meanwhile.
        public static bool Release(string action)
        {
            held.RemoveAll(h => h.Action == action);
            bool played = heard;
            heard = false;
            return played;
        }

        // Called from the Sound.Play postfix with the source the game got back.
        public static void Played(Sound sound, AudioSource? source)
        {
            if (!Gate.Active)
            {
                return;
            }
            for (int i = 0; i < held.Count; i++)
            {
                if (ReferenceEquals(held[i].Sound, sound))
                {
                    heard = true;
                    Quiet(held[i].Action, source);
                    return;
                }
            }
            string? action = Known(sound, out bool trigger);
            if (action == null)
            {
                return;
            }
            if (trigger)
            {
                Actions.Fire(action);
            }
            Quiet(action, source);
        }

        private static void Quiet(string action, AudioSource? source)
        {
            if (source && Actions.Mapped(action))
            {
                source!.volume = 0f;
            }
        }

        // Sounds on your own Semibot that are known on sight. trigger: the sound playing is the
        // moment itself.
        private static string? Known(Sound sound, out bool trigger)
        {
            trigger = false;
            PlayerAvatar me = PlayerAvatar.instance;
            if (!me)
            {
                return null;
            }
            if (me.mapToolController)
            {
                if (ReferenceEquals(sound, me.mapToolController.SoundStart))
                {
                    trigger = true;
                    return "map_open";
                }
                if (ReferenceEquals(sound, me.mapToolController.SoundStop))
                {
                    trigger = true;
                    return "map_close";
                }
            }
            if (me.flashlightController)
            {
                if (ReferenceEquals(sound, me.flashlightController.lightOnAudio))
                {
                    trigger = true;
                    return "flashlight_on";
                }
                if (ReferenceEquals(sound, me.flashlightController.lightOffAudio))
                {
                    trigger = true;
                    return "flashlight_off";
                }
            }
            PhysGrabber hands = PhysGrabber.instance;
            if (hands)
            {
                if (ReferenceEquals(sound, hands.startSound))
                {
                    return "grab";
                }
                if (ReferenceEquals(sound, hands.stopSound))
                {
                    return "let_go";
                }
            }
            if (ReferenceEquals(sound, me.deathSound) || (GameDirector.instance && ReferenceEquals(sound, GameDirector.instance.SoundDeath)))
            {
                return "death";
            }
            return null;
        }
    }
}
