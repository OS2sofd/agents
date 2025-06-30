$ScriptName = "disableUser.ps1"
$LogDir = "c:/logs/sofd core powershell"
$LogFile = "$($LogDir)/$($ScriptName).log"
$MaxLogLines = 10000

[System.IO.Directory]::CreateDirectory($LogDir) | Out-Null
function Log {
    param(
        [string] $Message,
        [system.object] $ErrorRecord
    )
    $Output = get-date -format "yyyy-MM-dd HH:mm:ss "
    if( $null -ne $ErrorRecord )
    {
        $Output += "ERROR $($Message). Line $($ErrorRecord.InvocationInfo.ScriptLineNumber). $($ErrorRecord.Exception.Message)"
    }
    else
    {
        $Output += "INFO $($Message)"
    }
    $Output >> $LogFile
}

function ShrinkLog {
    $Tail = Get-content -Tail $MaxLogLines -Path $LogFile
    $Tail > $LogFile 
}


function Invoke-Method {
	param(
        [string] $SAMAccountName = $(throw "Please specify a sAMAccountName."),
        [string] $Name = $(throw "Please specify a name."),
		[string] $Uuid = $(throw "Please specify a uuid."),
		[string] $DC = $(throw "Please specify a DC")
	)
    Log "Executing $($ScriptName)"
    Log "Parameters: $($SAMAccountName), $($Name), $($Uuid), $($DC)"
	
    try
    {        
        # *** begin custom logic ***
        Log "Custom changes here"
        # *** end custom logic ***
    }
    catch
    {
        Log "Invoke-Method failed" $_
    }

    Log "Finished executing $($ScriptName)"
    ShrinkLog
}