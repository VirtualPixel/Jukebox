namespace Jukebox.Models
{
    internal enum Admit
    {
        Queue,
        MakeRoom,
        LocalOnly
    }

    // Who decides what leaves your mic while a clip is in the stream. The game shuts the
    // recorder's transmit gate whenever push to talk is up or you are muted, and Photon then
    // drops the whole buffer before the mixer ever sees it, so a clip holds the gate open for
    // as long as it sounds (and a moment after, for the frame still in the framer). The mic
    // under it stays shut unless the game wanted it open, and while a clip plays the speech
    // detector that would have kept your breathing out is off, so the mic only rides along
    // while you are actually talking.
    internal static class VoiceGate
    {
        // The recorder frames 40 ms at a time and the last partial frame of a clip waits for
        // the next buffer, so the gate stays open a little past the last sample.
        public const float Tail = 0.25f;

        public static bool Hold(bool sounding, float sinceSounding) => sounding || sinceSounding < Tail;

        public static bool Transmit(bool gameWants, bool hold) => gameWants || hold;

        // gameWants: the gate as the game set it (mic open). talking: your level in this buffer
        // is over the floor. sounding: a clip is in the stream, so the speech detector is off.
        public static bool KeepMic(bool gameWants, bool talking, bool sounding) => gameWants && (talking || !sounding);

        // The WebRTC speech detector on the recorder drops every frame it hears no voice in,
        // which is most of a jingle.
        public static bool DetectorOn(bool gameSetting, bool hold) => gameSetting && !hold;

        // How far through a clip one sample of the stream moves. Clips are held at clipRate and
        // the stream runs at whatever the microphone opened at.
        public static double Step(float pitch, int clipRate, int streamRate) =>
            streamRate > 0 ? pitch * (double)clipRate / streamRate : pitch;

        // What the cap on clips in the stream does with a new one: the oldest plain one-shot
        // makes room, a sticky clip always gets in, and anything else stays out of the stream
        // but still plays for you.
        public static Admit Room(int queued, int max, bool anyPlain, bool sticky) =>
            queued < max ? Admit.Queue : anyPlain ? Admit.MakeRoom : sticky ? Admit.Queue : Admit.LocalOnly;

        // Where a looped clip goes on from once it runs past its last sample. A step can be longer
        // than a very short loop, so once round is not always enough.
        public static double Wrap(double pos, int last) => pos % last;

        // Whether the voice the mixer holds is still the one going out. No voice chat (back in
        // singleplayer after a lobby) means it is gone, whatever the last recorder left behind.
        public static bool StillAttached(bool voiceChat, bool liveVoice) => voiceChat && liveVoice;
    }
}
