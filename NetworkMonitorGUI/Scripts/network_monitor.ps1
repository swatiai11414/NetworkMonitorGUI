# नेटवर्क इंटरफेस चुनें
$interface = Get-NetAdapter | Where-Object { $_.Status -eq "Up" } | Select-Object -First 1 -ExpandProperty Name
if (-not $interface) {
    Write-Host "❌ कोई active network interface नहीं मिला।"
    exit
}

# लॉग फाइल
$date = Get-Date -Format "yyyyMMdd"
$logFile = "$PSScriptRoot\network_log_$date.csv"
if (-not (Test-Path $logFile)) {
    "Timestamp,Speed(Mbps),ActiveProcesses" | Out-File -FilePath $logFile -Encoding UTF8
}

# वॉइस स्पीकर
Add-Type -AssemblyName System.Speech
$voice = New-Object System.Speech.Synthesis.SpeechSynthesizer

# Toast Notification COM Interface (Windows 10+)
function Show-Toast {
    param (
        [string]$title,
        [string]$message
    )
    try {
        [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
        $template = [Windows.UI.Notifications.ToastTemplateType]::ToastText02
        $xml = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent($template)
        $textNodes = $xml.GetElementsByTagName("text")
        $textNodes.Item(0).AppendChild($xml.CreateTextNode($title)) | Out-Null
        $textNodes.Item(1).AppendChild($xml.CreateTextNode($message)) | Out-Null
        $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
        $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("Network Monitor")
        $notifier.Show($toast)
    } catch {
        Write-Warning "⚠️ Toast Notification विफल: $_"
    }
}

# प्रारंभिक आँकड़े
$prevStats = Get-NetAdapterStatistics -Name $interface
$prevBytes = $prevStats.ReceivedBytes + $prevStats.SentBytes

Write-Host "📡 Monitoring started on: $interface"
Write-Host "📁 Logs: $logFile"
Write-Host "⏹ Press Ctrl+C to stop."

while ($true) {
    Start-Sleep -Seconds 10

    $stats = Get-NetAdapterStatistics -Name $interface
    $currBytes = $stats.ReceivedBytes + $stats.SentBytes
    $diffBytes = $currBytes - $prevBytes
    $bps = $diffBytes / 10
    $mbps = [math]::Round(($bps * 8) / 1MB, 2)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

    # Netstat से PID निकालें
    $pids = @()
    foreach ($line in netstat -ano) {
        if ($line -match '\s+(\d+)$') {
            $pnum = $matches[1]
            if ($pnum -ne '' -and -not $pids.Contains($pnum)) {
                $pids += $pnum
            }
        }
    }

    # PID से प्रोसेस नाम प्राप्त करें
    $procNames = @()
    foreach ($p in $pids) {
        try {
            $proc = Get-Process -Id $p -ErrorAction Stop
            if (-not $procNames.Contains($proc.ProcessName)) {
                $procNames += $proc.ProcessName
            }
        } catch {}
    }

    $processList = if ($procNames.Count -gt 0) { $procNames -join ";" } else { "No Active Process" }
    $logLine = "$timestamp,$mbps,$processList"

    # सुरक्षित रूप से फाइल में लिखें
    $maxRetries = 3
    for ($i = 0; $i -lt $maxRetries; $i++) {
        try {
            $stream = [System.IO.StreamWriter]::new($logFile, $true, [System.Text.Encoding]::UTF8)
            $stream.WriteLine($logLine)
            $stream.Close()
            break
        } catch {
            Start-Sleep -Milliseconds 200
            if ($i -eq $maxRetries - 1) {
                Write-Warning "⚠️ लॉग लिखने में समस्या: $_"
            }
        }
    }

    # अलर्ट: यदि स्पीड 1 Mbps से ज्यादा है
    if ($mbps -gt 1) {
        $alertText = "🚨 Alert! Speed: $mbps Mbps | Apps: $($procNames -join ', ')"
        Write-Host $alertText

        Show-Toast -title "Network Alert" -message "Speed $mbps Mbps"
        try {
            $voice.SpeakAsync("Network speed alert. Speed is $mbps megabits per second.") | Out-Null
        } catch {
            Write-Warning "⚠️ वॉइस अलर्ट विफल: $_"
        }
    }

    $prevBytes = $currBytes
}
