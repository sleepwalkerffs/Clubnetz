<#
.SYNOPSIS
    Takes screenshots of a running Clubnetz instance with headless Microsoft Edge.

.DESCRIPTION
    Drives Edge over the DevTools protocol: logs in through the API, sets the browser
    language, the light or dark theme and the viewport, opens each page and saves a PNG
    at twice the resolution. Meant for the pseudonymized database of
    New-ScreenshotDatabase.ps1; never point it at real data you don't want published.

    Each shot is a hashtable: @{ Name = "booking"; Path = "/clubs/1/book"; Email = "..."; Width = 390; Height = 844; Mobile = $true }
    Optional keys: Wait (seconds, default 5), Theme ("light" | "dark"), Script (JavaScript run before the capture).
#>
param(
    [Parameter(Mandatory)] [hashtable[]] $Shots,
    [Parameter(Mandatory)] [string] $Password,
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [ValidateSet("de", "en")] [string] $Language = "en",
    [ValidateSet("light", "dark")] [string] $Theme = "light",
    [string] $BaseUrl = "http://localhost:5082",
    [string] $Edge = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    [int] $DebugPort = 9333
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

$locale = if ($Language -eq "de") { "de-AT" } else { "en-GB" }
$profileDir = Join-Path ([IO.Path]::GetTempPath()) "clubnetz-screens-$([guid]::NewGuid())"
$browser = Start-Process $Edge -PassThru -ArgumentList @(
    "--headless=new", "--remote-debugging-port=$DebugPort", "--user-data-dir=$profileDir",
    "--lang=$locale", "--accept-lang=$locale", "--hide-scrollbars", "--no-first-run", "about:blank")

$script:socket = $null
$script:nextId = 0

function Send-Cdp([string] $Method, [hashtable] $Params = @{}) {
    $id = ++$script:nextId
    $json = @{ id = $id; method = $Method; params = $Params } | ConvertTo-Json -Depth 10 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $script:socket.SendAsync([ArraySegment[byte]]::new($bytes), "Text", $true, [Threading.CancellationToken]::None).Wait()
    while ($true) {
        $stream = [IO.MemoryStream]::new()
        $buffer = [byte[]]::new(1MB)
        do {
            $result = $script:socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), [Threading.CancellationToken]::None).Result
            $stream.Write($buffer, 0, $result.Count)
        } while (-not $result.EndOfMessage)
        $message = [Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json -Depth 20
        if ($message.id -eq $id) {
            if ($message.error) { throw "$Method failed: $($message.error.message)" }
            return $message.result
        }
    }
}

function Invoke-Js([string] $Expression) {
    $r = Send-Cdp "Runtime.evaluate" @{ expression = $Expression; awaitPromise = $true; returnByValue = $true }
    if ($r.exceptionDetails) { throw "JavaScript failed: $($r.exceptionDetails.text) $($r.exceptionDetails.exception.description)" }
    return $r.result.value
}

try {
    $target = $null
    for ($i = 0; $i -lt 50 -and -not $target; $i++) {
        try { $target = (Invoke-RestMethod "http://127.0.0.1:$DebugPort/json") | Where-Object type -eq "page" | Select-Object -First 1 }
        catch { Start-Sleep -Milliseconds 200 }
    }
    if (-not $target) { throw "Edge did not start." }

    $script:socket = [Net.WebSockets.ClientWebSocket]::new()
    $script:socket.ConnectAsync([Uri] $target.webSocketDebuggerUrl, [Threading.CancellationToken]::None).Wait()
    Send-Cdp "Page.enable" | Out-Null
    Send-Cdp "Emulation.setLocaleOverride" @{ locale = $locale } | Out-Null
    # Blazor picks its culture from navigator.languages, which only this override changes.
    $userAgent = (Send-Cdp "Browser.getVersion").userAgent -replace "HeadlessChrome", "Chrome"
    Send-Cdp "Emulation.setUserAgentOverride" @{ userAgent = $userAgent; acceptLanguage = $locale } | Out-Null
    Send-Cdp "Emulation.setTimezoneOverride" @{ timezoneId = "Europe/Vienna" } | Out-Null

    $currentEmail = $null
    foreach ($shot in $Shots) {
        $width = $shot.Width ?? 1280
        $height = $shot.Height ?? 800
        $mobile = [bool] $shot.Mobile
        Send-Cdp "Emulation.setDeviceMetricsOverride" @{ width = $width; height = $height; deviceScaleFactor = 2; mobile = $mobile } | Out-Null
        Send-Cdp "Emulation.setTouchEmulationEnabled" @{ enabled = $mobile } | Out-Null

        if ($shot.Email -ne $currentEmail) {
            Send-Cdp "Page.navigate" @{ url = "$BaseUrl/account/login" } | Out-Null
            Start-Sleep -Seconds 3
            Invoke-Js "fetch('/api/Account/Logout', { method: 'POST' }).then(r => r.status)" | Out-Null
            $body = @{ email = $shot.Email; password = $Password; rememberMe = $true } | ConvertTo-Json -Compress
            # Retried, because the first requests after rebuilding the database can hit connections it closed.
            for ($attempt = 0; $attempt -lt 3; $attempt++) {
                $status = Invoke-Js "fetch('/api/Account/Login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify($body) }).then(r => r.status)"
                if ($status -eq 200) { break }
                Start-Sleep -Seconds 2
            }
            if ($status -ne 200) { throw "Login as $($shot.Email) failed with HTTP $status." }
            $currentEmail = $shot.Email
        }

        $shotTheme = $shot.Theme ?? $Theme
        Invoke-Js "localStorage.setItem('bookennis-theme', '$shotTheme'); true" | Out-Null

        Send-Cdp "Page.navigate" @{ url = "$BaseUrl$($shot.Path)" } | Out-Null
        Start-Sleep -Seconds ($shot.Wait ?? 6)
        if ($shot.Script) {
            Invoke-Js $shot.Script | Out-Null
            Start-Sleep -Seconds 2
        }

        $image = Send-Cdp "Page.captureScreenshot" @{ format = "webp"; quality = 82 }
        $file = Join-Path $OutputDirectory "$($shot.Name).webp"
        [IO.File]::WriteAllBytes($file, [Convert]::FromBase64String($image.data))
        Write-Host "Saved $file"
    }
}
finally {
    if ($script:socket) { $script:socket.Dispose() }
    taskkill /PID $browser.Id /T /F 2>&1 | Out-Null
    $global:LASTEXITCODE = 0
    Start-Sleep -Seconds 1
    Remove-Item -Recurse -Force $profileDir -ErrorAction SilentlyContinue
}
