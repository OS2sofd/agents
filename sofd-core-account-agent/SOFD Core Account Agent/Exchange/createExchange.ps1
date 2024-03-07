function Invoke-Method {
	param(
        [string] $SAMAccountName = $(throw "Please specify a sAMAccountName."),
        [string] $Name = $(throw "Please specify a name."),
		[string] $Uuid = $(throw "Please specify a uuid."),
		[string] $EmailAlias = $(throw "Please specify an EmailAlias."),
		[string] $DC = $(throw "Please specify a DC"),
	)
	
	$result = "Creating " + $SAMAccountName + ", " + $Name + ", " + $Uuid + ", " + $EmailAlias;

	$result | Out-File 'c:\logs\log.txt'
}
