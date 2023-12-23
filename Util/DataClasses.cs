using System.Collections.Generic;

namespace AzuCraftyBoxes.Util;

public class Group
{
    public string Name { get; set; }
    public List<string> Items { get; set; }

    public Group(string name, List<string> items)
    {
        Name = name;
        Items = items;
    }
    
    public static Dictionary<string, Group> PredefinedGroups = new Dictionary<string, Group>
    {
        {
            "Swords", new Group("Swords", new List<string>
            {
                "SwordBlackmetal",
                "SwordBronze",
                // Add other sword item prefab names
            })
        },
        {
            "Armor", new Group("Armor", new List<string>
            {
                "ArmorBronzeChest",
                "ArmorBronzeLegs",
                // Add other armor item prefab names
            })
        },
        // Add other groups (e.g., Food, Arrows, Ores, Scraps, Tier 2 Items, Bows)
    };

}