function Invoke-Method {
	param(
		[string] $DomainController = $(throw "Please specify DomainController."),
        [string] $DistinguishedNameFrom = $(throw "Please specify DistinguishedNameFrom."),
        [string] $DistinguishedNameTo = $(throw "Please specify DistinguishedNameTo.")
	)
}