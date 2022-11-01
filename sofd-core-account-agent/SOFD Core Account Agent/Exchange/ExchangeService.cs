using Serilog;

namespace SOFD
{
    class ExchangeService
    {
        private ILogger logger;
        private string exchangeServer;
        private string onlineDomain;
        private bool remote;
        private bool usePSSnapin;
        
        private ExchangePowershellRunner disableMailboxOnPremiseRunner = new ExchangePowershellRunner("InternalPowershell\\disableMailboxOnPremise.ps1");
        private ExchangePowershellRunner enableMailboxOnPremiseRunner = new ExchangePowershellRunner("InternalPowershell\\enableMailboxOnPremise.ps1");
        private ExchangePowershellRunner mailboxExistsOnPremiseRunner = new ExchangePowershellRunner("InternalPowershell\\mailboxExistsOnPremise.ps1");

        private ExchangePowershellRunner disableMailboxRemoteRunner = new ExchangePowershellRunner("InternalPowershell\\disableMailboxRemote.ps1");
        private ExchangePowershellRunner enableMailboxRemoteRunner = new ExchangePowershellRunner("InternalPowershell\\enableMailboxRemote.ps1");
        private ExchangePowershellRunner mailboxExistsRemoteRunner = new ExchangePowershellRunner("InternalPowershell\\mailboxExistsRemote.ps1");

        public ExchangeService(string exchangeServer, string onlineDomain, bool remote, bool usePSSnapin, ILogger logger)
        {
            this.exchangeServer = exchangeServer;
            this.remote = remote;
            this.onlineDomain = onlineDomain;
            this.logger = logger;
            this.usePSSnapin = usePSSnapin;
        }

        public void EnableMailbox(string identity, string email)
        {
            logger.Information("Enabling mailbox " + email + " for " + identity);

            if (remote)
            {
                string alias = "";
                string onlineEmail = "";

                int idx = email.IndexOf("@");
                if (idx >= 0)
                {
                    alias = email.Substring(0, idx);
                    onlineEmail = alias + onlineDomain;
                }
                enableMailboxRemoteRunner.Run(usePSSnapin, exchangeServer, identity, email, alias, onlineEmail);
            }
            else
            {
                enableMailboxOnPremiseRunner.Run(usePSSnapin, exchangeServer, identity, email);
            }
        }

        public void DisableMailbox(string identity)
        {
            logger.Information("Disabling mailbox " + identity);

            if (remote)
            {
                disableMailboxRemoteRunner.Run(usePSSnapin, exchangeServer, identity);
            }
            else
            {
                disableMailboxOnPremiseRunner.Run(usePSSnapin, exchangeServer, identity);
            }
        }

        public bool MailboxExists(string identity)
        {
            logger.Information("Verifying existance of " + identity);

            if (remote)
            {
                return mailboxExistsRemoteRunner.Run(usePSSnapin, exchangeServer, identity,throwOnFailure: false);
            }
            else
            {
                return mailboxExistsOnPremiseRunner.Run(usePSSnapin, exchangeServer, identity, throwOnFailure: false);
            }
        }
    }
}
