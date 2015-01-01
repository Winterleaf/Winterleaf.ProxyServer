// WinterLeaf Entertainment
// Copyright (c) 2014, WinterLeaf Entertainment LLC
// 
// 
// THIS SOFTWARE IS PROVIDED BY WINTERLEAF ENTERTAINMENT LLC ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES,
//  INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR 
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL WINTERLEAF ENTERTAINMENT LLC BE LIABLE FOR ANY DIRECT, INDIRECT, 
// INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND 
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR 
// OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH 
// DAMAGE. 

using System;
using System.ServiceProcess;

namespace Winterleaf.ProxyServer.Framework
{
    /// <summary>
    /// A generic Windows Service that can handle any assembly that
    /// implements IWindowsService (including AbstractWindowsService) 
    /// </summary>
    public partial class WindowsServiceHarness : ServiceBase
    {

        /// <summary>
        /// Constructor a generic windows service from the given class
        /// </summary>
        /// <param name="serviceImplementation">Service implementation.</param>
        public WindowsServiceHarness(IWindowsService serviceImplementation)
        {
            // make sure service passed in is valid
            if (serviceImplementation == null)
                throw new ArgumentNullException("serviceImplementation", "IWindowsService cannot be null in call to GenericWindowsService");

            // set instance and backward instance
            ServiceImplementation = serviceImplementation;

            // configure our service
            ConfigureServiceFromAttributes(serviceImplementation);
        }

        /// <summary>
        /// Get the class implementing the windows service
        /// </summary>
        public IWindowsService ServiceImplementation { get; private set; }

        /// <summary>
        /// Override service control on continue
        /// </summary>
        protected override void OnContinue()
        {
            // perform class specific behavior 
            ServiceImplementation.OnContinue();
        }

        /// <summary>
        /// Called when service is paused
        /// </summary>
        protected override void OnPause()
        {
            // perform class specific behavior 
            ServiceImplementation.OnPause();
        }

        /// <summary>
        /// Called when the Operating System is shutting down
        /// </summary>
        protected override void OnShutdown()
        {
            // perform class specific behavior
            ServiceImplementation.OnShutdown();
        }

        /// <summary>
        /// Called when service is requested to start
        /// </summary>
        /// <param name="args">The startup arguments array.</param>
        protected override void OnStart(string[] args)
        {
            ServiceImplementation.OnStart(args);
        }

        /// <summary>
        /// Called when service is requested to stop
        /// </summary>
        protected override void OnStop()
        {
            ServiceImplementation.OnStop();
        }

        /// <summary>
        /// Set configuration data
        /// </summary>
        /// <param name="serviceImplementation">The service with configuration settings.</param>
        private void ConfigureServiceFromAttributes(IWindowsService serviceImplementation)
        {
            WindowsServiceAttribute attribute = serviceImplementation.GetType().GetAttribute<WindowsServiceAttribute>();

            if (attribute != null)
                {
                // wire up the event log source, if provided
                if (!string.IsNullOrWhiteSpace(attribute.EventLogSource))
                    {
                    // assign to the base service's EventLog property for auto-log events.
                    EventLog.Source = attribute.EventLogSource;
                    }

                CanStop = attribute.CanStop;
                CanPauseAndContinue = attribute.CanPauseAndContinue;
                CanShutdown = attribute.CanShutdown;

                // we don't handle: laptop power change event
                CanHandlePowerEvent = false;

                // we don't handle: Term Services session event
                CanHandleSessionChangeEvent = false;

                // always auto-event-log 
                AutoLog = true;
                }
            else
                throw new InvalidOperationException(string.Format("IWindowsService implementer {0} must have a WindowsServiceAttribute.", serviceImplementation.GetType().FullName));
        }
    }
}