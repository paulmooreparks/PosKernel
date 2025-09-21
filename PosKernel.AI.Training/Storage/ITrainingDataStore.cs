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

namespace PosKernel.AI.Training.Storage;

/// <summary>
/// Abstraction for training data persistence, supporting multiple storage backends
/// </summary>
public interface ITrainingDataStore
{
    /// <summary>
    /// Loads data by key, returning null if not found
    /// </summary>
    T? Load<T>(string key) where T : class;

    /// <summary>
    /// Saves data with the specified key
    /// </summary>
    Task SaveAsync<T>(string key, T data) where T : class;

    /// <summary>
    /// Deletes data by key
    /// </summary>
    Task DeleteAsync(string key);

    /// <summary>
    /// Checks if key exists
    /// </summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Lists all keys matching optional pattern
    /// </summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string? pattern = null);
}
