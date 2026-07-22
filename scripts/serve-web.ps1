$ErrorActionPreference = "Stop"

$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDirectory
$DevelopmentBuildDirectory = Join-Path $ProjectRoot "Builds\WebGL"
$PosterGenerationCounts = @{}
$MaximumPostersPerSession = 7

# In a distributed WebGL ZIP this script sits beside index.html. In a project
# checkout it sits under scripts/, so fall back to Builds/WebGL.
$BuildDirectory = if (Test-Path -LiteralPath (Join-Path $ScriptDirectory "index.html")) {
    $ScriptDirectory
} else {
    $DevelopmentBuildDirectory
}
$IndexPath = Join-Path $BuildDirectory "index.html"

function Import-DotEnv {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    foreach ($RawLine in [IO.File]::ReadAllLines($Path)) {
        $Line = $RawLine.Trim()
        if (-not $Line -or $Line.StartsWith("#") -or -not $Line.Contains("=")) {
            continue
        }
        if ($Line.StartsWith("export ")) {
            $Line = $Line.Substring(7).TrimStart()
        }
        $Separator = $Line.IndexOf("=")
        if ($Separator -le 0) {
            continue
        }
        $Key = $Line.Substring(0, $Separator).Trim()
        $Value = $Line.Substring($Separator + 1).Trim().Trim("'`"")
        if (-not [Environment]::GetEnvironmentVariable($Key, "Process")) {
            [Environment]::SetEnvironmentVariable($Key, $Value, "Process")
        }
    }
    return $true
}

$LoadedEnvPath = $null
foreach ($EnvCandidate in @(
    (Join-Path $BuildDirectory ".env"),
    (Join-Path $ScriptDirectory ".env"),
    (Join-Path $ProjectRoot ".env")
)) {
    if (Import-DotEnv $EnvCandidate) {
        $LoadedEnvPath = $EnvCandidate
        break
    }
}

$Port = 8081
if ($env:PORT) {
    $ParsedPort = 0
    if (-not [int]::TryParse($env:PORT, [ref]$ParsedPort) -or
        $ParsedPort -lt 1 -or
        $ParsedPort -gt 65535) {
        Write-Error "PORT must be a whole number from 1 through 65535."
        exit 1
    }
    $Port = $ParsedPort
}

$Url = "http://localhost:$Port/"

if (-not (Test-Path -LiteralPath $IndexPath -PathType Leaf)) {
    Write-Error @"
No WebGL export was found at $BuildDirectory.
Place this launcher beside the exported index.html file, or build the project first.
"@
    exit 1
}

function Get-ContentType {
    param([string]$Path)

    $LogicalPath = $Path
    if ($LogicalPath.EndsWith(".gz", [StringComparison]::OrdinalIgnoreCase) -or
        $LogicalPath.EndsWith(".br", [StringComparison]::OrdinalIgnoreCase)) {
        $LogicalPath = $LogicalPath.Substring(0, $LogicalPath.Length - 3)
    }

    switch ([IO.Path]::GetExtension($LogicalPath).ToLowerInvariant()) {
        ".html" { return "text/html; charset=utf-8" }
        ".htm"  { return "text/html; charset=utf-8" }
        ".js"   { return "application/javascript; charset=utf-8" }
        ".css"  { return "text/css; charset=utf-8" }
        ".json" { return "application/json; charset=utf-8" }
        ".wasm" { return "application/wasm" }
        ".data" { return "application/octet-stream" }
        ".png"  { return "image/png" }
        ".jpg"  { return "image/jpeg" }
        ".jpeg" { return "image/jpeg" }
        ".gif"  { return "image/gif" }
        ".svg"  { return "image/svg+xml" }
        ".ico"  { return "image/x-icon" }
        ".txt"  { return "text/plain; charset=utf-8" }
        ".xml"  { return "application/xml; charset=utf-8" }
        ".mp3"  { return "audio/mpeg" }
        ".mp4"  { return "video/mp4" }
        ".wav"  { return "audio/wav" }
        default { return "application/octet-stream" }
    }
}

function Write-Headers {
    param(
        [IO.Stream]$Stream,
        [int]$StatusCode,
        [string]$Reason,
        [string]$ContentType,
        [long]$ContentLength,
        [string]$ContentEncoding = ""
    )

    $EncodingHeader = if ($ContentEncoding) {
        "Content-Encoding: $ContentEncoding`r`n"
    } else {
        ""
    }

    $HeaderText =
        "HTTP/1.1 $StatusCode $Reason`r`n" +
        "Content-Type: $ContentType`r`n" +
        "Content-Length: $ContentLength`r`n" +
        $EncodingHeader +
        "Cache-Control: no-cache`r`n" +
        "Cross-Origin-Opener-Policy: same-origin`r`n" +
        "Cross-Origin-Embedder-Policy: require-corp`r`n" +
        "Cross-Origin-Resource-Policy: same-origin`r`n" +
        "Connection: close`r`n`r`n"

    $HeaderBytes = [Text.Encoding]::ASCII.GetBytes($HeaderText)
    $Stream.Write($HeaderBytes, 0, $HeaderBytes.Length)
}

function Write-ErrorResponse {
    param(
        [IO.Stream]$Stream,
        [string]$Method,
        [int]$StatusCode,
        [string]$Reason
    )

    $BodyBytes = [Text.Encoding]::UTF8.GetBytes("$StatusCode $Reason")
    Write-Headers $Stream $StatusCode $Reason "text/plain; charset=utf-8" $BodyBytes.Length
    if ($Method -ne "HEAD") {
        $Stream.Write($BodyBytes, 0, $BodyBytes.Length)
    }
}

function Write-JsonResponse {
    param(
        [IO.Stream]$Stream,
        [int]$StatusCode,
        [string]$Reason,
        [hashtable]$Payload
    )

    $Body = ConvertTo-Json $Payload -Compress
    $BodyBytes = [Text.Encoding]::UTF8.GetBytes($Body)
    Write-Headers $Stream $StatusCode $Reason "application/json; charset=utf-8" $BodyBytes.Length
    $Stream.Write($BodyBytes, 0, $BodyBytes.Length)
}

function Read-HttpRequest {
    param([IO.Stream]$Stream)

    $HeaderBytes = New-Object "System.Collections.Generic.List[byte]"
    $Matched = 0
    $Delimiter = [byte[]](13, 10, 13, 10)

    while ($HeaderBytes.Count -lt 65536) {
        $Byte = $Stream.ReadByte()
        if ($Byte -lt 0) {
            break
        }
        $HeaderBytes.Add([byte]$Byte)
        if ($Byte -eq $Delimiter[$Matched]) {
            $Matched++
            if ($Matched -eq $Delimiter.Length) {
                break
            }
        } else {
            $Matched = if ($Byte -eq 13) { 1 } else { 0 }
        }
    }

    if ($Matched -ne $Delimiter.Length) {
        throw "Incomplete HTTP request headers."
    }

    $HeaderText = [Text.Encoding]::ASCII.GetString($HeaderBytes.ToArray())
    $Lines = $HeaderText -split "`r`n"
    $RequestParts = $Lines[0].Split(" ")
    if ($RequestParts.Length -lt 2) {
        throw "Invalid HTTP request line."
    }

    $Headers = @{}
    for ($Index = 1; $Index -lt $Lines.Length; $Index++) {
        $Line = $Lines[$Index]
        if (-not $Line) {
            continue
        }
        $Separator = $Line.IndexOf(":")
        if ($Separator -le 0) {
            continue
        }
        $Headers[$Line.Substring(0, $Separator).Trim().ToLowerInvariant()] =
            $Line.Substring($Separator + 1).Trim()
    }

    $ContentLength = 0
    if ($Headers.ContainsKey("content-length")) {
        [int]::TryParse($Headers["content-length"], [ref]$ContentLength) | Out-Null
    }
    if ($ContentLength -lt 0 -or $ContentLength -gt 65536) {
        throw "HTTP request body is too large."
    }

    $BodyBytes = New-Object byte[] $ContentLength
    $Offset = 0
    while ($Offset -lt $ContentLength) {
        $Read = $Stream.Read($BodyBytes, $Offset, $ContentLength - $Offset)
        if ($Read -le 0) {
            throw "Incomplete HTTP request body."
        }
        $Offset += $Read
    }

    return [PSCustomObject]@{
        Method = $RequestParts[0].ToUpperInvariant()
        Path = $RequestParts[1]
        Headers = $Headers
        Body = $BodyBytes
    }
}

function Test-TelemetryPayload {
    param([object]$Payload)

    if (-not $Payload -or -not $Payload.streams -or $Payload.streams.Count -ne 1) {
        return "Telemetry payload must contain exactly one stream."
    }
    $Stream = $Payload.streams[0]
    if (-not $Stream.stream -or
        $Stream.stream.app -ne "madfact" -or
        $Stream.stream.source -ne "unity") {
        return "Telemetry stream labels are not allowed."
    }
    if (-not $Stream.values -or $Stream.values.Count -lt 1 -or $Stream.values.Count -gt 20) {
        return "Telemetry payload must contain between 1 and 20 values."
    }
    foreach ($Value in $Stream.values) {
        if (-not $Value -or $Value.Count -ne 2 -or
            $Value[0] -notmatch "^[0-9]+$" -or
            -not ($Value[1] -is [string])) {
            return "Telemetry value format is invalid."
        }
        try {
            $Record = $Value[1] | ConvertFrom-Json
        } catch {
            return "Telemetry record must contain JSON."
        }
        if ($Record.app -ne "madfact" -or $Record.source -ne "unity") {
            return "Telemetry record identity is invalid."
        }
        if (-not $Record.event_type) {
            return "Telemetry event type is missing."
        }
        if (-not $Record.game_session_id) {
            return "Telemetry session ID is missing."
        }
        if (-not $Record.participant_id) {
            return "Telemetry participant ID is missing."
        }
    }
    return $null
}

function Send-Telemetry {
    param(
        [IO.Stream]$Stream,
        [byte[]]$BodyBytes
    )

    if (-not $env:LOKI_PASSWORD) {
        Write-JsonResponse $Stream 503 "Service Unavailable" @{
            error = "LOKI_PASSWORD is not configured on the local web server."
        }
        return
    }

    try {
        $Body = [Text.Encoding]::UTF8.GetString($BodyBytes)
        $Payload = $Body | ConvertFrom-Json
    } catch {
        Write-JsonResponse $Stream 400 "Bad Request" @{
            error = "Telemetry payload is not valid JSON."
        }
        return
    }

    $ValidationError = Test-TelemetryPayload $Payload
    if ($ValidationError) {
        Write-JsonResponse $Stream 400 "Bad Request" @{ error = $ValidationError }
        return
    }

    $LokiUser = if ($env:LOKI_USER) { $env:LOKI_USER } else { "beetrap" }
    $LokiEndpoint = if ($env:LOKI_ENDPOINT) {
        $env:LOKI_ENDPOINT
    } elseif ($env:LOKI_URL) {
        $env:LOKI_URL.TrimEnd("/") + "/loki/api/v1/push"
    } else {
        "https://loki-madfact.interplaylab.io/loki/api/v1/push"
    }
    $CredentialBytes = [Text.Encoding]::UTF8.GetBytes(
        $LokiUser + ":" + $env:LOKI_PASSWORD
    )
    $Authorization = "Basic " + [Convert]::ToBase64String($CredentialBytes)

    try {
        Invoke-WebRequest `
            -Uri $LokiEndpoint `
            -Method Post `
            -Headers @{ Authorization = $Authorization } `
            -ContentType "application/json" `
            -Body $BodyBytes `
            -UseBasicParsing `
            -TimeoutSec 15 | Out-Null
        Write-Headers $Stream 204 "No Content" "text/plain; charset=utf-8" 0
    } catch {
        Write-JsonResponse $Stream 502 "Bad Gateway" @{
            error = "The local telemetry relay could not reach Loki."
        }
    }
}

function Test-PosterPayload {
    param([object]$Payload)

    if (-not $Payload) {
        return "Poster request must be a JSON object."
    }
    $SessionId = [string]$Payload.session_id
    if (-not $SessionId -or $SessionId -notmatch "^[A-Za-z0-9_-]{8,128}$") {
        return "Poster session ID is invalid."
    }
    $Prompt = [string]$Payload.prompt
    if (-not $Prompt -or $Prompt.Trim().Length -lt 20) {
        return "Poster prompt needs more detail."
    }
    if ($Prompt.Length -gt 4000) {
        return "Poster prompt is too long."
    }
    if ($Prompt.Contains([char]0)) {
        return "Poster prompt contains invalid text."
    }
    return $null
}

function Send-PosterGeneration {
    param(
        [IO.Stream]$Stream,
        [byte[]]$BodyBytes
    )

    if (-not $env:OPENAI_API_KEY) {
        Write-JsonResponse $Stream 503 "Service Unavailable" @{
            error = "Poster generation is not configured. Add OPENAI_API_KEY to the local server's .env file."
            code = "poster_not_configured"
        }
        return
    }

    try {
        $Body = [Text.Encoding]::UTF8.GetString($BodyBytes)
        $Payload = $Body | ConvertFrom-Json
    } catch {
        Write-JsonResponse $Stream 400 "Bad Request" @{
            error = "Poster request is not valid JSON."
        }
        return
    }

    $ValidationError = Test-PosterPayload $Payload
    if ($ValidationError) {
        Write-JsonResponse $Stream 400 "Bad Request" @{ error = $ValidationError }
        return
    }

    $SessionId = [string]$Payload.session_id
    $Count = if ($PosterGenerationCounts.ContainsKey($SessionId)) {
        [int]$PosterGenerationCounts[$SessionId]
    } else {
        0
    }
    if ($Count -ge $MaximumPostersPerSession) {
        Write-JsonResponse $Stream 429 "Too Many Requests" @{
            error = "This game session has already generated seven posters."
            code = "generation_limit"
        }
        return
    }

    $SafetyPrefix =
        "Create fictional, family-friendly movie-poster artwork appropriate for sixth-grade students. " +
        "Do not depict gore, graphic violence, sexual content, drugs, hateful imagery, or real-person likenesses. "
    $OpenAIPayload = @{
        model = "gpt-image-2"
        prompt = $SafetyPrefix + ([string]$Payload.prompt).Trim()
        size = "1024x1536"
        quality = "medium"
        output_format = "jpeg"
        output_compression = 85
        moderation = "auto"
        n = 1
    } | ConvertTo-Json -Compress

    try {
        $Response = Invoke-RestMethod `
            -Uri "https://api.openai.com/v1/images/generations" `
            -Method Post `
            -Headers @{ Authorization = "Bearer " + $env:OPENAI_API_KEY } `
            -ContentType "application/json" `
            -Body ([Text.Encoding]::UTF8.GetBytes($OpenAIPayload)) `
            -UseBasicParsing `
            -TimeoutSec 150
        $ImageBase64 = [string]$Response.data[0].b64_json
        if (-not $ImageBase64) {
            throw "Image response did not contain b64_json."
        }
    } catch {
        $StatusCode = 0
        $UpstreamCode = ""
        try {
            if ($_.Exception.Response) {
                $StatusCode = [int]$_.Exception.Response.StatusCode
                $Reader = [IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
                try {
                    $ErrorPayload = $Reader.ReadToEnd() | ConvertFrom-Json
                    $UpstreamCode = [string]$ErrorPayload.error.code
                } finally {
                    $Reader.Dispose()
                }
            }
        } catch {
            $UpstreamCode = ""
        }

        if ($UpstreamCode -eq "moderation_blocked") {
            Write-JsonResponse $Stream 400 "Bad Request" @{
                error = "That prompt was blocked by image safety checks."
                code = "moderation_blocked"
            }
        } elseif ($StatusCode -eq 429) {
            Write-JsonResponse $Stream 429 "Too Many Requests" @{
                error = "The image service is busy. Try again shortly."
                code = "upstream_rate_limit"
            }
        } elseif ($StatusCode -eq 401 -or $StatusCode -eq 403) {
            Write-JsonResponse $Stream 503 "Service Unavailable" @{
                error = "The local poster API credential was rejected."
                code = "credential_rejected"
            }
        } else {
            Write-JsonResponse $Stream 502 "Bad Gateway" @{
                error = "The local poster relay could not generate an image."
            }
        }
        return
    }

    $Count++
    $PosterGenerationCounts[$SessionId] = $Count
    Write-JsonResponse $Stream 200 "OK" @{
        image_base64 = $ImageBase64
        mime_type = "image/jpeg"
        remaining = $MaximumPostersPerSession - $Count
    }
}

$RootPath = [IO.Path]::GetFullPath($BuildDirectory)
$RootPrefix = $RootPath.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar
) + [IO.Path]::DirectorySeparatorChar
$Listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Any, $Port)

