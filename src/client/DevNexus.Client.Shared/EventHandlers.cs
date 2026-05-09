using System;
using Microsoft.AspNetCore.Components;

namespace DevNexus.Client.Shared;

[EventHandler("oncompositionstart", typeof(EventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("oncompositionend", typeof(EventArgs), enableStopPropagation: true, enablePreventDefault: true)]
public static class EventHandlers
{
}