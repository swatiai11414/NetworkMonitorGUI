# Sample minimal network monitor script - customize as needed

Write-Host "Network monitoring started..."

while ($true) {
    # Example: Display current time every 5 seconds
    $time = Get-Date -Format "HH:mm:ss"
    Write-Host "[$time] Monitoring network..."

    # Sleep for 5 seconds
    Start-Sleep -Seconds 5
}
