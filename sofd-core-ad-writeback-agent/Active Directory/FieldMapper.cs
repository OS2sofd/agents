using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;
using SOFD_Core.Model;

namespace Active_Directory
{
    class FieldMapper
    {
        public static string GetValue(string userId, string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            if (sofdField.StartsWith("concat("))
            {
                return GetValueConcat(userId, sofdField, person, affiliation, orgUnit);
            }
            if (sofdField.StartsWith("join("))
            {
                return GetValueJoin(userId, sofdField, person, affiliation, orgUnit);
            }
            if (sofdField.StartsWith("prefix("))
            {
                return GetValuePrefix(userId, sofdField, person, affiliation, orgUnit);
            }
            if (sofdField.StartsWith("right("))
            {
                return GetValueRight(userId, sofdField, person, affiliation, orgUnit);
            }
            if (sofdField.StartsWith("left("))
            {
                return GetValueLeft(userId, sofdField, person, affiliation, orgUnit);
            }
            if (sofdField.StartsWith("static("))
            {
                return GetValueStatic(sofdField);
            }
            if (sofdField.StartsWith("isnull("))
            {
                return GetValueIsNull(userId, sofdField, person, affiliation, orgUnit);
            }
            if (sofdField.StartsWith("pad("))
            {
                return GetValuePad(userId, sofdField, person, affiliation, orgUnit);
            }
            if (sofdField.StartsWith("cprformat("))
            {
                return GetValueCprFormat(userId, sofdField, person, affiliation, orgUnit);
            }
            if (sofdField.StartsWith("replace("))
            {
                return GetValueReplace(userId, sofdField, person, affiliation, orgUnit);
            }
            if (sofdField.StartsWith("trim("))
            {
                return GetValueTrim(userId, sofdField, person, affiliation, orgUnit);
            }

            string[] tokens = sofdField.Split('.');
            if (tokens.Length < 1)
            {
                throw new Exception("Invalid sofd field: " + sofdField);
            }

            switch (tokens[0])
            {
                case "opususer":
                    return MapOpusUser(sofdField, person);
                case "userid":
                    return userId;
                case "unilogin":
                    return MapUniLogin(person);
                case "affiliation":
                    return MapAffiliation(sofdField, person, affiliation, orgUnit);
                case "phone":
                    return MapPhone(sofdField, person);
                case "post":
                    return MapPost(sofdField, person);
                case "user":
                    return MapUser(userId, sofdField, person);
                case "authorizationCode":
                    return MapAuthorizationCode(sofdField, person);
                default:
                    return person.GetType().GetProperty(tokens[0])?.GetValue(person)?.ToString();
            }

            throw new Exception("Should never arrive here!");
        }

        private static string MapUniLogin(Person person)
        {
            foreach (var user in person.users)
            {
                if (user.userType.Equals("UNILOGIN"))
                {
                    return user.userId;
                }
            }

            return null;
        }

        private static string GetValueConcat(string userId, string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            // concat(xxx,xxxx) - strip first 7 chars and last, then trim
            var newSofdField = sofdField.Substring(7, sofdField.Length - 8).Trim();

            string[] fields = newSofdField.Split(',');
            if (fields.Length != 2)
            {
                throw new Exception("concat takes 2 arguments: " + sofdField);
            }
            var field1 = GetValue(userId, fields[0], person, affiliation, orgUnit);
            var field2 = GetValue(userId, fields[1], person, affiliation, orgUnit);

            return field1 + " (" + field2 + ")";
        }

        private static string GetValuePad(string userId, string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            // pad(x,xxx,xxxx) - strip first 4 chars and last, then trim
            var newSofdField = sofdField.Substring(4, sofdField.Length - 5).Trim();

            string[] fields = newSofdField.Split(',');
            if (fields.Length != 3)
            {
                throw new Exception("pad takes 3 arguments: " + sofdField);
            }

            var field = GetValue(userId, fields[2], person, affiliation, orgUnit);
            var maxLength = int.Parse(fields[0]);
            var padChar = char.Parse(fields[1]);

            if (field == null || field.Length >= maxLength)
            {
                return field;
            }

            string finalString = field;
            while (finalString.Length < maxLength)
            {
                finalString = padChar + finalString;
            }

            return finalString;
        }