try {
    $Listener.Start()
    Write-Host "Serving MadMovieFact at $Url"
    Write-Host "No Unity installation is required."
    if ($env:LOKI_PASSWORD) {
        Write-Host "Telemetry relay: configured"
    } else {
        Write-Host "Telemetry relay: disabled (LOKI_PASSWORD is missing)"
    }
    if ($env:OPENAI_API_KEY) {
        Write-Host "Poster generation: configured"
    } else {
        Write-Host "Poster generation: disabled (OPENAI_API_KEY is missing)"
    }
    if ($LoadedEnvPath) {
        Write-Host "Configuration: $LoadedEnvPath"
    }
    Write-Host "Press Ctrl+C to stop."

    try {
        Start-Process $Url
    } catch {
        Write-Host "Open $Url in your browser."
    }

    while ($true) {
        $Client = $Listener.AcceptTcpClient()
        $Stream = $null

        try {
            $Client.ReceiveTimeout = 180000
            $Client.SendTimeout = 180000
            $Stream = $Client.GetStream()
            $Request = Read-HttpRequest $Stream
            $Method = $Request.Method
            $RawPath = $Request.Path.Split("?")[0]

            if ($RawPath -eq "/api/telemetry") {
                if ($Method -ne "POST") {
                    Write-ErrorResponse $Stream $Method 405 "Method Not Allowed"
                    continue
                }
                Send-Telemetry $Stream $Request.Body
                continue
            }

            if ($RawPath -eq "/api/poster/generate") {
                if ($Method -ne "POST") {
                    Write-ErrorResponse $Stream $Method 405 "Method Not Allowed"
                    continue
                }
                Send-PosterGeneration $Stream $Request.Body
                continue
            }

            if ($Method -ne "GET" -and $Method -ne "HEAD") {
                Write-ErrorResponse $Stream $Method 405 "Method Not Allowed"
                continue
            }

            $DecodedPath = [Uri]::UnescapeDataString($RawPath)
            $RelativePath = $DecodedPath.TrimStart("/").Replace(
                "/",
                [IO.Path]::DirectorySeparatorChar
            )
            if ([string]::IsNullOrWhiteSpace($RelativePath)) {
                $RelativePath = "index.html"
            }

            $FilePath = [IO.Path]::GetFullPath((Join-Path $RootPath $RelativePath))
            $IsWithinRoot = $FilePath.Equals(
                $RootPath,
                [StringComparison]::OrdinalIgnoreCase
            ) -or $FilePath.StartsWith(
                $RootPrefix,
                [StringComparison]::OrdinalIgnoreCase
            )

            if (-not $IsWithinRoot) {
                Write-ErrorResponse $Stream $Method 403 "Forbidden"
                continue
            }

            if (Test-Path -LiteralPath $FilePath -PathType Container) {
                $FilePath = Join-Path $FilePath "index.html"
            }

            if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
                Write-ErrorResponse $Stream $Method 404 "Not Found"
                continue
            }

            $ContentEncoding = ""
            if ($FilePath.EndsWith(".gz", [StringComparison]::OrdinalIgnoreCase)) {
                $ContentEncoding = "gzip"
            } elseif ($FilePath.EndsWith(".br", [StringComparison]::OrdinalIgnoreCase)) {
                $ContentEncoding = "br"
            }

            $FileInfo = Get-Item -LiteralPath $FilePath
            Write-Headers `
                $Stream `
                200 `
                "OK" `
                (Get-ContentType $FilePath) `
                $FileInfo.Length `
                $ContentEncoding

            if ($Method -ne "HEAD") {
                $FileStream = [IO.File]::OpenRead($FilePath)
                try {
                    $Buffer = New-Object byte[] 65536
                    while (($BytesRead = $FileStream.Read($Buffer, 0, $Buffer.Length)) -gt 0) {
                        $Stream.Write($Buffer, 0, $BytesRead)
                    }
                } finally {
                    $FileStream.Dispose()
                }
            }
        } catch {
            if ($Stream) {
                try {
                    Write-ErrorResponse $Stream "GET" 500 "Internal Server Error"
                } catch {
                    # The browser may have already closed the connection.
                }
            }
        } finally {
            if ($Stream) { $Stream.Dispose() }
            $Client.Dispose()
        }
    }
} finally {
    $Listener.Stop()
}
