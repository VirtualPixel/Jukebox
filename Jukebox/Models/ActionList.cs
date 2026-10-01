using System.Collections.Generic;
using System.Text;

namespace Jukebox.Models
{
    // Every in-game moment Jukebox has a name for. The name is the file name in a look's set
    // (jump.wav, jump2.wav) and the left side of a line in cues.cfg (jump = boing).
    internal static class ActionList
    {
        public readonly struct Entry
        {
            public readonly string Name;
            public readonly string Group;
            public readonly string Meaning;

            public Entry(string group, string name, string meaning)
            {
                Group = group;
                Name = name;
                Meaning = meaning;
            }
        }

        public static readonly IReadOnlyList<Entry> All = new[]
        {
            new Entry("Moving", "jump", "you jump (the double jump upgrade too)"),
            new Entry("Moving", "land", "you land from a jump or a drop"),
            new Entry("Moving", "land_hard", "you land from a fall high enough to hurt"),
            new Entry("Moving", "crouch", "you crouch down"),
            new Entry("Moving", "stand", "you stand back up from a crouch"),
            new Entry("Moving", "crawl", "you go from a crouch to a crawl"),
            new Entry("Moving", "slide", "you slide out of a sprint"),
            new Entry("Moving", "sprint", "you start sprinting"),
            new Entry("Moving", "tumble", "you go into a tumble"),
            new Entry("Moving", "tumble_end", "you come out of a tumble"),
            new Entry("Moving", "break_free", "you break out of someone's grab (multiplayer only)"),

            new Entry("Health", "hurt", "you take damage and live"),
            new Entry("Health", "heal", "you get health back"),
            new Entry("Health", "truck_heal", "you are healed on getting back to the truck"),
            new Entry("Health", "death", "you die"),
            new Entry("Health", "revive", "you are revived"),

            new Entry("Hands", "grab", "you grab something"),
            new Entry("Hands", "let_go", "you let go of something"),
            new Entry("Hands", "throw", "you let go of something moving fast (played instead of let_go)"),
            new Entry("Hands", "valuable_grab", "you grab a valuable for the first time this level (played instead of grab)"),
            new Entry("Hands", "valuable_found", "you spot a valuable nobody had found yet"),
            new Entry("Hands", "valuable_break", "a valuable you are holding loses value"),
            new Entry("Hands", "valuable_destroyed", "a valuable is broken to pieces, anywhere"),
            new Entry("Hands", "door_open", "you swing a door open"),
            new Entry("Hands", "door_close", "you shut a door"),
            new Entry("Hands", "door_slam", "you slam a door"),

            new Entry("Items", "item_use", "you turn an item on"),
            new Entry("Items", "gun", "a gun you are holding fires"),
            new Entry("Items", "melee_swing", "you swing a melee weapon"),
            new Entry("Items", "melee_hit", "your melee weapon hits something"),
            new Entry("Items", "grenade", "you pull a grenade"),
            new Entry("Items", "grenade_boom", "your grenade goes off"),
            new Entry("Items", "mine", "you arm a mine"),
            new Entry("Items", "drone", "you switch a drone on"),
            new Entry("Items", "upgrade", "you take an upgrade"),
            new Entry("Items", "map_open", "you pull out the map"),
            new Entry("Items", "map_close", "you put the map away"),
            new Entry("Items", "flashlight_on", "your flashlight comes on"),
            new Entry("Items", "flashlight_off", "your flashlight goes off"),

            new Entry("You", "emote", "you make an expression"),
            new Entry("You", "chat", "you send a chat message"),

            new Entry("The run", "level_start", "a level begins (the loading screen lifts)"),
            new Entry("The run", "extraction_open", "an extraction point is switched on"),
            new Entry("The run", "extracted", "an extraction point takes the haul"),
            new Entry("The run", "shop_buy", "the shop takes your order"),
            new Entry("The run", "level_won", "the last extraction is done"),
            new Entry("The run", "truck_leave", "the truck leaves the level"),
            new Entry("The run", "level_lost", "everyone is dead"),
            new Entry("The run", "enemy_dies", "an enemy dies"),
            new Entry("The run", "taxman", "a line from the taxman comes up on the truck screen"),
        };

        // ACTIONS.txt, the list next to the config.
        public static string Readme(string setsFolder, string cuesFile)
        {
            StringBuilder text = new StringBuilder();
            text.Append("Jukebox actions\n\n");
            text.Append("Every moment below has a name. Give it a sound one of two ways:\n\n");
            text.Append("  Per look: drop <action>.wav in a folder named after a Wardrobe look,\n");
            text.Append("    " + setsFolder + "\n");
            text.Append("    e.g. sets/Mario/jump.wav. Add jump2.wav, jump3.wav... for variety.\n\n");
            text.Append("  For every look: a line in " + cuesFile + "\n");
            text.Append("    jump = boing          (a file in the sounds folder)\n");
            text.Append("    hurt = ow1, ow2       (a random one each time)\n");
            text.Append("    land = off            (no sound at all, the game's own included)\n\n");
            text.Append("The look you are wearing wins, then cues.cfg, then the game's own sound.\n");
            text.Append("A moment with a sound of yours plays it instead of the game's.\n");
            text.Append("This file is rewritten when the list changes.\n");
            string group = "";
            foreach (Entry entry in All)
            {
                if (entry.Group != group)
                {
                    group = entry.Group;
                    text.Append("\n").Append(group).Append("\n");
                }
                text.Append("  ").Append(entry.Name.PadRight(20)).Append(entry.Meaning).Append("\n");
            }
            return text.ToString();
        }

        public static bool Known(string name)
        {
            foreach (Entry entry in All)
            {
                if (entry.Name == name)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