        private static string GetValueJoin(string userId, string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            // join(xxx,xxxx) - strip first 5 chars and last, then trim
            var newSofdField = sofdField.Substring(5, sofdField.Length - 6).Trim();

            // split by commas except commas that are inside parentheses (e.g. other methods)
            string[] fields = Regex.Split(newSofdField, "(?<!\\([^\\)]*),");

            if (fields.Length < 2)
            {
                throw new Exception("join takes at least 2 arguments: " + sofdField);
            }

            var result = "";
            foreach (var field in fields)
            {
                var fieldValue = GetValue(userId, field, person, affiliation, orgUnit);
                // if any of the fields in the join are null, the entire join expression evaluates to null
                if (fieldValue == null)
                {
                    return null;
                }
                result += fieldValue;
            }

            return result;
        }

        private static string GetValueReplace(string userId, string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            // replace(xxx,xxxx) - strip first 8 chars and last, then trim
            var newSofdField = sofdField.Substring(8, sofdField.Length - 9).Trim();

            // split by commas except commas that are inside parentheses (e.g. other methods)
            string[] fields = Regex.Split(newSofdField, "(?<!\\([^\\)]*),");

            if (fields.Length != 3)
            {
                throw new Exception("replace takes 3 arguments: " + sofdField);
            }
            var fieldValue = GetValue(userId, fields[0], person, affiliation, orgUnit);
            if (fieldValue == null) {
                return null;
            }
            var search = fields[1].Replace("\\n", "\n");
            var replacement = fields[2];
            var result = fieldValue.Replace(search, replacement);
            return result;
        }

        private static string GetValueTrim(string userId, string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            // trim(xxx,xxxx) - strip first 5 chars and last, then trim
            var newSofdField = sofdField.Substring(5, sofdField.Length - 6).Trim();
            var fieldValue = GetValue(userId, newSofdField, person, affiliation, orgUnit);
            return fieldValue != null ? fieldValue.Trim() : null;
        }

        private static string GetValueIsNull(string userId, string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            // isnull(xxx,xxxx) - strip first 7 chars and last, then trim
            var newSofdField = sofdField.Substring(7, sofdField.Length - 8).Trim();

            // split by commas except commas that are inside square brackets (e.g. parameters to Tag)
            string[] fields = Regex.Split(newSofdField, "(?<!\\[[^\\]]*),");
            if (fields.Length != 2)
            {
                throw new Exception("isnull takes 2 arguments: " + sofdField);
            }
            var field1 = GetValue(userId, fields[0], person, affiliation, orgUnit);
            if (field1 != null)
            {
                return field1;
            }
            else
            {
                var field2 = GetValue(userId, fields[1], person, affiliation, orgUnit);
                return field2;
            }
        }


        private static string GetValuePrefix(string userId, string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            // prefix(xxx,xxxx) - strip first 7 chars and last, then trim
            var newSofdField = sofdField.Substring(7, sofdField.Length - 8).Trim();

            string[] fields = newSofdField.Split(',');
            if (fields.Length != 2)
            {
                throw new Exception("prefix takes 2 arguments: " + sofdField);
            }

            var prefix = fields[0];
            var field = GetValue(userId, fields[1], person, affiliation, orgUnit);

            if (field == null)
            {
                return null;
            }
            return field.StartsWith(prefix) ? field : String.Concat(prefix, field);
        }


        private static string GetValueRight(string userId, string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            // right(xxx,4) - return only the rightmost 4 characters of the value
            var newSofdField = sofdField.Substring(6, sofdField.Length - 7).Trim();
            string[] args = newSofdField.Split(',');
            if (args.Length != 2)
            {
                throw new Exception("right takes 2 arguments: " + sofdField);
            }
            var length = int.Parse(args[1]);
            var field = GetValue(userId, args[0], person, affiliation, orgUnit);

            if (field != null && field.Length > length)
            {
                field = field.Substring(field.Length - length, length);
            }
            return field;
        }

