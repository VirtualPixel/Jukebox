using UnityEngine;

namespace Jukebox.Services
{
    // The one per-frame job: keep the mixer bolted to whatever voice object the recorder has
    // right now, watch cues.cfg and the sets folder for a save, read the hotkeys, play whatever
    // came in through the endpoint since the last frame, and put a waiting HUD line up once
    // there is a HUD.
    internal class JukeboxController : MonoBehaviour
    {
        private void Update()
        {
            VoiceMix.Tick();
            Soundboard.Tick();
            SoundSets.Tick();
            Endpoint.Tick();
            Notice.Tick();
        }

        private void OnDestroy()
        {
            Endpoint.Stop();
        }

        private void OnApplicationQuit()
        {
            Endpoint.Stop();
        }
    }
}
