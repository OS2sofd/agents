function Invoke-Method {
	param(
        [string] $SAMAccountName = $(throw "Please specify a sAMAccountName."),
        [string] $Name = $(throw "Please specify a name."),
		[string] $Uuid = $(throw "Please specify a uuid."),
		[string] $EmailAlias = $(throw "Please specify an EmailAlias.")
	)
	
	$result = "Deactivating " + $SAMAccountName + ", " + $Name + ", " + $Uuid + ", " + $EmailAlias;

	$result | Out-File 'c:\logs\log.txt'
}
