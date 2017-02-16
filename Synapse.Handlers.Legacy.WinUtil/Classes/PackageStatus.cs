using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Synapse.Handlers.Legacy.WinCore
{
    public enum PackageStatus
    {
        New = 1,
        Initializing = 2,
        Running = 3,
        Complete = 4,
        CompletedWithErrors = 5,
        Waiting = 6,
        Failed = 7,
        RollingBack = 8,
        RolledBack = 9,
        RollBackCompletedWithErrors = 10,
        RollBackFailed = 11,
        Cancelling = 12,
        Cancelled = 13
    }
}
