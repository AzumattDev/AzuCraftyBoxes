using System.IO;
using YamlDotNet.Serialization;

namespace AzuCraftyBoxes.Util.Functions;

public static class YamlUtils
{
    internal static void ReadYaml(string yamlInput)
    {
        IDeserializer deserializer = new DeserializerBuilder().Build();
        AzuCraftyBoxesPlugin.yamlData = deserializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>>(yamlInput);
        AzuCraftyBoxesPlugin.AzuCraftyBoxesLogger.LogDebug($"yamlData:\n{yamlInput}");
    }

    internal static void ParseGroups()
    {
        if (AzuCraftyBoxesPlugin.yamlData.TryGetValue("groups", out Dictionary<string, List<string>> groupData))
        {
            foreach (KeyValuePair<string, List<string>> group in groupData)
            {
                AzuCraftyBoxesPlugin.groups[group.Key] = new HashSet<string>(group.Value);
            }
        }
    }
    public static void WriteYaml(string filePath)
    {
        ISerializer serializer = new SerializerBuilder().Build();
        using StreamWriter output = new(filePath);
        serializer.Serialize(output, AzuCraftyBoxesPlugin.yamlData);

        // Serialize the data again to YAML format
        string serializedData = serializer.Serialize(AzuCraftyBoxesPlugin.yamlData);

        // Append the serialized YAML data to the file
        File.AppendAllText(filePath, serializedData);
    }
}