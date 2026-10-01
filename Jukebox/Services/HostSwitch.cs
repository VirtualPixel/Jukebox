using System.Collections.Generic;
using Jukebox.Configuration;
using Jukebox.Models;
using Photon.Pun;
using Photon.Realtime;

namespace Jukebox.Services
{
    // The host's switch for the lobby. A host with AllowedInMyLobby off turns Jukebox off for
    // everyone in the room who has it: no clips, no direct path, no soundboard door, no wheel
    // takeover, the game's own sounds left alone. Everyone gets a line on the HUD when it flips.
    //
    // The word travels as an RPC on the relay rather than a room property, because a room
    // property does not say who wrote it and any client can write one. PUN stamps an RPC with
    // its sender, so only the current master client is listened to. It goes only to seats that
    // have said they have Jukebox (the jbx player property), once when they show up and again
    // whenever the host flips the switch. Until a word arrives, and whenever the host is
    // someone without Jukebox, it is allowed.
    internal static class HostSwitch
    {
        private static bool? hostSaid;
        private static bool allowed = true;
        private static readonly HashSet<int> told = new HashSet<int>();

        public static bool Allowed => allowed;

        private static bool InRoom => PhotonNetwork.InRoom && GameManager.instance && GameManager.instance.gameMode == 1;

        public static void Init()
        {
            PluginConfig.AllowedInMyLobby.SettingChanged += (_, _) =>
            {
                Evaluate();
                TellEveryone();
            };
        }

        // A word from the host, already checked to have come from the master client.
        public static void Heard(bool on)
        {
            hostSaid = on;
            Evaluate();
        }

        // A new room or a new host: the old host's word no longer counts.
        public static void Reset()
        {
            hostSaid = null;
            told.Clear();
            Evaluate();
            TellEveryone();
        }

        // Out of the room: back to allowed without a line, since nobody turned anything on.
        public static void Left()
        {
            hostSaid = null;
            told.Clear();
            allowed = true;
        }

        // The relay came up (a new scene): anyone with Jukebox not yet told hears it now.
        public static void GreetAll()
        {
            Evaluate();
            foreach (Player peer in Room.Peers())
            {
                Greet(peer);
            }
        }

        public static void Evaluate()
        {
            bool iAmHost = PhotonNetwork.IsMasterClient;
            bool now = HostRule.Allowed(InRoom, iAmHost, PluginConfig.AllowedInMyLobby.Value, hostSaid);
            if (now == allowed)
            {
                return;
            }
            string? line = HostRule.Line(allowed, now, iAmHost);
            allowed = now;
            if (!now)
            {
                Mixer.StopAll();
            }
            Jukebox.LogAlways("host switch: Jukebox is " + (now ? "on" : "off") + " for this lobby.");
            if (line != null)
            {
                Notice.Show(line);
            }
        }

        // The host tells every seat with Jukebox what the switch says.
        public static void TellEveryone()
        {
            if (!InRoom || !PhotonNetwork.IsMasterClient)
            {
                return;
            }
            foreach (Player peer in Room.Peers())
            {
                Tell(peer, true);
            }
        }

        // A seat that has just said it has Jukebox hears the switch once.
        public static void Greet(Player who)
        {
            if (InRoom && PhotonNetwork.IsMasterClient && !who.IsLocal && Room.HasJukebox(who) && !told.Contains(who.ActorNumber))
            {
                Tell(who, false);
            }
        }

        public static void Forget(Player who) => told.Remove(who.ActorNumber);

        private static void Tell(Player who, bool again)
        {
            if (!again && told.Contains(who.ActorNumber))
            {
                return;
            }
            told.Add(who.ActorNumber);
            Net.HostWord(who, PluginConfig.AllowedInMyLobby.Value);
        }
    }
}
