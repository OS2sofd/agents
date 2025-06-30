function Invoke-Method {
	param(
		[string] $DomainController = $(throw "Please specify DomainController."),
		[string] $SamAccountName = $(throw "Please specify SamAccountName."),
        [string] $DistinguishedNameFrom = $(throw "Please specify DistinguishedNameFrom."),
        [string] $DistinguishedNameTo = $(throw "Please specify DistinguishedNameTo.")
	)
}