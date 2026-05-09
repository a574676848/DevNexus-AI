using System;

namespace DevNexus.Core.Services.Swarm.Analysis;

/// <summary>
/// 任务领域类型
/// </summary>
public enum DomainType
{
    General,        // 通用/闲聊
    Coding,         // 编程开发
    DataAnalysis,   // 数据分析
    Creative,       // 创意写作/设计
    OfficeWork,     // 办公自动化 (Office/Email)
    LifeAssistant   // 生活助手 (日程/提醒)
}
