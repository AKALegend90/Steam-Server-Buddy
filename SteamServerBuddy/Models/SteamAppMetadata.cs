using System.Collections.Generic;

namespace SteamServerBuddy.Models
{
    public class SteamAppMetadata
    {
        public string AppId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string HeaderImageUrl { get; set; } = "";
        public string CapsuleImageUrl { get; set; } = "";
        public string SteamDbUrl { get; set; } = "";
        public string SteamStoreUrl { get; set; } = "";
        public List<string> Tags { get; set; } = new();
    }
}
