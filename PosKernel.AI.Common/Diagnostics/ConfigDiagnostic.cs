/*
Copyright 2025 Paul Moore Parks and contributors

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using PosKernel.Configuration;

namespace PosKernel.AI.Common.Diagnostics;

/// <summary>
/// Configuration diagnostic tool to verify .env file loading
/// </summary>
public class ConfigDiagnostic
{
    public static void DiagnoseConfiguration()
    {
        Console.WriteLine("=== POS Kernel Configuration Diagnostics ===");
        Console.WriteLine();
        
        // Check file paths
        Console.WriteLine("Configuration Paths:");
        Console.WriteLine($"Config Directory: {PosKernelConfiguration.ConfigDirectory}");
        Console.WriteLine($"Secrets File: {PosKernelConfiguration.SecretsFilePath}");
        Console.WriteLine($"Secrets File Exists: {File.Exists(PosKernelConfiguration.SecretsFilePath)}");
        Console.WriteLine();
        
        // Check if directory exists
        if (Directory.Exists(PosKernelConfiguration.ConfigDirectory))
        {
            Console.WriteLine("Files in config directory:");
            var files = Directory.GetFiles(PosKernelConfiguration.ConfigDirectory);
            foreach (var file in files)
            {
                Console.WriteLine($"  - {Path.GetFileName(file)}");
            }
        }
        else
        {
            Console.WriteLine("❌ Config directory does not exist!");
        }
        Console.WriteLine();
        
        // Try to read .env file directly
        if (File.Exists(PosKernelConfiguration.SecretsFilePath))
        {
            Console.WriteLine(".env file contents:");
            var lines = File.ReadAllLines(PosKernelConfiguration.SecretsFilePath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                {
                    Console.WriteLine($"  {trimmed}");
                }
            }
        }
        else
        {
            Console.WriteLine("❌ .env file not found!");
        }
        Console.WriteLine();
        
        // Test configuration loading
        Console.WriteLine("Loading configuration...");
        try
        {
            var config = PosKernelConfiguration.Initialize();
            Console.WriteLine("✅ Configuration loaded successfully");
            
            Console.WriteLine();
            Console.WriteLine("Configuration Sources:");
            foreach (var source in config.ConfigSources)
            {
                Console.WriteLine($"  - {source}");
            }
            Console.WriteLine();
            
            // Test specific values
            Console.WriteLine("Environment Variable Tests:");
            TestConfigValue(config, "STORE_AI_PROVIDER");
            TestConfigValue(config, "STORE_AI_MODEL");
            TestConfigValue(config, "STORE_AI_BASE_URL");
            TestConfigValue(config, "OPENAI_API_KEY");
            
            Console.WriteLine();
            Console.WriteLine("System Environment Variables:");
            TestSystemEnvVar("STORE_AI_PROVIDER");
            TestSystemEnvVar("STORE_AI_MODEL");
            TestSystemEnvVar("STORE_AI_BASE_URL");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Configuration loading failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    private static void TestConfigValue(PosKernelConfiguration config, string key)
    {
        var value = config.GetValue<string>(key);
        var status = string.IsNullOrEmpty(value) ? "❌ NULL/EMPTY" : "✅ FOUND";
        var displayValue = string.IsNullOrEmpty(value) ? "[null]" : 
            (key.Contains("KEY") ? $"[{value.Length} chars]" : value);
        Console.WriteLine($"  {key}: {status} = {displayValue}");
    }
    
    private static void TestSystemEnvVar(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        var status = string.IsNullOrEmpty(value) ? "❌ NULL/EMPTY" : "✅ FOUND";
        var displayValue = string.IsNullOrEmpty(value) ? "[null]" : value;
        Console.WriteLine($"  System.{key}: {status} = {displayValue}");
    }
}
