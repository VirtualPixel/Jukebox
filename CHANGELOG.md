# Changelog

## 1.0.0

- first release! custom sounds for pretty much everything your semibot does, around 50 actions (jump, land, hurt, death, revive, grab, throw, doors, guns, melee, grenades, extraction, taxman and a bunch more), all listed in ACTIONS.txt
- per look sound sets: drop jump.wav, hurt.wav etc in Jukebox/sets/<look name>/ and whatever wardrobe look your wearing uses them. jump2.wav, jump3.wav for variety
- cues.cfg for one sound on every look (`jump = boing`), several files for a random pick, `@0.6` to turn one down, `land = off` to kill an action completly
- the games own sound for an action goes quiet on your end when you gave it one, everything else stays vanilla
- clips go out through your voice chat so poeple without the mod hear them too, and they duck under your voice while you talk
- clips still go out on push to talk or muted (just the clip, your mic stays shut), and HearWhatGoesOut lets you hear what the lobby actualy gets
- when the whole lobby has jukebox clips go over the network instead so they sound clean
- hotkeys for clips (not while typing in chat) and a local soundboard door on 127.0.0.1 for stream decks and scripts, off by default
- hosts can turn jukebox off for there lobby (AllowedInMyLobby), everyone gets a line on the hud. no mod on the host means your free to use it
- takes over the wardrobe wheel so it only shows looks that have sounds, one switch turns that off
- api for other mods: load and play sounds, ship sound packs for looks, and PlayUserSound so a players own file wins over a mods line
- a broken wav in the sounds folder gets skipped instead of hanging the game on load
- files and cues.cfg get re-read a couple seconds after you save so you can tweak stuff without restarting
