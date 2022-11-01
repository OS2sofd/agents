using System;
using Serilog;

namespace Active_Directory
{
    public class ActiveDirectoryAttributeService : ActiveDirectoryServiceBase
    {
        public ActiveDirectoryAttributeService(ActiveDirectoryConfig config, ILogger log) : base(config, log)
        {

        }

        public string SetUserPrincipleNameAndNickname(string sAMAccountName, string emailAlias, Boolean updateUserPrincipalName)
        {
            string nickname = (emailAlias.IndexOf('@') > 0) ? emailAlias.Substring(0, emailAlias.IndexOf('@')) : sAMAccountName;

            try
            {
                var wrapper = GetBySAMAccountName(sAMAccountName);
                using (var de = wrapper.Entry)
                {
                    if (updateUserPrincipalName) { 
                        de.Properties["userprincipalname"].Value = emailAlias;
                    }
                    de.Properties["mailnickname"].Value = nickname;
                    de.CommitChanges();
                }

                return wrapper.DC;
            }
            catch (Exception ex)
            {
                log.Error(ex, "Unable to set UserPrincipleName to " + emailAlias + " and mailNickName to " + nickname + " on " + sAMAccountName);
            }

            return "";
        }
    }
}
