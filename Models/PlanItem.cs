using System;

namespace OllamaCoderIDE.Models;

public enum PlanItemStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public class PlanItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PlanItemStatus Status { get; set; } = PlanItemStatus.Pending;
    public bool IsSelected { get; set; } = true;
}
