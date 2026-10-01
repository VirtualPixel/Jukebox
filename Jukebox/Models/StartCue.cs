namespace Jukebox.Models
{
    // level_start, held from the loading screen going away until the game director is in Main,
    // which is when anything is allowed to play.
    internal sealed class StartCue
    {
        private bool armed;

        public void Arm() => armed = true;

        public void Clear() => armed = false;

        public bool Due(bool main)
        {
            if (!armed || !main)
            {
                return false;
            }
            armed = false;
            return true;
        }
    }
}
