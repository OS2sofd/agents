using System.Collections.Generic;

namespace SOFDCoreAD.Service.ActiveDirectory
{
    public class PropertyResolver
    {
        public string CprProperty { get; set; }
        public string EmployeeIdProperty { get; set; }
        public string AffiliationProperty { get; set; }
        public string TitleProperty { get; set; }
        public string ChosenNameProperty { get; set; }
        public string FirstnameProperty { get; set; }
        public string SurnameProperty { get; set; }
        public string EmailProperty { get; set; }
        public string MobileProperty { get; set; }
        public string SecretMobileProperty { get; set; }
        public string PhoneProperty { get; set; }
        public string DepartmentNumberProperty { get; set; }
        public string FaxNumberProperty { get; set; }
        public string UserIdProperty { get; set; }
        public string DeletedProperty { get; set; }
        public string ObjectGuidProperty { get; set; }
        public string AccountExpireProperty { get; set; }
        public string UserAccountControlProperty { get; set; }
        public string PwdLastSetProperty { get; set; }       
        public string PhotoProperty { get; set; }
        public string LockoutTimeProperty { get; set; }
        public string UPNProperty { get; set; }

        public Dictionary<string,string> LocalExtentionProperties { get; set; }
        public string[] AllProperties { get; set; }

        public PropertyResolver()
        {
            EmployeeIdProperty = Settings.GetStringValue("ActiveDirectory.Property.EmployeeId");
            CprProperty = Settings.GetStringValue("ActiveDirectory.Property.Cpr");
            AffiliationProperty = Settings.GetStringValue("ActiveDirectory.Property.Affiliation");
            TitleProperty = "title";
            ChosenNameProperty = "displayname";
            FirstnameProperty = "givenname";
            SurnameProperty = "sn";
            EmailProperty = "mail";
            UserIdProperty = "samaccountname";
            AccountExpireProperty = "accountExpires";
            DeletedProperty = "isdeleted";
            ObjectGuidProperty = "objectguid";
            PwdLastSetProperty = "pwdlastset";
            UserAccountControlProperty = "useraccountcontrol";
            LockoutTimeProperty = "lockoutTime";
            UPNProperty = "userPrincipalName";
            MobileProperty = Settings.GetStringValue("ActiveDirectory.Property.Mobile");
            SecretMobileProperty = Settings.GetStringValue("ActiveDirectory.Property.SecretMobile");
            PhoneProperty = Settings.GetStringValue("ActiveDirectory.Property.Phone");
            DepartmentNumberProperty = Settings.GetStringValue("ActiveDirectory.Property.DepartmentNumber");
            FaxNumberProperty = Settings.GetStringValue("ActiveDirectory.Property.FaxNumber");
            PhotoProperty = Settings.GetStringValue("ActiveDirectory.Property.Photo");

            LocalExtentionProperties = Settings.GetStringValues("ActiveDirectory.LocalExtention.");

            var allProperties = new List<string>();
            allProperties.AddRange(
                new string[]
                            {
                                CprProperty
                                ,TitleProperty
                                ,ChosenNameProperty
                                ,FirstnameProperty
                                ,SurnameProperty
                                ,EmailProperty
                                ,UserIdProperty
                                ,DeletedProperty
                                ,ObjectGuidProperty
                                ,PwdLastSetProperty
                                ,AccountExpireProperty
                                ,LockoutTimeProperty
                                ,UserAccountControlProperty
                                ,UPNProperty
                            });

            if (AffiliationProperty != null)
            {
                allProperties.Add(AffiliationProperty);
            }

            if (EmployeeIdProperty != null)
            {
                allProperties.Add(EmployeeIdProperty);
            }

            if (MobileProperty != null)
            {
                allProperties.Add(MobileProperty);
            }

            if (SecretMobileProperty != null)
            {
                allProperties.Add(SecretMobileProperty);
            }

            if (PhoneProperty != null)
            {
                allProperties.Add(PhoneProperty);
            }

            if (DepartmentNumberProperty != null)
            {
                allProperties.Add(DepartmentNumberProperty);
            }

            if (FaxNumberProperty != null)
            {
                allProperties.Add(FaxNumberProperty);
            }

            if (PhotoProperty != null)
            {
                allProperties.Add(PhotoProperty);
            }

            allProperties.AddRange(LocalExtentionProperties.Values);
            AllProperties = allProperties.ToArray();
        }
    }
}
