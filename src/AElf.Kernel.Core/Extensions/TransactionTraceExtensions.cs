namespace AElf.Kernel
{
    public static class TransactionTraceExtensions
    {
        public static bool IsSuccessful(this TransactionTrace txTrace)
        {
            var successful = txTrace.ExecutionStatus == ExecutionStatus.Executed;
            if (!successful)
            {
                return false;
            }

            foreach (var trace in txTrace.PreTraces)
            {
                if (!trace.IsSuccessful())
                {
                    return false;
                }
            }

            foreach (var trace in txTrace.InlineTraces)
            {
                if (!trace.IsSuccessful())
                {
                    return false;
                }
            }

            foreach (var trace in txTrace.PostTraces)
            {
                if (!trace.IsSuccessful())
                {
                    return false;
                }
            }

            return true;
        }

        public static void SurfaceUpError(this TransactionTrace txTrace)
        {
            foreach (var inline in txTrace.InlineTraces)
            {
                inline.SurfaceUpError();
                if (inline.ExecutionStatus < txTrace.ExecutionStatus)
                {
                    txTrace.ExecutionStatus = inline.ExecutionStatus;
                    txTrace.Error = $"{inline.Error}";
                }
            }

            if (txTrace.ExecutionStatus == ExecutionStatus.Postfailed)
            {
                foreach (var trace in txTrace.PostTraces)
                {
                    trace.SurfaceUpError();
                    if (!trace.IsSuccessful())
                    {
                        txTrace.Error += $"Post-Error: {trace.Error}";
                    }
                }
            }

            if (txTrace.ExecutionStatus == ExecutionStatus.Prefailed)
            {
                foreach (var trace in txTrace.PreTraces)
                {
                    trace.SurfaceUpError();
                    if (!trace.IsSuccessful())
                    {
                        txTrace.Error += $"Pre-Error: {trace.Error}";
                    }
                }
            }
        }
    }
}