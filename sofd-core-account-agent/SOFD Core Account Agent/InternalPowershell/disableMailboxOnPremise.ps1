# We need to set this globally to make sure exceptions are thrown when remoting
$Global:ErrorActionPreference = 'Stop'

function Invoke-Method {
	param(
		[bool] $usePSSnapin = $(throw "Please specify usePSSnapin."),
		[string] $server = $(throw "Please specify server."),
        [string] $identity = $(throw "Please specify an identity.")
	)
	
	$session = $null
	try {
		if ($usePSSnapin) {
			add-PSSnapin Microsoft.Exchange.Management.powershell.snapin
		}
		else {
			$remoteServer = "http://" + $server + "/powershell"

			$session = New-PSSession -ConfigurationName Microsoft.Exchange -ConnectionUri $remoteServer
			Import-PSSession $session | Out-Null
		}

        Disable-Mailbox -Identity $identity -Confirm:$false -ErrorAction Stop

        # Clear this mail-address in AD
        Get-ADUser -Filter "Mail -eq '$($identity)'" | Set-ADUser -Clear Mail

		return "true"
	}
	catch {
		return $_
	}
	finally {
		if (-Not $usePSSnapin) {
			Remove-PSSession $session
		}
	}
}
