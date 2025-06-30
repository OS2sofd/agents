function Invoke-Method {
	param(
		[string] $DomainController = $(throw "Please specify DomainController."),
		[string] $Name = $(throw "please specify Name"),
        [string] $DistinguishedName = $(throw "Please specify DistinguishedName.")
	)
}