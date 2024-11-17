using System;
using Serilog;
using System.Management.Automation;

namespace SOFD
{
    class PowershellRunner
    {
        private static ILogger log = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(PowershellRunner));
        private string powershellScript;

        public PowershellRunner(string powershellScript)
        {
            this.powershellScript = powershellScript;
        }

        public void Run(string sAMAccountName, string name, string uuid, string emailAlias, string dc, string optionalJson = null)
        {
            string script = null;
            try
            {
                script = System.IO.File.ReadAllText(powershellScript);

                if (!string.IsNullOrEmpty(script))
                {
                    using (PowerShell powershell = PowerShell.Create())
                    {
                        script += "\n\n" +
                            "$ppArg1=\"" + sAMAccountName + "\"\n" +
                            "$ppArg2=\"" + name + "\"\n" +
                            "$ppArg3=\"" + uuid + "\"\n" +
                            "$ppArg4=\"" + (emailAlias == null ? "" : emailAlias) + "\"\n" +
                            "$ppArg5=\"" + dc + "\"\n" +
                            "$ppArg6='" + (optionalJson == null ? "" : optionalJson) + "'\n";

                        script += "\nInvoke-Method -SAMAccountName $ppArg1 -Name $ppArg2 -Uuid $ppArg3";

                        if (!string.IsNullOrEmpty(emailAlias))
                        {
                            script += " -EmailAlias $ppArg4";
                        }

                        if (!string.IsNullOrEmpty(dc))
                        {
                            script += " -DC $ppArg5";
                        }

                        if (!string.IsNullOrEmpty(optionalJson))
                        {
                            script += " -optionalJson $ppArg6";
                        }

                        script += "\n";

                        powershell.AddScript(script);
                        powershell.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to run powershell script: " + script);
            }
        }
    }
}
