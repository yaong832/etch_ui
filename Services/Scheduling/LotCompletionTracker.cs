namespace etch_ui.Services.Scheduling;

/// <summary>Side Stg 카세트(25매) 출하 기준 LOT 완료 추적.</summary>
public sealed class LotCompletionTracker
{
    public int TargetCount { get; private set; }
    public int CompletedCount { get; private set; }

    public void Reset(int targetCount)
    {
        TargetCount = targetCount;
        CompletedCount = 0;
    }

    public void RecordWaferCompleted()
    {
        if (CompletedCount < TargetCount)
        {
            CompletedCount++;
        }
    }

    public bool IsTargetMet => CompletedCount >= TargetCount && TargetCount > 0;
}
