using System;
using System.Collections.Generic;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Service for managing context-aware, configurable confidence thresholds.
    /// ARCHITECTURAL PRINCIPLE: Replace magic numbers with configurable, context-aware thresholds.
    /// </summary>
    public interface IConfigurableThresholdService
    {
        /// <summary>
        /// Gets confidence threshold for a specific context and operation.
        /// Replaces hardcoded magic numbers like 0.8, 0.9.
        /// </summary>
        /// <param name="context">Context (e.g., "payment_method_matching", "product_disambiguation")</param>
        /// <param name="operation">Operation (e.g., "auto_add", "require_confirmation")</param>
        /// <returns>Confidence threshold (0.0-1.0)</returns>
        double GetConfidenceThreshold(string context, string operation);

        /// <summary>
        /// Determines if confidence score meets threshold for the given context and operation.
        /// </summary>
        bool MeetsThreshold(double confidence, string context, string operation);

        /// <summary>
        /// Gets all configured thresholds for debugging/diagnostics.
        /// </summary>
        IReadOnlyDictionary<string, double> GetAllThresholds();
    }

    /// <summary>
    /// Default implementation using store configuration.
    /// </summary>
    public class ConfigurableThresholdService : IConfigurableThresholdService
    {
        private readonly Dictionary<string, double> _thresholds;

        public ConfigurableThresholdService()
        {
            // ARCHITECTURAL PRINCIPLE: Reasonable defaults that can be overridden by configuration
            _thresholds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Product matching thresholds
                ["product_disambiguation.auto_add"] = 0.85,
                ["product_disambiguation.require_confirmation"] = 0.60,
                ["product_matching.exact_match"] = 0.90,

                // Payment method thresholds
                ["payment_method_matching.auto_process"] = 0.80,
                ["payment_method_matching.require_confirmation"] = 0.50,

                // Completion detection thresholds
                ["completion_detection.confident"] = 0.75,
                ["completion_detection.possible"] = 0.50,

                // Set detection thresholds
                ["set_detection.confident"] = 0.80,
                ["set_detection.possible"] = 0.60,

                // General semantic matching
                ["semantic_matching.high_confidence"] = 0.85,
                ["semantic_matching.medium_confidence"] = 0.70,
                ["semantic_matching.low_confidence"] = 0.50
            };
        }

        public double GetConfidenceThreshold(string context, string operation)
        {
            var key = $"{context}.{operation}";
            if (_thresholds.TryGetValue(key, out var threshold))
            {
                return threshold;
            }

            // ARCHITECTURAL PRINCIPLE: Fail fast if threshold not configured
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Confidence threshold not configured for '{key}'. " +
                $"Add threshold configuration to avoid hardcoded magic numbers.");
        }

        public bool MeetsThreshold(double confidence, string context, string operation)
        {
            var threshold = GetConfidenceThreshold(context, operation);
            return confidence >= threshold;
        }

        public IReadOnlyDictionary<string, double> GetAllThresholds()
        {
            return _thresholds;
        }
    }
}
