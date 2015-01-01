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
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;

namespace Winterleaf.ProxyServer.Framework
{
    /// <summary>
    /// A generic windows service installer
    /// </summary>
    [RunInstaller(true)]
    public partial class WindowsServiceInstaller : Installer
    {

        /// <summary>
        /// Creates a blank windows service installer with configuration in ServiceImplementation
        /// </summary>
        public WindowsServiceInstaller() : this(typeof (ServiceImplementation))
        {
        }

        /// <summary>
        /// Creates a windows service installer using the type specified.
        /// </summary>
        /// <param name="windowsServiceType">The type of the windows service to install.</param>
        public WindowsServiceInstaller(Type windowsServiceType)
        {
            if (!windowsServiceType.GetInterfaces().Contains(typeof (IWindowsService)))
                throw new ArgumentException("Type to install must implement IWindowsService.", "windowsServiceType");

            WindowsServiceAttribute attribute = windowsServiceType.GetAttribute<WindowsServiceAttribute>();

            if (attribute == null)
                throw new ArgumentException("Type to install must be marked with a WindowsServiceAttribute.", "windowsServiceType");

            Configuration = attribute;
        }

        /// <summary>
        /// Gets or sets the type of the windows service to install.
        /// </summary>
        public WindowsServiceAttribute Configuration { get; set; }

        /// <summary>
        /// Performs a transacted installation at run-time of the AutoCounterInstaller and any other listed installers.
        /// </summary>
        /// <param name="otherInstallers">The other installers to include in the transaction</param>
        /// <typeparam name="T">The IWindowsService implementer to install.</typeparam>
        public static void RuntimeInstall<T>() where T : IWindowsService
        {
            string path = "/assemblypath=" + Assembly.GetEntryAssembly().Location;

            using (TransactedInstaller ti = new TransactedInstaller())
                {
                ti.Installers.Add(new WindowsServiceInstaller(typeof (T)));
                ti.Context = new InstallContext(null, new[] {path});
                ti.Install(new Hashtable());
                }
        }

        /// <summary>
        /// Performs a transacted un-installation at run-time of the AutoCounterInstaller and any other listed installers.
        /// </summary>
        /// <param name="otherInstallers">The other installers to include in the transaction</param>
        /// <typeparam name="T">The IWindowsService implementer to install.</typeparam>
        public static void RuntimeUnInstall<T>(params Installer[] otherInstallers) where T : IWindowsService
        {
            string path = "/assemblypath=" + Assembly.GetEntryAssembly().Location;

            using (TransactedInstaller ti = new TransactedInstaller())
                {
                ti.Installers.Add(new WindowsServiceInstaller(typeof (T)));
                ti.Context = new InstallContext(null, new[] {path});
                ti.Uninstall(null);
                }
        }

        /// <summary>
        /// Installer class, to use run InstallUtil against this .exe
        /// </summary>
        /// <param name="savedState">The saved state for the installation.</param>
        public override void Install(IDictionary savedState)
        {
            ConsoleHarness.WriteToConsole(ConsoleColor.White, "Installing service {0}.", Configuration.Name);

            // install the service 
            ConfigureInstallers();
            base.Install(savedState);

            // wire up the event log source, if provided
            if (!string.IsNullOrWhiteSpace(Configuration.EventLogSource))
                {
                // create the source if it doesn't exist
                if (!EventLog.SourceExists(Configuration.EventLogSource))
                    EventLog.CreateEventSource(Configuration.EventLogSource, "Application");
                }
        }

        /// <summary>
        /// Removes the counters, then calls the base uninstall.
        /// </summary>
        /// <param name="savedState">The saved state for the installation.</param>
        public override void Uninstall(IDictionary savedState)
        {
            ConsoleHarness.WriteToConsole(ConsoleColor.White, "Un-Installing service {0}.", Configuration.Name);

            // load the assembly file name and the config
            ConfigureInstallers();
            base.Uninstall(savedState);

            // wire up the event log source, if provided
            if (!string.IsNullOrWhiteSpace(Configuration.EventLogSource))
                {
                // create the source if it doesn't exist
                if (EventLog.SourceExists(Configuration.EventLogSource))
                    EventLog.DeleteEventSource(Configuration.EventLogSource);
                }
        }

        /// <summary>
        /// Rolls back to the state of the counter, and performs the normal rollback.
        /// </summary>
        /// <param name="savedState">The saved state for the installation.</param>
        public override void Rollback(IDictionary savedState)
        {
            ConsoleHarness.WriteToConsole(ConsoleColor.White, "Rolling back service {0}.", Configuration.Name);

            // load the assembly file name and the config
            ConfigureInstallers();
            base.Rollback(savedState);
        }

        /// <summary>
        /// Method to configure the installers
        /// </summary>
        private void ConfigureInstallers()
        {
            // load the assembly file name and the config
            Installers.Add(ConfigureProcessInstaller());
            Installers.Add(ConfigureServiceInstaller());
        }

        /// <summary>
        /// Helper method to configure a process installer for this windows service
        /// </summary>
        /// <returns>Process installer for this service</returns>
        private ServiceProcessInstaller ConfigureProcessInstaller()
        {
            ServiceProcessInstaller result = new ServiceProcessInstaller();

            // if a user name is not provided, will run under local service acct
            if (string.IsNullOrEmpty(Configuration.UserName))
                {
                result.Account = ServiceAccount.LocalService;
                result.Username = null;
                result.Password = null;
                }
            else
                {
                // otherwise, runs under the specified user authority
                result.Account = ServiceAccount.User;
                result.Username = Configuration.UserName;
                result.Password = Configuration.Password;
                }

            return result;
        }

        /// <summary>
        /// Helper method to configure a service installer for this windows service
        /// </summary>
        /// <returns>Process installer for this service</returns>
        private ServiceInstaller ConfigureServiceInstaller()
        {
            // create and config a service installer
            ServiceInstaller result = new ServiceInstaller {ServiceName = Configuration.Name, DisplayName = Configuration.DisplayName, Description = Configuration.Description, StartType = Configuration.StartMode,};

            return result;
        }
    }
}