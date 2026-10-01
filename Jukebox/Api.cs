using System;
using System.Collections.Generic;
using Jukebox.Models;
using Jukebox.Services;

namespace Jukebox
{
    // What another mod calls. Add a reference to Jukebox.dll, declare the dependency, register
    // your sounds once, and play them by name:
    //
    //   Jukebox.Api.Load(pluginFolder, "yourmod");
    //   Jukebox.Api.Play("coin");
    //   Jukebox.Api.PlayLoop("themes/overworld", 0.6f);
    //   Jukebox.Api.Stop("themes/overworld");
    //
    // Everything a plain client hears goes through your voice stream, so nobody else has to
    // install anything. In a room where everyone has Jukebox the same calls go out as one word
    // instead and play at your Semibot on the other side, at full quality.
    public static class Api
    {
        // The rate every clip is folded to on the way in, for anything that builds an AudioClip
        // out of Samples.
        public const int SampleRate = Library.Rate;

        // Your own clips: the folder in the game's save area that is always loaded, under the
        // name "me/<file>" as well as its plain name.
        public static string SoundboardFolder => Library.SoundboardFolder;

        // The file that points cues at your own clips.
        public static string CuesFile => Soundboard.CuesFile;

        // Whether anything is going out through the voice stream this instant. Worth holding
        // the microphone open for.
        public static bool Playing => Mixer.Playing;

        // Somewhere the room can see you and the quiet-in-menus rule is happy: the truck, a
        // level, the shop, the arena.
        public static bool OnStage => Mixer.OnStage;

        // Somewhere the room can see you, whatever state the game director is in.
        public static bool InPlace => Mixer.InPlace;

        // Whether clips are going straight to the others instead of through your voice.
        public static bool Direct => Mixer.Direct;

        // Whether everyone else in the room has Jukebox and has said so.
        public static bool EveryoneHasJukebox => Room.EveryoneHasJukebox;

        // Who else in the room has Jukebox, not counting you.
        public static int Others => Room.Others;

        // Whose copy of a shared sound plays. Jukebox's own answer is the nearest friend with
        // the mod in a lower seat; a mod that knows more about the room (who is in costume, who
        // is alive) sets this and gets asked instead. Set it back to null to hand it back.
        public static Func<float, bool>? SharedCoveredBy
        {
            get => Room.Covered;
            set => Room.Covered = value;
        }

        // The same answer, but only for the clips registered under one namespace: your idea of
        // the room decides your sounds and leaves everyone else's alone. Null hands it back.
        public static void CoverFor(string space, Func<float, bool>? answer) => Room.CoverFor(space, answer);

        // Register a folder or a .pak under a namespace of your own. A folder with a sounds
        // subfolder is read from there, and any .pak sitting in it is read too, so handing over
        // your whole plugin folder works whether you ship loose WAVs or a packed file.
        //
        // The namespace is a prefix, not a rename: a clip is filed under its own name and under
        // "<space>/<name>", so your names keep working and a name two mods both want is still
        // reachable as yours. Pass "" for no prefix. Returns false when there is nothing there.
        public static bool Load(string path, string space = "") => Library.Register(path, space);

        public static bool Has(string name) => Library.Has(name);

        // The decoded samples of a clip, 48 kHz mono, for anything that wants to play it
        // through the game's own audio instead. Do not write to the array.
        public static float[]? Samples(string name) => Library.Samples(name);

        // Seconds of a clip (the first entry of a comma list, any level trim taken off).
        public static float Seconds(string name) => Mixer.Seconds(name);

        // Every clip name that is loaded, sorted.
        public static string[] Clips() => Library.Names();

        // Play a clip. The name may be a comma separated list, whose entries take turns, and
        // any entry may carry a level trim: "coin@0.6".
        //
        // gain     what the clip is worth against everything else, 0 to 2.
        // pitch    a playback rate: 1.06 is six percent higher and shorter, 0.94 lower and longer.
        // loop     keeps going until Stop. Music.
        // shared   one the whole room gets from the same event: when a friend with the mod is
        //          close enough to hear and in a lower seat, theirs plays and yours stays quiet.
        // outLoud  a loop that is part of the play, not background: it goes out to the room
        //          whatever the music rules say.
        // sticky   a death or a game over: it plays over whatever else is going and nothing
        //          that starts after it pushes it out.
        public static void Play(string name, float gain = 1f, float pitch = 1f, bool loop = false,
            bool shared = false, bool outLoud = false, bool sticky = false)
        {
            Mixer.Play(name, gain, pitch, loop, shared, outLoud, sticky);
        }

        public static void PlayLoop(string name, float gain = 1f) => Mixer.Play(name, gain, 1f, true, true, false, false);

        // A loop that is part of the play, not background: the star theme. It goes out to the
        // room whatever the music rules say, through the voice, or as one pass to friends on
        // the mod when nothing goes through the voice.
        public static void PlayLoopOut(string name, float gain = 1f) => Mixer.Play(name, gain, 1f, true, false, true, false);

