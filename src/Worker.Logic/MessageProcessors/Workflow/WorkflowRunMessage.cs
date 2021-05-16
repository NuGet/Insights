namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowRunMessage
    {
        public string WorkflowRunId { get; set; }
        public int AttemptCount { get; set; }
    }
}
