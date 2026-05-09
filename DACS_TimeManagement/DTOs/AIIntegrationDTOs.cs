using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DACS_TimeManagement.DTOs
{
    // --- Application Internal DTOs ---

    public class AIRequestDTO
    {
        public GoalInfo Goal { get; set; }
        public ProjectInfo Project { get; set; }
    }

    public class GoalInfo
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public int CompletedTasks { get; set; }
        public int TargetTasks { get; set; }
        public double CompletedHours { get; set; }
        public double? TargetHours { get; set; }
        public DateTime TargetDate { get; set; }
    }

    public class ProjectInfo
    {
        public string Name { get; set; }
        public string Detail { get; set; }
        public string Status { get; set; }
    }

    // --- Gemini API Structure DTOs (For reference or strongly typed parsing if needed) ---

    public class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate> Candidates { get; set; }
    }

    public class Candidate
    {
        [JsonPropertyName("content")]
        public Content Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string FinishReason { get; set; }
    }

    public class Content
    {
        [JsonPropertyName("parts")]
        public List<Part> Parts { get; set; }
    }

    public class Part
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    public class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<ContentRequest> Contents { get; set; }
    }

    public class ContentRequest
    {
        [JsonPropertyName("parts")]
        public List<PartRequest> Parts { get; set; }
    }

    public class PartRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
