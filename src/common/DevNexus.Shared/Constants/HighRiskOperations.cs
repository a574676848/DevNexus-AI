using System.Collections.Generic;

namespace DevNexus.Shared.Constants;

public static class HighRiskOperations
{
    public static readonly HashSet<string> All = new()
    {
        // 宿主服务 (HostService)
        "ExecuteCommand", 
        "DeleteFile", 
        "DeleteDirectory",
        "MoveFile",
        "OverwriteFile",
        
        // 数据库高危操作
        "DropDatabase",
        "TruncateTable",
        "DeleteAll"
    };
}
