using System;
using Serilog;
using System.Management.Automation;

namespace SOFD
{
    class ExchangePowershellRunner
    {
        private static ILogger log = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(ExchangePowershellRunner));
        private string powershellScript;

        public ExchangePowershellRunner(string powershellScript)
        {
            this.powershellScript = powershellScript;
        }

        public bool Run(bool usePSSnapin, string exchangeServer, string identity, string email = null, string alias = null, string onlineEmail = null, string dc = null, bool throwOnFailure = true)
        {
            var success = false;
            try
            {
                string script = System.IO.File.ReadAllText(powershellScript);
                if (!string.IsNullOrEmpty(script))
                {
                    using (PowerShell powershell = PowerShell.Create())
                    {
                        script = script + "\n\n" +
                            "$ppArgSnapIn=" + ((usePSSnapin) ? "$true" : "$false") + "\n" +
                            "$ppArgServer=\"" + exchangeServer + "\"\n" +
                            "$ppArg1=\"" + identity + "\"\n" +
                            "$ppArg2=\"" + (email == null ? "" : email) + "\"\n" +
                            "$ppArg3=\"" + (alias == null ? "" : alias) + "\"\n" +
                            "$ppArg4=\"" + (onlineEmail == null ? "" : onlineEmail) + "\"\n" +
                            "$ppArg5=\"" + (dc == null ? "" : dc) + "\"\n";

                        script += "Invoke-Method -usePSSnapin $ppArgSnapin -server $ppArgServer -Identity $ppArg1";
                        if (!string.IsNullOrEmpty(email))
                        {
                            script += " -Email $ppArg2";
                        }

                        if (!string.IsNullOrEmpty(alias))
                        {
                            script += " -Alias $ppArg3";
                        }

                        if (!string.IsNullOrEmpty(onlineEmail))
                        {
                            script += " -OnlineEmail $ppArg4";
                        }

                        if (!string.IsNullOrEmpty(dc))
                        {
                            script += " -DC $ppArg5";
                        }

                        script += "\n";

                        powershell.AddScript(script);

                        var result = powershell.Invoke();

                        if (result.Count == 0)
                        {
                            Exception ex = null;
                            if (powershell.Streams.Error.Count > 0)
                            {
                                ex = powershell.Streams.Error[0].Exception;
                            }
                            throw new Exception("Did not get a response from executing powershell " + powershellScript + " with arguments: identity=" + identity + ", email=" + email + ", alias=" + alias + ", onlineEmail=" + onlineEmail, ex);
                        }

                        var msg = result[result.Count-1].ToString();
                        success = "true".Equals(msg);
                        if (!success)
                        {
                            throw new Exception("Powershell executation indicated failed operation for " + powershellScript + " with arguments: identity=" + identity + ", email=" + email + ", alias=" + alias + ", onlineEmail=" + onlineEmail + ". With message = " + msg);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (throwOnFailure)
                {
                    throw e;
                }                
            }
            return success;
        }
    }
}