function Invoke-Method {
	param(
        [string] $SAMAccountName = $(throw "Please specify a sAMAccountName."),
        [string] $Name = $(throw "Please specify a name."),
		[string] $Uuid = $(throw "Please specify a uuid."),
		[string] $DC = $(throw "Please specify a DC")
	)
	
	$result = "Deleting " + $SAMAccountName + ", " + $Name + ", " + $Uuid + ", " + $DC

	$result | Out-File 'c:\logs\log.txt'
}
