using ParksComputing.Xfer.Lang;

// Simple test to verify XferLang deserialization works
var simpleXfer = @"{
    services {
        rust {
            name ""Rust Service""
            enabled ~true
        }
    }
}";

try
{
    var config = XferConvert.Deserialize<TestConfig>(simpleXfer);
    Console.WriteLine($"Loaded {config?.Services?.Count ?? 0} services");

    if (config?.Services != null)
    {
        foreach (var service in config.Services)
        {
            Console.WriteLine($"  {service.Key}: {service.Value.Name} (enabled: {service.Value.Enabled})");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

public class TestConfig
{
    public Dictionary<string, TestService> Services { get; set; } = new();
}

public class TestService
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
