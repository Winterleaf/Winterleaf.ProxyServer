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
using System.Linq;
using System.ServiceProcess;
using Winterleaf.ProxyServer.Framework;

namespace Winterleaf.ProxyServer
{
    internal static class Program
    {
        // The main entry point for the windows service application.
        private static void Main(string[] args)
        {
            try
                {
                // if install was a command line flag, then run the installer at runtime.
                if (args.Contains("-install", StringComparer.InvariantCultureIgnoreCase))
                    WindowsServiceInstaller.RuntimeInstall<ServiceImplementation>();

                    // if uninstall was a command line flag, run uninstaller at runtime.
                else if (args.Contains("-uninstall", StringComparer.InvariantCultureIgnoreCase))
                    WindowsServiceInstaller.RuntimeUnInstall<ServiceImplementation>();

                    // otherwise, fire up the service as either console or windows service based on UserInteractive property.
                else
                    {
                    ServiceImplementation implementation = new ServiceImplementation();

                    // if started from console, file explorer, etc, run as console app.
                    if (Environment.UserInteractive)
                        ConsoleHarness.Run(args, implementation);

                        // otherwise run as a windows service
                    else
                        ServiceBase.Run(new WindowsServiceHarness(implementation));
                    }
                }

            catch (Exception ex)
                {
                ConsoleHarness.WriteToConsole(ConsoleColor.Red, "An exception occurred in Main(): {0}", ex);
                }
        }
    }
}