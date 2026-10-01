using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Jukebox.Services
{
    // The direct path. In a room where everyone has Jukebox a clip skips the voice stream and
    // is sent as one word instead; on the far side it plays at the sender's Semibot, clean and
    // at full quality, and the microphone stays a microphone.
    //
    // The word is only ever sent to seats that have said they have the mod, because PUN logs an
    // error on a client that has no method for it. It names its sender and is only acted on
    // when it came from that seat, so nobody can put a sound in someone else's mouth.
    public class Net : MonoBehaviourPunCallbacks
    {
        internal static Net? Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Room.Announce();
            HostSwitch.GreetAll();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void OnJoinedRoom()
        {
            Room.Announce();
            HostSwitch.Reset();
        }

        public override void OnLeftRoom() => HostSwitch.Left();

        public override void OnPlayerEnteredRoom(Player newPlayer) => Room.Forget();

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            Room.Forget();
            HostSwitch.Forget(otherPlayer);
        }

        public override void OnPlayerPropertiesUpdate(Player target, ExitGames.Client.Photon.Hashtable changedProps)
        {
            Room.Forget();
            HostSwitch.Greet(target);
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            Room.Forget();
            HostSwitch.Reset();
        }

        private static int Me => PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;

        // With everyone on the mod the room broadcast is the cheapest way to reach them;
        // otherwise each of them is named, which in a room that is mostly plain is cheaper than
        // a broadcast as well as quieter.
        public static void Sound(string name, float gain, float pitch)
        {
            if (!Instance || !PhotonNetwork.InRoom)
            {
                return;
            }
            Player[] peers = Room.Peers();
            if (peers.Length == 0)
            {
                return;
            }
            if (Room.EveryoneHasJukebox)
            {
                Instance!.photonView.RPC(nameof(JukeboxSound), RpcTarget.Others, Me, name, gain, pitch);
                return;
            }
            for (int i = 0; i < peers.Length; i++)
            {
                Instance!.photonView.RPC(nameof(JukeboxSound), peers[i], Me, name, gain, pitch);
            }
        }

        // The host's switch, to one seat that has Jukebox.
        public static void HostWord(Player who, bool on)
        {
            if (Instance && PhotonNetwork.InRoom)
            {
                Instance!.photonView.RPC(nameof(JukeboxHost), who, on);
            }
        }

        [PunRPC]
        public void JukeboxHost(bool on, PhotonMessageInfo info = default)
        {
            if (SemiFunc.MasterOnlyRPC(info))
            {
                HostSwitch.Heard(on);
            }
        }

        [PunRPC]
        public void JukeboxSound(int owner, string name, float gain, float pitch, PhotonMessageInfo info = default)
        {
            if (info.Sender != null && info.Sender.ActorNumber == owner)
            {
                Mixer.PlayFrom(owner, name, gain, pitch);
            }
        }
    }
}
