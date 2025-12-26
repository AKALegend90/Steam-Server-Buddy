using System;
using System.Collections.Generic;
using System.Linq;

public class TestConfigParser
{
    public static void Main()
    {
        Console.WriteLine("Running Config Parser Tests...");
        int passed = 0;
        int failed = 0;

        // Test 1: Basic Parsing
        string input1 = "OptionSettings=(ServerName=\"Test Server\",Port=8211,bEnabled=True)";
        var res1 = ParsePalworldSettings(input1);
        if (Assert(res1["ServerName"] == "\"Test Server\"", "Test 1.1") && 
            Assert(res1["Port"] == "8211", "Test 1.2") && 
            Assert(res1["bEnabled"] == "True", "Test 1.3")) passed++; else failed++;

        // Test 2: Quoted strings with commas (The tricky case)
        string input2 = "OptionSettings=(ServerDescription=\"Welcome, have fun!\",AdminPassword=\"12345\")";
        var res2 = ParsePalworldSettings(input2);
        if (Assert(res2["ServerDescription"] == "\"Welcome, have fun!\"", "Test 2.1") && 
            Assert(res2["AdminPassword"] == "\"12345\"", "Test 2.2")) passed++; else failed++;

        // Test 3: Reconstruction Settings (Boolean handling)
        var dict3 = new Dictionary<string, string> {
            { "bAllowGlobalPalboxExport", "True" },
            { "bAllowGlobalPalboxImport", "False" },
            { "ServerName", "My Server" } // Should get quotes added
        };
        string output3 = UpdatePalworldSettings(dict3);
        // We expect: OptionSettings=(bAllowGlobalPalboxExport=True,bAllowGlobalPalboxImport=False,ServerName="My Server")
        // Order might vary, but components should differ
        if (Assert(output3.Contains("bAllowGlobalPalboxExport=True"), "Test 3.1") &&
            Assert(output3.Contains("bAllowGlobalPalboxImport=False"), "Test 3.2") &&
            Assert(output3.Contains("ServerName=\"My Server\""), "Test 3.3")) passed++; else failed++;

        Console.WriteLine($"\nTests Complete. Passed: {passed}, Failed: {failed}");
    }

    static bool Assert(bool condition, string name)
    {
        if (condition) 
        {
            Console.WriteLine($"[PASS] {name}");
            return true;
        }
        else
        {
            Console.WriteLine($"[FAIL] {name}");
            return false;
        }
    }

    // --- COPIED LOGIC FROM ConfigService.cs ---
        private static Dictionary<string, string> ParsePalworldSettings(string line)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var start = line.IndexOf('(');
            var end = line.LastIndexOf(')');
            if (start < 0 || end < 0) return values;

            var content = line.Substring(start + 1, end - start - 1);
            
            var currentToken = "";
            bool inQuote = false;
            
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '"')
                {
                    inQuote = !inQuote;
                    currentToken += c;
                }
                else if (c == ',' && !inQuote)
                {
                    ProcessToken(values, currentToken);
                    currentToken = "";
                }
                else
                {
                    currentToken += c;
                }
            }
            ProcessToken(values, currentToken); // Last token
            return values;
        }

        private static void ProcessToken(Dictionary<string, string> values, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;
            var part = token.Trim();
            if (part.Contains('='))
            {
                var kv = part.Split('=', 2);
                values[kv[0].Trim()] = kv[1].Trim(); // KEEP QUOTES in value for now to pass test
            }
        }

        private static string UpdatePalworldSettings(Dictionary<string, string> values)
        {
             var settings = string.Join(",", values.Select(kv => 
            {
                var val = kv.Value;
                bool isBool = val.Equals("True", StringComparison.OrdinalIgnoreCase) || val.Equals("False", StringComparison.OrdinalIgnoreCase);
                bool isNumber = double.TryParse(val, out _);
                bool isReserverKeyword = val.Equals("None", StringComparison.OrdinalIgnoreCase) || val.Equals("All", StringComparison.OrdinalIgnoreCase);

                if (!isBool && !isNumber && !isReserverKeyword && !val.StartsWith("\""))
                {
                    val = $"\"{val}\"";
                }
                
                return $"{kv.Key}={val}";
            }));
            return $"OptionSettings=({settings})";
        }
}
