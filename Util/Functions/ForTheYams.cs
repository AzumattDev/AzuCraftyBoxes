using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace AzuCraftyBoxes.Util.Functions;

public static class YamlUtils
{
    internal static void ReadYaml(string yamlInput)
    {
        IDeserializer? deserializer = new DeserializerBuilder().Build();
        AzuCraftyBoxesPlugin.yamlData = deserializer.Deserialize<Dictionary<string, object>>(yamlInput);
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"yamlData:\n{yamlInput}");
    }

    internal static void ParseGroups()
    {
        // Check if the groups dictionary has been initialized
        if (AzuCraftyBoxesPlugin.groups == null)
            AzuCraftyBoxesPlugin.groups = new Dictionary<string, HashSet<string>>();

        if (AzuCraftyBoxesPlugin.yamlData.TryGetValue("groups", out object groupData))
        {
            Dictionary<object, object>? groupDict = groupData as Dictionary<object, object>;
            if (groupDict != null)
            {
                foreach (KeyValuePair<object, object> group in groupDict)
                {
                    string groupName = group.Key.ToString();
                    if (group.Value is List<object> prefabs)
                    {
                        HashSet<string> prefabNames = new HashSet<string>();
                        foreach (object? prefab in prefabs)
                        {
                            prefabNames.Add(prefab.ToString());
                        }

                        AzuCraftyBoxesPlugin.groups[groupName] = prefabNames;
                    }
                }
            }
        }
    }
    public static void WriteYaml(string filePath)
    {
        ISerializer? serializer = new SerializerBuilder().Build();
        using StreamWriter? output = new StreamWriter(filePath);
        serializer.Serialize(output, AzuCraftyBoxesPlugin.yamlData);

        // Serialize the data again to YAML format
        string serializedData = serializer.Serialize(AzuCraftyBoxesPlugin.yamlData);

        // Append the serialized YAML data to the file
        File.AppendAllText(filePath, serializedData);
    }
}