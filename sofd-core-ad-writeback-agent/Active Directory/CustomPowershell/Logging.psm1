﻿param(
# Initialize
function LogInfo {
function LogError {
function LogWarning {
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