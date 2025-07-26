$logFolder = "$PSScriptRoot\logs"
$daysToKeep = 7
if (Test-Path $logFolder) {
    Get-ChildItem $logFolder -File | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$daysToKeep) } | Remove-Item -Force
    Write-Host "Purged logs older than $daysToKeep days."
} else {
    Write-Host "No log folder found."
}
