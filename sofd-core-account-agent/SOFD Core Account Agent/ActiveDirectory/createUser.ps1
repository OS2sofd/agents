function Invoke-Method {
	param(
        [string] $SAMAccountName = $(throw "Please specify a sAMAccountName."),
        [string] $Name = $(throw "Please specify a name."),
        [string] $Uuid = $(throw "Please specify a uuid."),
        [string] $DC = $(throw "Please specify a DC"),
		[Parameter(Mandatory=$false)][string] $optionalJson
	)


    # insert your own code here	
    $result = "Data " + $SAMAccountName + ", " + $optionalJson

	$result | Out-File 'c:\logs\log.txt'

}
