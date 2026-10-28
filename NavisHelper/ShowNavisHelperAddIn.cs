using System;
using Autodesk.Navisworks.Api.Plugins;

namespace NavisHelper
{
    /// <summary>
    /// Kemet fork: the NavisHelper button lives in Navisworks' own "Tool Add-ins" tab
    /// instead of a dedicated "NavisHelper" ribbon tab. Navisworks places every
    /// AddInLocation.AddIn plugin there automatically. The icon paths are relative to
    /// the plugin folder (NavisHelper.bundle\Contents\&lt;year&gt;\).
    /// </summary>
    [Plugin("NavisHelperShowPanel", "CBC", DisplayName = "NavisHelper",
        ToolTip = "Show or hide the NavisHelper panel")]
    // Kemet fork: AddInLocation.None - no button anywhere in Navisworks' ribbon. The panel is
    // opened from the "Helper" button on the Kemet Navis Addons tab.
    [AddInPlugin(AddInLocation.None,
        Icon = "icons\\NavisHelperAddIn_16.png",
        LargeIcon = "icons\\NavisHelperAddIn_32.png")]
    public class ShowNavisHelperAddIn : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            NavisHelperPanelToggle.Toggle();
            return 0;
        }
    }

    /// <summary>Opens or closes the NavisHelper dock pane.</summary>
    internal static class NavisHelperPanelToggle
    {
        internal static void Toggle()
        {
            try
            {
                RibbonLoader.EnsureAgentRuntimeInitialized();

                var pluginRecord = Autodesk.Navisworks.Api.Application.Plugins.FindPlugin("NavisHelperDockPane.CBC");
                if (pluginRecord == null)
                {
                    RibbonCommandMessages.ShowPluginMissing("NavisHelperDockPane.CBC");
                    return;
                }

                if (pluginRecord.IsLoaded == false)
                    pluginRecord.LoadPlugin();

                var dockPane = pluginRecord.LoadedPlugin as DockPanePlugin;
                if (dockPane == null)
                {
                    RibbonCommandMessages.ShowDockPaneLoadFailed(
                        pluginRecord.LoadedPlugin?.GetType().Name ?? "null");
                    return;
                }

                dockPane.Visible = !dockPane.Visible;
            }
            catch (Exception ex)
            {
                RibbonCommandMessages.ShowError(ex.Message);
            }
        }
    }
}
