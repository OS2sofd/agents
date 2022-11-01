# We need to set this globally to make sure exceptions are thrown when remoting
$Global:ErrorActionPreference = 'Stop'

function Invoke-Method {
	param(
		[bool] $usePSSnapin = $(throw "Please specify usePSSnapin."),
		[string] $server = $(throw "Please specify server."),
        [string] $identity = $(throw "Please specify identity.")
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

		Get-Mailbox -Identity $identity

		return "true";
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