        private static string GetValueLeft(string userId, string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            // left(xxx,4) - return only the leftmost 4 characters of the value
            var newSofdField = sofdField.Substring(5, sofdField.Length - 6).Trim();
            string[] args = newSofdField.Split(',');
            if (args.Length != 2)
            {
                throw new Exception("left takes 2 arguments: " + sofdField);
            }
            var length = int.Parse(args[1]);
            var field = GetValue(userId, args[0], person, affiliation, orgUnit);

            if (field != null && field.Length > length)
            {
                field = field.Substring(0, length);
            }
            return field;
        }

        private static string GetValueCprFormat(string userId, string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            // cprformat(xxx,xxxx) - strip first 4 chars and last, then trim
            var newSofdField = sofdField.Substring(10, sofdField.Length - 11).Trim();
            var input = GetValue(userId, newSofdField, person, affiliation, orgUnit);

            var result = Regex.Replace(input, "^(.{6})(.{4})", "$1-$2", RegexOptions.IgnoreCase);
            return result;
        }


        private static string GetValueStatic(string sofdField)
        {
            var value = sofdField.Substring(7, sofdField.Length - 8);
            return value;
        }

        private static string MapUser(string userId, string sofdField, Person person)
        {
            if (userId == null)
            {
                return null;
            }

            string[] tokens = sofdField.Split('.');
            if (tokens.Length < 2)
            {
                throw new Exception("Invalid sofd field: " + sofdField);
            }

            foreach (User user in person.users)
            {
                if (string.Equals(user.userType, "ACTIVE_DIRECTORY") && string.Equals(userId.ToLower(), user.userId.ToLower()))
                {
                    return user.GetType().GetProperty(tokens[1])?.GetValue(user)?.ToString();
                }
            }

            return null;
        }

        private static string MapPost(string sofdField, Person person)
        {
            string[] tokens = sofdField.Split('.');
            if (tokens.Length < 2)
            {
                throw new Exception("Invalid sofd field: " + sofdField);
            }

            if (person.registeredPostAddress != null)
            {
                return person.registeredPostAddress.GetType().GetProperty(tokens[1])?.GetValue(person.registeredPostAddress)?.ToString();
            }
            else if (person.residencePostAddress != null)
            {
                return person.residencePostAddress.GetType().GetProperty(tokens[1])?.GetValue(person.residencePostAddress)?.ToString();
            }

            return null;
        }

        private static string MapPost(string sofdField, OrgUnit orgUnit)
        {
            string[] tokens = sofdField.Split('.');
            if (tokens.Length < 4)
            {
                throw new Exception("Invalid sofd field: " + sofdField);
            }

            if (orgUnit.postAddresses != null && orgUnit.postAddresses.Count > 0)
            {
                foreach (Post post in orgUnit.postAddresses)
                {
                    if (post.prime)
                    {
                        return post.GetType().GetProperty(tokens[3])?.GetValue(post)?.ToString();
                    }
                }
            }

            return null;
        }

        private static OrgUnit GetOrgUnitWithTag(OrgUnit orgUnit, string tag, bool inherit)
        {
            if (orgUnit == null)
            {
                return null;
            }
            if (orgUnit.tags.Exists(t => t.tag == tag))
            {
                return orgUnit;
            }
            else if (inherit)
            {
                return GetOrgUnitWithTag(orgUnit.parent, tag, inherit);
            }
            return null;
        }

