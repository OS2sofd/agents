param(    [parameter(Position=0,Mandatory=$true)][string]$LogFile,    [parameter(Position=1,Mandatory=$false)][int]$MaxLogLines=10000  )
# InitializeIf( -not (Test-Path -Path $LogFile -PathType Leaf) ){    New-Item -Path $LogFile -Force}
function LogInfo {    param(        [string] $Message    )    (get-date -format "yyyy-MM-dd HH:mm:ss ") + "INFO $Message" >> $LogFile}
function LogError {    param(        [string] $Message    )    (get-date -format "yyyy-MM-dd HH:mm:ss ") + "ERROR $Message" >> $LogFile}
function LogWarning {    param(        [string] $Message    )    (get-date -format "yyyy-MM-dd HH:mm:ss ") + "WARNING $Message" >> $LogFile}
function LogException {
    param(
        [system.object] $ErrorRecord,
        [string] $Message
    )
    LogError "Line $($ErrorRecord.InvocationInfo.ScriptLineNumber). $($ErrorRecord.Exception.Message). $Message"
}
function ShrinkLog {
    $Tail = Get-content -Tail $MaxLogLines -Path $LogFile
    $Tail > $LogFile 
}