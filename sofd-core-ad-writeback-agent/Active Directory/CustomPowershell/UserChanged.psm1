<#  
	This script is used if ActiveDirectoryEnablePowershell setting is set to "True" in "SOFD Core AD Writeback Agent.exe.config"
	The "User-Changed" function is invoked whenever SOFD Core AD Writeback agent writes a user change to Active Directory.
	The $Person parameter is an object containing information about the person object that was changed (see sample data at the bottom of this file).
#>

# Custom log module exposes LogInfo, LogWarning, LogError and LogException functions
$LogFile = "c:/logs/SOFD Core AD Writeback Agent - Custom Powershell/UserChanged.psm1.log"
$MaxLogLines = 10000
Import-Module -Name "./CustomPowershell/Logging.psm1" -ArgumentList ($LogFile, $MaxLogLines) -Force

function User-Changed { 
	param(
        [object] $Person
	)
	Try {
		LogInfo("Executing User-Changed for user: $($Person.user.userId)")
		
		#TODO: Add custom logic for user changes here
		
		#sample output json structure of changed user to log-file
		LogInfo("Changed Person:`n$($Person | ConvertTo-Json -Depth 3)")

	}
	Catch {
		LogException($_)
	}
	ShrinkLog
}

<#
    Sample Person object:
    {
        "uuid":  "ae1ed24c-e7b6-4198-85f8-5c5cb834a7ef",
        "firstname":  "Anders",
        "surname":  "And",
        "chosenName":  "Anders And",
        "user":  {
                     "uuid":  "8bcc556b-24bb-4887-ac56-6f1387a30c11",
                     "master":  "ActiveDirectory",
                     "masterId":  "e0872819-7246-4f2e-94eb-b06c5a354183",
                     "userId":  "aa"
                 },
        "affiliation":  {
                            "uuid":  "f9ae23ea-e091-4e36-a2fd-6d3e124e9708",
                            "master":  "OPUS",
                            "employeeId":  "12345",
                            "positionName":  "Kuli",
                            "affiliationType":  "EMPLOYEE",
                            "orgunit":  {
                                            "uuid":  "7733dc4a-f2d1-44e7-9e90-db1243d5c4ee",
                                            "parentUuid":  "7e4f3470-a1e4-42ae-af89-9a09015ad971",
                                            "master":  "OPUS",
                                            "masterId":  "12345",
                                            "name":  "Andeby Kommune - Veje og Park"
                                        }
                        }
    }
#>