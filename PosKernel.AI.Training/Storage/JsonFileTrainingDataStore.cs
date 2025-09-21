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

using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PosKernel.AI.Training.Storage;

/// <summary>
/// JSON file-based training data storage with cross-platform support
/// </summary>
public class JsonFileTrainingDataStore : ITrainingDataStore
{
    private readonly string _dataDirectory;
    private readonly ILogger<JsonFileTrainingDataStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonFileTrainingDataStore(string dataDirectory, ILogger<JsonFileTrainingDataStore> logger)
    {
        _dataDirectory = Path.GetFullPath(dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory)));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // ARCHITECTURAL PRINCIPLE: Fail fast if data directory not accessible
        EnsureDataDirectoryExists();
    }

    public T? Load<T>(string key) where T : class
    {
        ValidateKey(key);

        var filePath = GetFilePath(key);

        if (!File.Exists(filePath))
        {
            _logger.LogDebug("Training data file not found: {FilePath}", filePath);
            return null;
        }

        try
        {
            _logger.LogDebug("Starting to read training data file: {FilePath}", filePath);
            
            // Check file accessibility before attempting to read
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                _logger.LogDebug("File disappeared between existence check and read: {FilePath}", filePath);
                return null;
            }
            
            _logger.LogDebug("File size: {Size} bytes, Last modified: {LastWrite}", 
                fileInfo.Length, fileInfo.LastWriteTime);
            
            // Validate file is not empty or corrupted
            if (fileInfo.Length == 0)
            {
                _logger.LogWarning("Training data file is empty, deleting: {FilePath}", filePath);
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Could not delete empty file: {FilePath}", filePath);
                }
                return null;
            }

            // Use synchronous file reading to avoid async hanging issues
            string json;
            try
            {
                _logger.LogDebug("About to read file synchronously: {FilePath}", filePath);
                json = File.ReadAllText(filePath);
                _logger.LogDebug("Successfully read {Size} characters from file", json.Length);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Access denied reading training data '{key}' from '{filePath}'. " +
                    $"Current user lacks read permissions for the file. " +
                    $"Check file permissions: {ex.Message}");
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: I/O error reading training data '{key}' from '{filePath}'. " +
                    $"File may be locked, corrupted, or inaccessible. " +
                    $"Check file status and permissions: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Unexpected error reading training data '{key}' from '{filePath}'. " +
                    $"System error: {ex.GetType().Name}: {ex.Message}");
            }
            
            // Validate JSON before deserializing
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Training data file contains only whitespace, deleting: {FilePath}", filePath);
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Could not delete whitespace-only file: {FilePath}", filePath);
                }
                return null;
            }

            var data = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            _logger.LogDebug("Successfully deserialized training data: {Key}, Type: {Type}", key, typeof(T).Name);
            return data;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to parse JSON in training data '{key}' from '{filePath}'. " +
                $"File contains invalid JSON and may be corrupted. " +
                $"Consider deleting corrupted file and recreating. JSON error: {ex.Message}");
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Unexpected error loading training data '{key}' from '{filePath}'. " +
                $"System error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public async Task SaveAsync<T>(string key, T data) where T : class
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(data);

        var filePath = GetFilePath(key);
        var tempFilePath = filePath + ".tmp";

        try
        {
            _logger.LogDebug("Starting to save training data: {Key} to {FilePath}", key, filePath);

            var json = JsonSerializer.Serialize(data, _jsonOptions);
            _logger.LogDebug("Serialized data to JSON: {Size} characters", json.Length);

            // ARCHITECTURAL PRINCIPLE: Atomic write operation - write to temp file first
            try
            {
                _logger.LogDebug("About to write temp file: {TempPath}", tempFilePath);
                File.WriteAllText(tempFilePath, json);
                _logger.LogDebug("Successfully wrote temp file: {TempPath}", tempFilePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                CleanupTempFile(tempFilePath);
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Access denied writing temp file '{tempFilePath}'. " +
                    $"Current user lacks write permissions: {ex.Message}");
            }
            catch (IOException ex)
            {
                CleanupTempFile(tempFilePath);
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: I/O error writing temp file '{tempFilePath}'. " +
                    $"Check disk space and permissions: {ex.Message}");
            }

            // Atomic replace (works on Windows, Linux, macOS)
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            File.Move(tempFilePath, filePath);
            _logger.LogDebug("Successfully moved temp file to final location: {FilePath}", filePath);

            _logger.LogInformation("Saved training data: {Key}, Size: {Size} bytes", key, json.Length);
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            // Clean up temp file on error
            CleanupTempFile(tempFilePath);

            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Unexpected error saving training data '{key}' to '{filePath}'. " +
                $"System error: {ex.GetType().Name}: {ex.Message}");
        }

        await Task.CompletedTask; // Keep async signature for interface compliance
    }

    public async Task DeleteAsync(string key)
    {
        ValidateKey(key);

        var filePath = GetFilePath(key);

        if (!File.Exists(filePath))
        {
            _logger.LogDebug("Training data file not found for deletion: {FilePath}", filePath);
            return;
        }

        try
        {
            File.Delete(filePath);
            _logger.LogDebug("Deleted training data: {Key}", key);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Access denied deleting training data '{key}' from '{filePath}'. " +
                $"Current user lacks delete permissions for the file. " +
                $"Check file permissions and ensure file is not read-only. Error: {ex.Message}");
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: I/O error deleting training data '{key}' from '{filePath}'. " +
                $"File may be locked by another process or in use. " +
                $"Ensure file is not open in another application. Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Unexpected error deleting training data '{key}' from '{filePath}'. " +
                $"This may indicate a system-level problem. Error: {ex.Message}");
        }

        await Task.CompletedTask; // Make method async for interface compliance
    }

    public async Task<bool> ExistsAsync(string key)
    {
        ValidateKey(key);

        var filePath = GetFilePath(key);
        var exists = File.Exists(filePath);

        _logger.LogDebug("Training data exists check: {Key} = {Exists}", key, exists);
        
        return await Task.FromResult(exists);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string? pattern = null)
    {
        try
        {
            _logger.LogDebug("Listing training data keys in directory: {Directory}", _dataDirectory);

            if (!Directory.Exists(_dataDirectory))
            {
                _logger.LogDebug("Training data directory does not exist: {Directory}", _dataDirectory);
                return await Task.FromResult<IReadOnlyList<string>>(new List<string>());
            }

            var files = Directory.GetFiles(_dataDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();

            if (!string.IsNullOrEmpty(pattern))
            {
                // Simple wildcard pattern matching
                var regex = new System.Text.RegularExpressions.Regex(
                    "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$");
                files = files.Where(f => regex.IsMatch(f)).ToList();
            }

            _logger.LogDebug("Listed training data keys: {Count} files, Pattern: {Pattern}", 
                files.Count, pattern ?? "none");

            return await Task.FromResult<IReadOnlyList<string>>(files);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Access denied listing training data keys in directory '{_dataDirectory}'. " +
                $"Current user lacks read permissions for the directory. " +
                $"Check directory permissions. Error: {ex.Message}");
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: I/O error accessing training data directory '{_dataDirectory}'. " +
                $"Directory may not be accessible or filesystem error occurred. " +
                $"Check directory existence and permissions. Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Unexpected error listing training data keys in directory '{_dataDirectory}'. " +
                $"This may indicate a system-level problem. Error: {ex.Message}");
        }
    }

    private void EnsureDataDirectoryExists()
    {
        if (!Directory.Exists(_dataDirectory))
        {
            try
            {
                _logger.LogDebug("Creating training data directory: {Directory}", _dataDirectory);
                Directory.CreateDirectory(_dataDirectory);
                _logger.LogInformation("Created training data directory: {Directory}", _dataDirectory);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Access denied creating training data directory '{_dataDirectory}'. " +
                    $"Current user lacks write permissions for parent directory. " +
                    $"Ensure the application has appropriate directory creation permissions. Error: {ex.Message}");
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: I/O error creating training data directory '{_dataDirectory}'. " +
                    $"Check disk space and parent directory permissions. Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Unexpected error creating training data directory '{_dataDirectory}'. " +
                    $"This may indicate a system-level problem. " +
                    $"Check system resources and file system integrity. Error: {ex.Message}");
            }
        }
        else
        {
            _logger.LogDebug("Training data directory already exists: {Directory}", _dataDirectory);
        }
    }

    private string GetFilePath(string key)
    {
        // ARCHITECTURAL PRINCIPLE: Safe file naming with validation
        var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_dataDirectory, $"{safeKey}.json");
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "DESIGN DEFICIENCY: Training data key cannot be null or empty. " +
                "Provide a valid key identifier for data storage.");
        }

        if (key.Length > 200)
        {
            throw new ArgumentException(
                $"DESIGN DEFICIENCY: Training data key '{key}' exceeds maximum length of 200 characters. " +
                "Use shorter, descriptive keys for data identification.");
        }
    }

    private void CleanupTempFile(string tempFilePath)
    {
        if (File.Exists(tempFilePath))
        {
            try 
            { 
                File.Delete(tempFilePath);
                _logger.LogDebug("Cleaned up temporary file: {TempPath}", tempFilePath);
            } 
            catch (Exception cleanupEx)
            { 
                _logger.LogWarning(cleanupEx, "Could not clean up temporary file: {TempPath}", tempFilePath);
            }
        }
    }
}
