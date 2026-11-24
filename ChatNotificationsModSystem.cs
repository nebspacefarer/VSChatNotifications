using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ChatNotifications;

public class ChatNotificationsModSystem : ModSystem
{
    const string LogPrefix = "[ChatNotifications]";
    const string ChatDialogTypeName = "HudDialogChat";
    const string ChatComposerKey = "chat";
    const string TabsElementKey = "tabs";
    const string TabClickedMethodName = "OnTabClicked";

    ICoreClientAPI capi;

    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        api.Event.ChatMessage += OnChatMessage;
    }

    void OnChatMessage(int groupId, string message, EnumChatType chatType, string data)
    {
        // Notification messages already trigger the game's own tab jump.
        if (chatType != EnumChatType.OthersMessage) return;
        if (groupId == GlobalConstants.CurrentChatGroup) return;

        // Let the dialog finish handling the message before we switch.
        capi.Event.RegisterCallback(_ => SwitchToGroup(groupId), 1);
    }

    void SwitchToGroup(int groupId)
    {
        try
        {
            var dialog = capi.Gui.OpenedGuis.FirstOrDefault(g => g.GetType().Name == ChatDialogTypeName);
            if (dialog == null)
            {
                capi.Logger.Warning($"{LogPrefix} chat dialog '{ChatDialogTypeName}' not found");
                return;
            }

            var tabBar = dialog.Composers[ChatComposerKey]?.GetHorizontalTabs(TabsElementKey);
            if (tabBar == null)
            {
                capi.Logger.Warning($"{LogPrefix} tab bar '{TabsElementKey}' not found");
                return;
            }

            int index = Array.FindIndex(tabBar.tabs, t => t.DataInt == groupId);
            if (index < 0) return;

            // Private handler: sets the current group, clears the alarm, refreshes the text.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var click = dialog.GetType().GetMethod(TabClickedMethodName, flags);
            if (click == null)
            {
                capi.Logger.Warning($"{LogPrefix} method '{TabClickedMethodName}' not found");
                return;
            }
            click.Invoke(dialog, new object[] { groupId });

            // OnTabClicked doesn't move the highlight, so do it here.
            tabBar.activeElement = index;
        }
        catch (Exception e)
        {
            capi.Logger.Warning($"{LogPrefix} could not switch tab: {e.Message}");
        }
    }

    public override void Dispose()
    {
        if (capi != null) capi.Event.ChatMessage -= OnChatMessage;
    }
}
