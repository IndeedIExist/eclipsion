namespace Content.Shared._Crescent.SingleLifeJob;  

public abstract class SingleLifeJobTrackerSystem : EntitySystem  
{  
    public abstract bool HasPlayedThisRound(string jobId);  
}