        private static string MapTag(string sofdField, string tagToken, OrgUnit orgUnit)
        {
            var match = Regex.Match(tagToken, @"^tag\[(?<tag>[^,].*?),(?<inherit>[^,].*?),(?<defaultValue>[^,].*?)\]$");
            if (!match.Success)
            {
                throw new Exception("Invalid sofd field: " + sofdField);
            }

            var tag = match.Groups["tag"].Value;
            var inherit = Boolean.Parse(match.Groups["inherit"].Value);
            var defaultValue = match.Groups["defaultValue"].Value;

            var orgUnitWithTag = GetOrgUnitWithTag(orgUnit,tag,inherit);
            if (orgUnitWithTag != null)
            {
                var orgUnitTag = orgUnitWithTag.tags.First(t => t.tag == tag);
                if (!String.IsNullOrEmpty(orgUnitTag.customValue))
                {
                    return orgUnitTag.customValue;
                }
                if (defaultValue != "null")
                {
                    return orgUnit?.GetType().GetProperty(defaultValue)?.GetValue(orgUnitWithTag)?.ToString();
                }
            }
            return null;
        }


        private static string MapAuthorizationCode(string sofdField, Person person)
        {
            string[] tokens = sofdField.Split('.');
            if (tokens.Length < 2)
            {
                throw new Exception("Invalid sofd field: " + sofdField);
            }
            var property = tokens[1];

            if (person.authorizationCodes != null)
            {
                var authorizationCode = person.authorizationCodes.Where(a => a.prime).FirstOrDefault();
                return authorizationCode?.GetType().GetProperty(property)?.GetValue(authorizationCode)?.ToString();
            }
            return null;
        }


        private static string MapPhone(string sofdField, Person person)
        {
            string[] tokens = sofdField.Split('.');
            if (tokens.Length < 3)
            {
                throw new Exception("Invalid sofd field: " + sofdField);
            }
            var phoneType = tokens[1];
            var phoneProperty = tokens[2];

            if (person.phones != null)
            {
                var phone = person.phones.Where(p => p.phoneType.Equals(phoneType, StringComparison.InvariantCultureIgnoreCase)).OrderByDescending(p => p.typePrime).FirstOrDefault();
                if (phone != null)
                {
                    return phone.GetType().GetProperty(phoneProperty)?.GetValue(phone)?.ToString();
                }
            }
            return null;
        }

        private static string MapOrgUnitPhone(string sofdField, OrgUnit orgUnit)
        {
            string[] tokens = sofdField.Split('.');
            if (tokens.Length == 5) // phoneType specified
            {
                var phoneType = tokens[3];
                var phoneProperty = tokens[4];
                if (orgUnit.phones != null)
                {
                    var phone = orgUnit.phones.Where(p => p.phoneType.Equals(phoneType, StringComparison.InvariantCultureIgnoreCase)).OrderByDescending(p => p.typePrime).FirstOrDefault();
                    if (phone != null)
                    {
                        return phone.GetType().GetProperty(phoneProperty)?.GetValue(phone)?.ToString();
                    }
                }
            }
            else if (tokens.Length == 4) // phoneType not specified
            {
                var phoneProperty = tokens[3];
                var phone = orgUnit.phones.Where(p => p.prime).FirstOrDefault();
                if (phone != null)
                {
                    return phone.GetType().GetProperty(phoneProperty)?.GetValue(phone)?.ToString();
                }
            }
            else
            {
                throw new Exception("Invalid sofd field: " + sofdField);
            }
            return null;
        }


        private static string MapAffiliation(string sofdField, Person person, Affiliation affiliation, OrgUnit orgUnit)
        {
            // sanity check
            if (affiliation == null)
            {
                return null;
            }

            string[] tokens = sofdField.Split('.');
            if (tokens.Length < 2)
            {
                throw new Exception("Invalid sofd field: " + sofdField);
            }

            switch (tokens[1])
            {
                case "orgUnit":
                    return MapOrgUnit(sofdField, orgUnit);
                default:
                    return affiliation.GetType().GetProperty(tokens[1])?.GetValue(affiliation)?.ToString();
            }
        }

