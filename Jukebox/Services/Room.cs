using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Jukebox.Services
{
    // Who else in the room has Jukebox. Each of us writes one number into our own Photon player
    // properties on the way in; a plain client writes nothing and is never written to, and a
    // late joiner is known the moment their properties land, with nobody having to ask. That
    // one number is the whole handshake, and it is what decides whether clips can skip the
    // voice stream and go straight to the others.
    internal static class Room
    {
        // What the messages look like. A build that writes a different number is treated as a
        // plain client, so two versions in one room fall back to the voice instead of trading
        // words neither can read.
        public const int Wire = 2;
        private const string Key = "jbx";

        private static Player[] peers = Array.Empty<Player>();
        private static readonly List<Player> scratch = new List<Player>();
        private static bool allModded;
        private static int peersFrame = -1;

        // Whose copy of a shared sound plays. A mod that knows more about the room than we do
        // (who is in costume, who is alive) answers this instead, for the clips it registered;
        // the plain answer covers everything else, so one mod's idea of the room never silences
        // another mod's sounds.
        public static Func<float, bool>? Covered { get; set; }
        private static readonly Dictionary<string, Func<float, bool>> covers = new Dictionary<string, Func<float, bool>>(StringComparer.OrdinalIgnoreCase);

        public static void CoverFor(string space, Func<float, bool>? answer)
        {
            string key = (space ?? "").Trim().Trim('/');
            if (answer == null)
            {
                covers.Remove(key);
            }
            else
            {
                covers[key] = answer;
            }
        }

        public static void Announce()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            {
                return;
            }
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { Key, Wire } });
            Forget();
            Jukebox.LogInfo("room: said hello with wire " + Wire + ".");
        }

        // The room changed under us: work the list out again on the next read.
        public static void Forget()
        {
            peersFrame = -1;
        }

        public static bool HasJukebox(Player? who)
        {
            return who != null && who.CustomProperties != null
                && who.CustomProperties.TryGetValue(Key, out object value) && value is int wire && wire == Wire;
        }

        // Worked out at most once a frame, which covers the bursts where a dozen clips go out
        // at once, and off the room's own player table rather than PhotonNetwork's list of the
        // others, which sorts and copies the whole room every time it is read.
        private static void Refresh()
        {
            if (peersFrame == Time.frameCount)
            {
                return;
            }
            peersFrame = Time.frameCount;
            allModded = false;
            Photon.Realtime.Room? room = PhotonNetwork.CurrentRoom;
            if (room == null || room.Players == null)
            {
                peers = Array.Empty<Player>();
                return;
            }
            scratch.Clear();
            foreach (KeyValuePair<int, Player> pair in room.Players)
            {
                Player p = pair.Value;
                if (p != null && !p.IsLocal && HasJukebox(p))
                {
                    scratch.Add(p);
                }
            }
            peers = scratch.Count == 0 ? Array.Empty<Player>() : scratch.ToArray();
            allModded = peers.Length > 0 && peers.Length == room.PlayerCount - 1;
        }

        public static Player[] Peers()
        {
            Refresh();
            return peers;
        }

        public static bool EveryoneHasJukebox
        {
            get
            {
                Refresh();
                return allModded;
            }
        }

        public static int Others
        {
            get
            {
                Refresh();
                return peers.Length;
            }
        }

        private static int Me => PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;

        public static bool HostHasMod => PhotonNetwork.InRoom && (PhotonNetwork.IsMasterClient || HasJukebox(PhotonNetwork.MasterClient));

        // A friend with the mod, in a lower seat and close enough that you would hear them:
        // their copy of a sound the whole room shares is the one that plays, so nobody hears it
        // twice. A mod that has its own idea of who counts answers this through Covered.
        public static bool NearbyLowerSeat(float range, string space = "")
        {
            if (space.Length > 0 && covers.TryGetValue(space, out Func<float, bool> own))
            {
                return own(range);
            }
            Func<float, bool>? asked = Covered;
            if (asked != null)
            {
                return asked(range);
            }
            PlayerController pc = PlayerController.instance;
            if (!pc || Others == 0)
            {
                return false;
            }
            int me = Me;
            foreach (PlayerAvatar other in SemiFunc.PlayerGetAll())
            {
                if (!other || other.isLocal || other.isDisabled || !other.photonView || other.photonView.Owner == null)
                {
                    continue;
                }
                if (other.photonView.Owner.ActorNumber >= me || !HasJukebox(other.photonView.Owner))
                {
                    continue;
                }
                if (Vector3.Distance(other.transform.position, pc.transform.position) < range)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
