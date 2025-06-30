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

        public void Run(string sAMAccountName, string name, string uuid, string emailAlias, string dc, string optionalJson, string date, string orderedBy)
        {
            string script = null;
            try
            {
                script = System.IO.File.ReadAllText(powershellScript);

                if (!string.IsNullOrEmpty(script))
                {
                    using (PowerShell powershell = PowerShell.Create())
                    {
                        var invokeCommand = "Invoke-Method";
                        invokeCommand += $" -SAMAccountName \"{sAMAccountName}\"";
                        invokeCommand += $" -Name \"{name}\"";
                        invokeCommand += $" -Uuid \"{uuid}\"";                        

                        if (!string.IsNullOrEmpty(emailAlias))
                        {
                            invokeCommand += $" -EmailAlias \"{emailAlias}\"";
                        }

                        if (!string.IsNullOrEmpty(dc))
                        {
                            invokeCommand += $" -DC \"{dc}\"";
                        }

                        if (!string.IsNullOrEmpty(optionalJson))
                        {
                            invokeCommand += $" -OptionalJson \"{optionalJson}\"";
                        }
                        
                        if (!string.IsNullOrEmpty(date))
                        {
                            invokeCommand += $" -Date \"{date}\"";
                        }

                        if (!string.IsNullOrEmpty(orderedBy))
                        {
                            invokeCommand += $" -OrderedBy \"{orderedBy}\"";
                        }

                        script += $"\n\n{invokeCommand}\n";

                        powershell.AddScript(script);
                        log.Information($"Invoking powershell {powershellScript}:\n{invokeCommand}");
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
