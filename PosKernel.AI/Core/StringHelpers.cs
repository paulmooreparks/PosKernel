//
// Copyright 2025 Paul Moore Parks and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

namespace PosKernel.AI.Core;

internal static class StringHelpers {

    /// <summary>
    /// Safely truncates a string to the specified length without throwing exceptions
    /// ARCHITECTURAL PRINCIPLE: Fail-safe string operations for logging
    /// </summary>
    public static string SafeTruncateString(this string input, int maxLength, int startIndex = 0) {
        if (string.IsNullOrEmpty(input)) {
            return "[Empty string]";
        }

        if (maxLength <= 0) {
            return "[Invalid length]";
        }

        if (startIndex < 0 || startIndex >= input.Length) {
            return "[Invalid start index]";
        }

        var availableLength = input.Length - startIndex;
        var actualLength = Math.Min(maxLength, availableLength);

        if (actualLength <= 0) {
            return "[No content available]";
        }

        return input.Substring(startIndex, actualLength);
    }
}
