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

using System.Collections.Concurrent;
using PosKernel.AI.Interfaces;

namespace PosKernel.AI.Core
{
    /// <summary>
    /// High-performance thought logging system for AI reasoning transparency.
    /// Shows the AI's decision process in real-time without impacting customer experience.
    /// </summary>
    public class ThoughtLogger
    {
        private readonly ConcurrentQueue<ThoughtEntry> _thoughts = new();
        private readonly Timer _flushTimer;
        private readonly ILogDisplay _logDisplay;
        private volatile bool _disposed;

        public ThoughtLogger(ILogDisplay logDisplay)
        {
            _logDisplay = logDisplay ?? throw new ArgumentNullException(nameof(logDisplay));
            
            // Batch flush every 100ms to avoid UI performance impact
            _flushTimer = new Timer(FlushToDebugWindow, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }

        /// <summary>
        /// Logs a thought from the AI reasoning process.
        /// Thread-safe and non-blocking for performance.
        /// </summary>
        public void LogThought(string thought)
        {
            if (_disposed) {
                return;
            }
            
            _thoughts.Enqueue(new ThoughtEntry
            {
                Timestamp = DateTime.Now,
                Content = thought,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            });
        }

        /// <summary>
        /// Logs a thought with confidence level for decision tracking.
        /// </summary>
        public void LogThought(string thought, double confidence)
        {
            LogThought($"{thought} (confidence: {confidence:F2})");
        }

        /// <summary>
        /// Logs a step in the AI's reasoning process.
        /// </summary>
        public void LogStep(int step, string description)
        {
            LogThought($"📋 Step {step}: {description}");
        }

        /// <summary>
        /// Logs the AI's analysis of user input.
        /// </summary>
        public void LogAnalysis(string userInput, string analysis)
        {
            LogThought($"🔍 Analyzing '{userInput}': {analysis}");
        }

        /// <summary>
        /// Logs a decision made by the AI.
        /// </summary>
        public void LogDecision(string decision, string reasoning)
        {
            LogThought($"🎯 Decision: {decision} | Reasoning: {reasoning}");
        }

        private void FlushToDebugWindow(object? state)
        {
            if (_disposed) {
                return;
            }

            var batch = new List<ThoughtEntry>();
            while (_thoughts.TryDequeue(out var thought) && batch.Count < 20)
            {
                batch.Add(thought);
            }

            if (batch.Any())
            {
                try
                {
                    foreach (var thought in batch)
                    {
                        _logDisplay.AddLog($"THOUGHT: {thought.Content}");
                    }
                }
                catch
                {
                    // Ignore logging errors to prevent disrupting the main application
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _flushTimer?.Dispose();
            
            // Flush any remaining thoughts
            FlushToDebugWindow(null);
        }

        private class ThoughtEntry
        {
            public DateTime Timestamp { get; set; }
            public string Content { get; set; } = string.Empty;
            public int ThreadId { get; set; }
        }
    }
}
