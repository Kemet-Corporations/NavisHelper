using System;
using System.Windows.Threading;
using Autodesk.Navisworks.Api.Plugins;
using NavisHelper.Agent;
using NavisHelper.Core;

namespace NavisHelper
{
    /// <summary>
    /// Starts the agent runtime (the MCP host bridge) when Navisworks loads.
    /// Kemet fork: this plugin no longer builds a "NavisHelper" ribbon tab. The
    /// button that opens the panel is <see cref="ShowNavisHelperAddIn"/>, which
    /// Navisworks shows in its own "Tool Add-ins" tab.
    /// </summary>
    [Plugin("NavisHelper.RibbonLoader", "NavisHelper", DisplayName = "NavisHelper")]
    public class RibbonLoader : EventWatcherPlugin
    {
        private DispatcherTimer _timer;
        private ExceptionObservation _exceptionObservation;

        internal static void EnsureAgentRuntimeInitialized()
        {
            AgentRuntime.Initialize(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        }

        public override void OnLoaded()
        {
            StopObservationAndTimer();
            _exceptionObservation = new ExceptionObservation();
            _exceptionObservation.Start();
            try
            {
                EnsureAgentRuntimeInitialized();
                // Kept from upstream: a short retry loop in case the runtime could not
                // start while Navisworks was still building its UI.
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(2);
                _timer.Tick += OnTimerTick;
                _timer.Start();
            }
            catch
            {
                StopObservationAndTimer();
                throw;
            }
        }

        public override void OnUnloading()
        {
            try
            {
                AgentRuntime.Shutdown();
            }
            finally
            {
                StopObservationAndTimer();
            }
        }

        private void StopObservationAndTimer()
        {
            try
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Tick -= OnTimerTick;
                    _timer = null;
                }
            }
            finally
            {
                _exceptionObservation?.Dispose();
                _exceptionObservation = null;
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            try
            {
                EnsureAgentRuntimeInitialized();
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Tick -= OnTimerTick;
                }
            }
            catch (Exception ex)
            {
                // Preserve the retry, but record its first failure once per plugin load.
                _exceptionObservation?.ReportRibbonRetry(ex);
            }
        }
    }
}
