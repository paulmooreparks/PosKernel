using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PosKernel.AI.Services;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// AI-powered semantic matching implementation.
    /// ARCHITECTURAL PRINCIPLE: Use AI for semantic understanding instead of string heuristics.
    /// </summary>
    public class AiSemanticMatchingService : ISemanticMatchingService
    {
        private readonly McpClient _aiClient;
        private readonly IConfigurableThresholdService _thresholds;
        private readonly ILogger<AiSemanticMatchingService> _logger;

        public AiSemanticMatchingService(
            McpClient aiClient,
            IConfigurableThresholdService thresholds,
            ILogger<AiSemanticMatchingService> logger)
        {
            _aiClient = aiClient ?? throw new ArgumentNullException(nameof(aiClient));
            _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IList<SemanticMatch>> FindSemanticMatchesAsync(
            string userInput,
            IEnumerable<SemanticCandidate> candidates,
            string context)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return Array.Empty<SemanticMatch>();
            }

            var candidateList = candidates.ToList();
            if (!candidateList.Any())
            {
                return Array.Empty<SemanticMatch>();
            }

            try
            {
                var prompt = BuildSemanticMatchingPrompt(userInput, candidateList, context);
                var response = await _aiClient.CompleteWithToolsAsync(prompt, Array.Empty<McpTool>());

                return ParseSemanticMatchingResponse(response.Content, candidateList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform AI semantic matching for context: {Context}", context);
                // ARCHITECTURAL PRINCIPLE: Fail fast instead of silent fallback
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: AI semantic matching failed for context '{context}'. " +
                    $"Cannot perform intelligent matching without functional AI service.", ex);
            }
        }

        public async Task<SemanticMatch?> GetBestMatchAsync(
            string userInput,
            IEnumerable<SemanticCandidate> candidates,
            string context)
        {
            var matches = await FindSemanticMatchesAsync(userInput, candidates, context);

            var bestMatch = matches.OrderByDescending(m => m.Confidence).FirstOrDefault();

            // Check if best match meets minimum threshold
            if (bestMatch != null)
            {
                var minThreshold = _thresholds.GetConfidenceThreshold(context, "minimum_match");
                if (bestMatch.Confidence >= minThreshold)
                {
                    return bestMatch;
                }
            }

            return null;
        }

        public async Task<bool> MatchesConceptAsync(string userInput, string concept, string context)
        {
            if (string.IsNullOrWhiteSpace(userInput) || string.IsNullOrWhiteSpace(concept))
            {
                return false;
            }

            try
            {
                var prompt = BuildConceptMatchingPrompt(userInput, concept, context);
                var response = await _aiClient.CompleteWithToolsAsync(prompt, Array.Empty<McpTool>());

                return ParseConceptMatchingResponse(response.Content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform AI concept matching for concept: {Concept}", concept);
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: AI concept matching failed for concept '{concept}'. " +
                    $"Cannot perform intelligent concept detection without functional AI service.", ex);
            }
        }

        private string BuildSemanticMatchingPrompt(string userInput, List<SemanticCandidate> candidates, string context)
        {
            var candidatesJson = JsonSerializer.Serialize(candidates.Select(c => new
            {
                id = c.Id,
                name = c.DisplayName,
                aliases = c.Aliases,
                description = c.Description
            }), new JsonSerializerOptions { WriteIndented = true });

            return $@"# Semantic Matching Task

You are performing semantic similarity matching for a POS system.

## Context
- User input: ""{userInput}""
- Matching context: {context}
- Available candidates: {candidates.Count}

## Candidates
{candidatesJson}

## Task
Analyze the user input semantically and determine similarity to each candidate.
Consider:
- Semantic meaning, not just string similarity
- Cultural variations and synonyms
- Context-appropriate interpretation
- Abbreviations and common variations

## Response Format
Return a JSON array of matches with confidence scores (0.0-1.0):
```json
[
  {{
    ""candidate_id"": ""id"",
    ""confidence"": 0.85,
    ""reasoning"": ""explanation of semantic similarity""
  }}
]
```

Provide matches in descending confidence order.";
        }

        private string BuildConceptMatchingPrompt(string userInput, string concept, string context)
        {
            return $@"# Concept Matching Task

Determine if the user input semantically represents the given concept.

## Input
- User input: ""{userInput}""
- Target concept: ""{concept}""
- Context: {context}

## Task
Does the user input semantically represent or indicate the target concept?
Consider cultural variations, synonyms, and context-appropriate interpretation.

## Response Format
Respond with exactly: ""YES"" or ""NO""
Then provide a brief reasoning on the next line.";
        }

        private List<SemanticMatch> ParseSemanticMatchingResponse(string response, List<SemanticCandidate> candidates)
        {
            try
            {
                // Extract JSON from response if it contains additional text
                var jsonStart = response.IndexOf('[');
                var jsonEnd = response.LastIndexOf(']');

                if (jsonStart >= 0 && jsonEnd >= jsonStart)
                {
                    var jsonText = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var results = JsonSerializer.Deserialize<JsonElement[]>(jsonText);

                    var matches = new List<SemanticMatch>();

                    foreach (var result in results ?? Array.Empty<JsonElement>())
                    {
                        if (result.TryGetProperty("candidate_id", out var idElement) &&
                            result.TryGetProperty("confidence", out var confidenceElement))
                        {
                            var candidateId = idElement.GetString();
                            var confidence = confidenceElement.GetDouble();

                            var candidate = candidates.FirstOrDefault(c => c.Id == candidateId);
                            if (candidate != null)
                            {
                                var reasoning = result.TryGetProperty("reasoning", out var reasonElement)
                                    ? reasonElement.GetString() : null;

                                matches.Add(new SemanticMatch
                                {
                                    Candidate = candidate,
                                    Confidence = confidence,
                                    Reasoning = reasoning
                                });
                            }
                        }
                    }

                    return matches;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse semantic matching response: {Response}", response);
            }

            return new List<SemanticMatch>();
        }

        private bool ParseConceptMatchingResponse(string response)
        {
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var firstLine = lines.FirstOrDefault()?.Trim().ToUpperInvariant();

            return firstLine == "YES";
        }
    }
}
