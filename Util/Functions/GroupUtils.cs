
namespace AzuCraftyBoxes.Util.Functions;

public class GroupUtils
{
    // Get a list of all excluded groups for a container
    public static List<string> GetExcludedGroups(string container)
    {
        if (AzuCraftyBoxesPlugin.yamlData.TryGetValue(container, out Dictionary<string, List<string>> containerData))
        {
            if (containerData.TryGetValue("exclude", out List<string> excludeList))
            {
                return excludeList.Where(excludeItem => AzuCraftyBoxesPlugin.groups.ContainsKey(excludeItem)).ToList();
            }
        }

        return new List<string>();
    }
    public static bool IsGroupDefined(string groupName)
    {
        if (AzuCraftyBoxesPlugin.yamlData == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError("yamlData is null. Make sure to call DeserializeYamlFile() before using IsGroupDefined.");
            return false;
        }

        bool groupInYaml = false;

        if (AzuCraftyBoxesPlugin.yamlData.TryGetValue("groups", out Dictionary<string, List<string>> groupsData))
        {
            groupInYaml = groupsData.ContainsKey(groupName);
        }

        // Check for the group in both yamlData and predefined groups
        return groupInYaml || AzuCraftyBoxesPlugin.groups.ContainsKey(groupName);
    }


    /*public static bool IsGroupDefined(string groupName)
    {
        if (AzuCraftyBoxesPlugin.yamlData == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError(
                "yamlData is null. Make sure to call DeserializeYamlFile() before using IsGroupDefined.");
            return false;
        }

        if (!AzuCraftyBoxesPlugin.yamlData.ContainsKey("groups"))
        {
            return false;
        }

        var groupsData = AzuCraftyBoxesPlugin.yamlData["groups"] as Dictionary<object, object>;
        if (groupsData == null)
        {
            AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogError(
                "Unable to cast groupsData to Dictionary<object, object>.");
            return false;
        }

        return groupsData.ContainsKey(groupName);
    }*/


// Check if a group exists in the container data
    public static bool GroupExists(string groupName)
    {
        return AzuCraftyBoxesPlugin.groups.ContainsKey(groupName);
    }

// Get a list of all groups in the container data
    public static List<string> GetAllGroups()
    {
        return AzuCraftyBoxesPlugin.groups.Keys.ToList();
    }

// Get a list of all items in a group
    public static List<string> GetItemsInGroup(string groupName)
    {
        if (AzuCraftyBoxesPlugin.groups.TryGetValue(groupName, out HashSet<string> groupPrefabs))
        {
            return groupPrefabs.ToList();
        }

        return new List<string>();
    }

    /*public static bool IsItemInGroup(string itemName, string groupName)
    {
        if (PredefinedGroups.ContainsKey(groupName))
        {
            return PredefinedGroups[groupName].Items.Contains(itemName);
        }

        return false;
    }*/
}