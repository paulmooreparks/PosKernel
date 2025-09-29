using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// AI-driven semantic matching service to replace hardcoded string matching.
    /// ARCHITECTURAL PRINCIPLE: Use AI for semantic understanding instead of brittle string heuristics.
    /// </summary>
    public interface ISemanticMatchingService
    {
        /// <summary>
        /// Determines semantic similarity between user input and target options using AI.
        /// Replaces hardcoded StartsWith, Contains, string distance calculations.
        /// </summary>
        /// <param name="userInput">The user's input text</param>
        /// <param name="candidates">List of possible matches with their identifiers</param>
        /// <param name="context">Context for matching (e.g., "payment_method", "product_name", "completion_phrase")</param>
        /// <returns>Ordered matches with confidence scores</returns>
        Task<IList<SemanticMatch>> FindSemanticMatchesAsync(string userInput,
            IEnumerable<SemanticCandidate> candidates,
            string context);

        /// <summary>
        /// Gets the best semantic match above the configured confidence threshold.
        /// </summary>
        Task<SemanticMatch?> GetBestMatchAsync(string userInput,
            IEnumerable<SemanticCandidate> candidates,
            string context);

        /// <summary>
        /// Determines if user input semantically represents a business concept.
        /// Replaces hardcoded business rule detection.
        /// </summary>
        Task<bool> MatchesConceptAsync(string userInput, string concept, string context);
    }

    /// <summary>
    /// Candidate for semantic matching.
    /// </summary>
    public class SemanticCandidate
    {
        public required string Id { get; set; }
        public required string DisplayName { get; set; }
        public string[]? Aliases { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Result of semantic matching with confidence score.
    /// </summary>
    public class SemanticMatch
    {
        public required SemanticCandidate Candidate { get; set; }
        public required double Confidence { get; set; }
        public string? Reasoning { get; set; }
    }
}
