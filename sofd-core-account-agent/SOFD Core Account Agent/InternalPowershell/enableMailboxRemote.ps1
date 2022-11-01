# We need to set this globally to make sure exceptions are thrown when remoting
$Global:ErrorActionPreference = 'Stop'

function Find-DomainController {
	param( 
		[string] $SAMAccountName
	)
	
	$Domain = [System.Directoryservices.Activedirectory.Domain]::GetCurrentDomain() 
	Foreach ($DomainController in $Domain.DomainControllers) {
		if ((Get-ADUser -Server $DomainController.Name -Filter "sAMAccountName -eq '$($SAMAccountName)'")) {
			Return $DomainController.Name
		}
	}
	Throw "The user $SAMAccountName was not found in any of the Domain Controllers";
}

function Invoke-Method {
	param(
		[bool] $usePSSnapin = $(throw "Please specify usePSSnapin."),
		[string] $server = $(throw "Please specify server."),
		[string] $identity = $(throw "Please specify an identity."),
		[string] $email = $(throw "Please specify an email."),
		[string] $alias = $(throw "Please specify an alias."),
		[string] $onlineEmail = $(throw "Please specify an onlineEmail.")
	)
	
	$session = $null
	try {
		if ($usePSSnapin) {
			add-PSSnapin Microsoft.Exchange.Management.powershell.snapin
		}
		else {
			$remoteServer = "http://" + $server + "/powershell"

			$session = New-PSSession -ConfigurationName Microsoft.Exchange -ConnectionUri $remoteServer
			Import-PSSession $session -AllowClobber | Out-Null
		}
		$DomainController = Find-DomainController $identity

		# disable the remote mailbox if usertype is already a RemoteUserMailbox
		# otherwise enabling it will fail
		$recipientType = Get-RemoteMailbox -DomainController $DomainController -Identity $identity -ErrorAction SilentlyContinue | Select-Object -ExpandProperty RecipientTypeDetails
		if( $recipientType -eq "RemoteUserMailbox" )
		{
			Disable-RemoteMailbox -Identity $identity -Confirm:$false
		}

		Enable-RemoteMailbox -DomainController $DomainController -Identity $identity -PrimarySmtpAddress $email -Alias $alias -RemoteRoutingAddress $onlineEmail -ErrorAction Stop
		Set-RemoteMailbox -DomainController $DomainController -Identity $identity -EmailAddressPolicyEnabled $true -ErrorAction Stop

		return "true"
	}
	catch {
		return $_
	}
	finally {
		if (-Not $usePSSnapin -and (-Not $null -eq $session)) {
			Remove-PSSession $session
		}
	}
}