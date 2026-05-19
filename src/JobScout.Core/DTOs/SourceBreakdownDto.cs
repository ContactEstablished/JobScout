using JobScout.Core.Enums;

namespace JobScout.Core.DTOs;

public class SourceBreakdownDto
{
    public JobSource Source { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public int JobCount { get; set; }
    public int StrongFitCount { get; set; }
}
