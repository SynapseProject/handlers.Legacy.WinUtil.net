using System;


namespace Synapse.Handlers.Legacy.WinCore
{
    public class AdapterProgressEventArgs
    {
        public AdapterProgressEventArgs(string context, string message, PackageStatus status = PackageStatus.Running, int id = 0, int severity = 0, Exception ex = null)
        {
            Context = context;
            Exception = ex;
            Id = id;
            Message = message;
            Severity = severity;
            Status = status;
        }

        public string Context { get; protected set; }
        public Exception Exception { get; protected set; }
        public bool HasException { get; }
        public int Id { get; protected set; }
        public string Message { get; protected set; }
        public int Severity { get; protected set; }
        public PackageStatus Status { get; protected set; }
    }
}