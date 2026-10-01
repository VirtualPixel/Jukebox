# Jukebox

*Custom sounds for R.E.P.O. Give your Semibot its own jump, hurt, death, grab, whatever you want, and a different set for every Wardrobe look. It all goes out through your voice, so the whole lobby hears it even if they have nothing installed.*

> **A note from Vippy**
>
> I've been sitting on this one for a while. It started as the sound engine inside a much bigger mod of mine and got good enough that it deserved to be its own thing. Test builds and fixes land in [Vippy's Discord](https://discord.gg/kKqhck2NrP) before they hit Thunderstore, so come hang out and tell me what you made your Semibot sound like.

## How it works

Make a folder named after a Wardrobe look and drop in WAV files named after the thing they should play on:

```
Jukebox/sets/Mario/jump.wav
Jukebox/sets/Mario/jump2.wav      more of the same, one gets picked at random
Jukebox/sets/Mario/hurt.wav
Jukebox/sets/Mario/death.wav
Jukebox/sets/Luigi/jump.wav
```

Put on Mario and your jumps are his. Swap to Luigi and they're his. That's it, no config.

The `Jukebox` folder lives in the game's save folder (on Windows `%USERPROFILE%/AppData/LocalLow/semiwork/Repo/Jukebox`), not in your mod profile, so exporting a profile never walks off with your clips. It gets made on first launch.

`ACTIONS.txt` sits right next to it and lists every action by name with what it means. There are about 50: jumping, landing, hurt, death, revive, grabbing, throwing, doors, guns, melee, grenades, the map, your flashlight, extractions, the shop, the taxman, and a pile more. A number on the end of a file is just another take, so `jump.wav`, `jump2.wav` and `jump17.wav` all play on a jump.

When you give an action a sound, the game's own sound for it goes quiet on your end. Anything you didn't touch stays vanilla.

## One sound for every look

Don't care about looks and just want the same boing on every jump? `cues.cfg` in the same folder:

```
jump  = boing
hurt  = ow1, ow2, ow3
death = wilhelm@0.6
land  = off
```

Files come from `Jukebox/sounds/`. Several files means a random one each time, `@0.6` turns one down, and `off` kills the action completely (the game's sound too). A look's own folder still wins over a line here. Both the file and the folders get re-read a couple seconds after you save, so you can sit in the truck with a text editor open and hear each change on the next jump.

## Soundboard and stream deck

- **Hotkeys**: `Hotkeys = F1=airhorn, F2=wilhelm` in the config plays a clip on a key. They stay quiet while you're typing in chat.
- **The soundboard door**: turn `Endpoint` on and Jukebox listens on `127.0.0.1:8793`, this PC only. Point a stream deck, Touch Portal, a script, anything that can hit a URL at it:

```
GET  http://127.0.0.1:8793/play?name=airhorn
POST http://127.0.0.1:8793/play    {"name":"airhorn","gain":1,"pitch":1}
GET  http://127.0.0.1:8793/clips
```

`/clips` lists every sound that's loaded. Nothing outside your machine can reach it.

## Why nobody else needs it

Every clip is mixed into your own voice chat on the way out, on top of your mic. Friends without the mod just hear it coming out of your Semibot like you said it, positional and everything. Your voice stays on top too: clips duck under you while you talk so you can still call out the Huntsman over your own theme song. Push to talk or muted, the clips still go out and your mic stays shut.

If everyone in the lobby has Jukebox, clips skip the voice and go over the network instead, so they play clean at full quality. Your own files still go through the voice though, since nobody else has them.

## Hosts

Don't want it in your lobby? Install it and set `AllowedInMyLobby = false`. Jukebox shuts off for everyone in the lobby who has it, and they get a line on the HUD saying the host turned it off (and another when you turn it back on). Works mid-lobby. And if the host doesn't have Jukebox at all, you're free to use it.

## The wheel

Jukebox takes over the Wardrobe wheel so it only shows looks that have sounds, your own folders or ones a mod ships. No sets anywhere yet means it shows everything, so a fresh install never gives you an empty wheel. `WheelOnlyLooksWithSounds = false` puts it back to normal.

## Settings

The plain stuff is under `General`, the HTTP door under `Soundboard door`, the rest under `Advanced`.

| Setting | Default | Description |
|---|---|---|
| On | `true` | Off plays nothing and leaves the game's sounds alone |
| Volume | `1` | Everything Jukebox plays gets scaled by this |
| AllowedInMyLobby | `true` | When you host, off turns Jukebox off for the whole lobby |
| WheelOnlyLooksWithSounds | `true` | The Wardrobe wheel only shows looks with a sound set |
| Endpoint | `false` | The local soundboard door for stream decks and scripts |
| Port | `8793` | Where the door listens |
| DirectSound | `true` | Send clips over the network instead of the voice when the whole lobby has Jukebox. Off and everything always goes through your voice |
| MusicThroughVoice | `false` | Let looping music out through your mic to players without the mod. Nobody asked for your song, so it's opt in |
| QuietInMenus | `true` | Nothing plays until you're somewhere the lobby can see you |
| DuckUnderVoice / DuckRatio | `true` / `0.5` | Hold clips under your voice while you talk, and by how much |
| HearOwnSounds / OwnCopyVolume | `true` / `0.85` | The game mutes your own voice for you, so without this you'd never hear your own clips |
| Hotkeys | empty | `F1=airhorn, F2=wilhelm` |
| ReloadKey | `None` | Re-reads everything right now instead of waiting a couple seconds |
| SoundboardVolume | `1` | Level for hotkeys and the door |
| Logging | `false` | A log line for every clip, only for chasing a problem |
| HearWhatGoesOut | `false` | Hear your voice chat exactly the way the lobby gets it, and log every clip sent into it |

## Troubleshooting

Not sure your clips are getting out? Host a lobby, turn on `HearWhatGoesOut` and play something: what you hear is your voice stream the way everyone else gets it, and the log gets a `to voice` line per clip. Needs a lobby, singleplayer has no voice chat.

## For modders

Jukebox has an API so other mods can be heard by the whole lobby without anyone installing anything. Reference `Jukebox.dll`, hard depend on `Vippy.Jukebox`, then:

```csharp
Jukebox.Api.Load(pluginFolder, "yourmod");
Jukebox.Api.Play("coin");
Jukebox.Api.PlayLoop("themes/overworld", 0.6f);
Jukebox.Api.Stop("themes/overworld");
```

You can also ship whole sound packs for looks with `RegisterSoundPack`, and a mod that voices its own moments can ask `HasUserSound` and `PlayUserSound` so a player's own file always wins. Every call has a comment on it in [Api.cs](https://github.com/VirtualPixel/Jukebox/blob/main/Jukebox/Api.cs).

## Compatibility

- Needs [Wardrobe](https://thunderstore.io/c/repo/p/Vippy/Wardrobe/). Your sound set follows the Wardrobe look you're wearing and the wheel is Wardrobe's, so there's nothing to hang the sets on without it.
- Client-side. Nobody else has to install anything, host included.
- Plays nice with Smash & Grab, which uses Jukebox for all its character voices.
- Source is on [GitHub](https://github.com/VirtualPixel/Jukebox).

## Come hang out

I'm Vippy. I make R.E.P.O. mods and I read every bug report.

| | |
|---|---|
| **[Vippy's Discord](https://discord.gg/kKqhck2NrP)** | Test builds land here before Thunderstore, you get a say in what comes next, and it's the fastest way to get a bug fixed. Come show off your sound sets. |
| **[GitHub Issues](https://github.com/VirtualPixel/Jukebox/issues)** | Bug reports and ideas that deserve a paper trail. |
| **[R.E.P.O. Modding Server](https://discord.gg/9fDzZ9sk95)** | The whole modding scene, not just me. |
| **[More of my mods](https://thunderstore.io/c/repo/p/Vippy/)** | Everything else I've made. If your crew keeps fighting over upgrades, [SharedUpgradesPlus](https://thunderstore.io/c/repo/p/Vippy/SharedUpgradesPlus/) fixes that. |

## Keep the mods coming

Everything I make is free and stays free. Two ways to help if you feel like it, neither one expected:

- **[Ko-fi](https://ko-fi.com/vippydev)**: buy me a coffee and your name goes on the supporters list in my Discord. Every coffee buys another evening on the next update.
- **[BisectHosting](https://bisecthosting.com/vippy)**: hosting a server for Minecraft or anything else your crew plays? Code `vippy` takes 25% off, and I get a cut at no cost to you. It's where my own servers live.

[![25% off BisectHosting servers with code vippy](https://www.bisecthosting.com/partners/custom-banners/71eecea6-f5bb-437d-ac56-f6fee4266193.png)](https://bisecthosting.com/vippy)
