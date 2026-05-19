namespace JobScout.Core.DTOs;

public class RecalibrateRequest
{
    public Guid ProfileId { get; set; }
    public bool ResetHistory { get; set; }
}
