namespace JobScout.Core.DTOs;

public record ResumeParseResult(string PlainText, int WordCount, string[] DetectedSkills);
