using System.Collections.Generic;
using Newtonsoft.Json;

namespace SteamServerBuddy.Models
{
    public class GameSchema
    {
        [JsonProperty("appid")]
        public string AppId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("executables")]
        public List<string> Executables { get; set; }

        [JsonProperty("launch_args")]
        public List<string> LaunchArgs { get; set; }

        [JsonProperty("config_files")]
        public List<ConfigFileDefinition> ConfigFiles { get; set; }
    }

    public class ConfigFileDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; } // "json", "ini", etc.

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("fields")]
        public List<FieldDefinition> Fields { get; set; }
    }

    public class FieldDefinition
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } // "text", "number", "select", "toggle"

        [JsonProperty("category")]
        public string Category { get; set; } = "General";

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("default")]
        public object Default { get; set; }

        [JsonProperty("options")]
        public List<object> Options { get; set; }

        [JsonProperty("min")]
        public double? Min { get; set; }

        [JsonProperty("max")]
        public double? Max { get; set; }

        [JsonProperty("step")]
        public double? Step { get; set; }
    }

    public class OptionDefinition
    {
        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }
    }
}
