using DevNexus.Client.Shared.Models;
using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Services.Storage;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Chat;

public partial class InputBox
{
    #region Provider Handling

    private async Task HandleProviderChanged((Guid? ProviderId, string? ProviderName) selection)
    {
        _selectedProviderId = selection.ProviderId;
        _selectedProviderName = selection.ProviderName;

        if (OnProviderChanged.HasDelegate)
        {
            await OnProviderChanged.InvokeAsync(_selectedProviderId);
        }

        await InvokeAsync(StateHasChanged);
    }

    #endregion

}