        // Drops every copy of a clip, looping or not, from the stream and from your own copy.
        public static void Stop(string name) => Mixer.Stop(name);

        public static void StopAll() => Mixer.StopAll();

        // A friend's sound, played at their Semibot (or their head, when they are dead) through
        // the same mixer their voice comes through. This is what arrives on the direct path;
        // call it yourself only if you carry the word some other way.
        public static void PlayFrom(int actor, string name, float gain, float pitch) => Mixer.PlayFrom(actor, name, gain, pitch);

        // Send a clip to everyone in the room who has Jukebox, to play at your Semibot on their
        // side. Play does this for you on the direct path; this is the bare word.
        public static void SendSound(string name, float gain, float pitch) => Net.Sound(name, gain, pitch);

        // Whether Jukebox is doing anything right now: the player's own On switch and the
        // host's switch for the lobby. A mod that plays its own sounds around Jukebox can
        // stand down on the same word.
        public static bool Active => Gate.Active;

        // The folder of per-look sound sets: <SetsFolder>/<look name>/<action>.wav.
        public static string SetsFolder => SoundSets.Folder;

        // Every action name, in the order ACTIONS.txt lists them.
        public static string[] Actions()
        {
            string[] names = new string[ActionList.All.Count];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = ActionList.All[i].Name;
            }
            return names;
        }

        // Whether a Wardrobe look has a sound set, yours or a pack's that is switched on.
        public static bool HasSoundSet(string look) => SoundSets.HasSet(look);

        // A sound pack: sets your mod ships, one folder per look, in the same shape as the
        // player's own sets folder:
        //
        //   <rootFolder>/<look>/<file>.wav        (or a sounds subfolder, or a .pak, as Load takes)
        //
        // aliases     your file names to Jukebox action names, for a pack whose files are named
        //             its own way: { "win", "level_won" }, { "attack", "melee_swing" }. A name is
        //             looked up whole, then with its variant number off (win1 -> win), so one
        //             entry covers win1, win2 and win12. A file with no alias whose name is an
        //             action is taken as it is; anything else is ignored.
        // folderLooks your folder names to Wardrobe look names, where they differ:
        //             { "chunli", "Chun-Li" }. Other folders match a look by name, case and the
        //             characters a folder cannot hold ignored.
        //
        // playsOwnActions false is for a mod that voices these moments itself, with its own timing:
        // the pack then only says which looks have a voice (HasSoundSet and the wheel), and Jukebox
        // never plays its files on an action. Check HasUserSound before your own line so a file
        // the player dropped into their sets folder is heard instead of yours, not as well.
        //
        // The player's own sets folder wins over a pack for the same look and action. Clips are
        // decoded once even if you also Load the same folder. Call it again with the same GUID to
        // replace your pack. Returns false when there is nothing at rootFolder.
        public static bool RegisterSoundPack(string pluginGuid, string rootFolder,
            IDictionary<string, string>? aliases = null, IDictionary<string, string>? folderLooks = null, bool playsOwnActions = true)
        {
            return SoundSets.RegisterPack(pluginGuid, rootFolder, aliases, folderLooks, playsOwnActions);
        }

        // Every look your pack stands for, for the wheel: a look named here counts as having a sound
        // set even when its folder is thin or missing, say a character who never speaks. Names are
        // your own, as in folderLooks. Call it once your pack is registered (before works too); a
        // later call replaces the list. The pack's switch still holds: an off pack declares nothing.
        public static void DeclareLooks(string pluginGuid, IEnumerable<string> looks) => SoundSets.DeclareLooks(pluginGuid, looks);

        // Switches a registered pack off and on, say when your mod is turned off for the lobby. An
        // off pack is no sound set for the wheel and plays nothing. It holds for a pack registered
        // later under the same GUID too. Packs start on.
        public static void SetPackEnabled(string pluginGuid, bool enabled) => SoundSets.SetPackEnabled(pluginGuid, enabled);

        // Whether the player's own sets folder (SetsFolder/<look>/) has a file for this action. A
        // mod that voices a moment itself stays quiet when this is true: Jukebox plays the file.
        public static bool HasUserSound(string look, string action) => SoundSets.HasUserSound(look, action);

        // Plays the player's own file for this look and action (SetsFolder/<look>/<action>.wav),
        // the same way Jukebox plays it on the game's own moment. For a mod that voices a moment
        // Jukebox never sees, like a death that is not the game's. Returns false when there is no
        // such file or Jukebox is off, so the mod can play its own line instead.
        public static bool PlayUserSound(string look, string action) => Services.Actions.FireUserSound(look, action);

        // Hand the mixer a Photon Voice local voice object. Jukebox keeps its own watch on the
        // recorder and reads the mic's rate off it, so you never need this for the game's own
        // voice. Handing the same one over twice is fine.
        public static bool Attach(object voice) => Mixer.Attach(voice, 0, 1);
    }
}