        private static string MapOrgUnit(string sofdField, OrgUnit orgUnit)
        {
            // sanity check
            if (orgUnit == null)
            {
                return null;
            }

            string[] tokens = sofdField.Split('.');
            if (tokens.Length < 3)
            {
                throw new Exception("Invalid sofd field: " + sofdField);
            }

            if ("post".Equals(tokens[2]))
            {
                return MapPost(sofdField, orgUnit);
            }
            else if ("manager".Equals(tokens[2]))
            {
                return orgUnit.manager?.name;
            }
            else if ("phone".Equals(tokens[2]))
            {
                return MapOrgUnitPhone(sofdField, orgUnit);
            }
            else if (tokens[2].StartsWith("tag["))
            {
                return MapTag(sofdField, tokens[2], orgUnit);
            }
            else if (tokens[2].StartsWith("_"))
            {
                if (tokens.Length < 4)
                {
                    throw new Exception("Invalid sofd field: " + sofdField);
                }

                int level = int.Parse(tokens[2].Substring(1));
                List<OrgUnit> hierarchy = new List<OrgUnit>();

                OrgUnit parent = orgUnit;
                while (parent != null)
                {
                    hierarchy.Add(parent);
                    parent = parent.parent;
                }

                if (hierarchy.Count < level)
                {
                    return null;
                }

                OrgUnit parentOU = hierarchy[level];

                // remove first ^ part from sofdField
                var regex = new Regex("\\._\\d+");
                var newSofdField = regex.Replace(sofdField, "", 1);

                return MapOrgUnit(newSofdField, parentOU);
            }
            else if (tokens[2].StartsWith("^"))
            {
                if (tokens.Length < 4)
                {
                    throw new Exception("Invalid sofd field: " + sofdField);
                }

                int level = int.Parse(tokens[2].Substring(1));
                Stack<OrgUnit> stack = new Stack<OrgUnit>();

                OrgUnit parent = orgUnit;
                while (parent != null)
                {
                    stack.Push(parent);
                    parent = parent.parent;
                }

                OrgUnit parentOU = null;
                for (int i = 0; i < level && stack.Count > 0; i++)
                {
                    parentOU = stack.Pop();
                }

                // remove first ^ part from sofdField
                var regex = new Regex("\\.\\^\\d+");
                var newSofdField = regex.Replace(sofdField, "", 1);
                return MapOrgUnit(newSofdField, parentOU);
            }
            else if (tokens[2].StartsWith(">"))
            {
                if (tokens.Length < 4)
                {
                    throw new Exception("Invalid sofd field: " + sofdField);
                }

                int level = int.Parse(tokens[2].Substring(1));
                List<OrgUnit> hierarchy = new List<OrgUnit>();

                OrgUnit parent = orgUnit;
                while (parent != null)
                {
                    hierarchy.Insert(0, parent);
                    parent = parent.parent;
                }

                if (hierarchy.Count < level)
                {
                    return null;
                }
                else
                {
                    var parentOU = hierarchy[level - 1];
                    // remove first > part from sofdField
                    var regex = new Regex("\\.>\\d+");
                    var newSofdField = regex.Replace(sofdField, "", 1);
                    return MapOrgUnit(newSofdField, parentOU);
                }
            }
            else if ("name".Equals(tokens[2]))
            {
                return orgUnit?.GetDisplayName();
            }
            else if ("parent".Equals(tokens[2]))
            {
                if (tokens.Length < 4)
                {
                    throw new Exception("Invalid sofd field: " + sofdField);
                }
                var parent = orgUnit.parent ?? orgUnit;
                // remove first parent part from sofdField
                var regex = new Regex("\\.parent");
                var newSofdField = regex.Replace(sofdField, "", 1);
                return MapOrgUnit(newSofdField, parent);
            }


            return orgUnit?.GetType().GetProperty(tokens[2])?.GetValue(orgUnit)?.ToString();
        }

        private static string MapOpusUser(string sofdField, Person person)
        {
            string[] tokens = sofdField.Split('.');
            if (tokens.Length < 2)
            {
                throw new Exception("Invalid sofd field: " + sofdField);
            }

            if (person.users != null && person.users.Count > 0)
            {
                User user = null;

                foreach (User u in person.users)
                {
                    if (u.prime && u.userType.Equals("OPUS"))
                    {
                        user = u;
                    }
                }

                if (user != null)
                {
                    return user.GetType().GetProperty(tokens[1])?.GetValue(user)?.ToString();
                }
            }

            return null;
        }
    }
}
