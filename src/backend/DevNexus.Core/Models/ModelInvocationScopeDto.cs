using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Models;

public class ModelInvocationScopeDto
{
    public string OwnerType { get; set; } = ModelInvocationOwnerTypes.System;

    public Guid? OwnerUserId { get; set; }

    public Guid? SessionId { get; set; }

    public Guid? MessageId { get; set; }

    public string SceneCode { get; set; } = ModelInvocationSceneCodes.SystemOther;

    public string SceneCategory { get; set; } = ModelInvocationSceneCategories.Other;

    public string ResourceType { get; set; } = ModelInvocationResourceTypes.None;

    public string? ResourceId { get; set; }
}