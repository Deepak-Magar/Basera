# Smoke test against a running Basera (Development: it reads SMS codes from /dev/sms).
# Usage (app running, e.g. via ./scripts/dev.ps1):  ./scripts/smoke.ps1 [-BaseUrl http://localhost:5080]
#
# Covers what exists so far (ARCHITECTURE section 11 lists the rest for later sessions):
#   health -> register an owner -> sign out -> sign in -> OTP sign-in of the seeded resident
#   -> owner invites a warden -> the warden accepts the SMS link.
# Needs the dev seed (scripts/dev.ps1 runs it). It makes 6 sign-in POSTs; the "auth" limit is 10 a minute
# per IP, so wait a minute between two runs.
param(
    [string]$BaseUrl = 'http://localhost:5080'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

$ResidentPhone = '9800000001'
$script:failures = 0

# A browser-like client: its own cookies, no automatic redirects (so each 302 can be checked).
function New-Browser {
    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.CookieContainer = New-Object System.Net.CookieContainer
    $handler.AllowAutoRedirect = $false
    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.BaseAddress = New-Object System.Uri($BaseUrl)
    return $client
}

function Get-Page($Browser, [string]$Path) {
    $response = $Browser.GetAsync($Path).Result
    return [pscustomobject]@{
        Status   = [int]$response.StatusCode
        Location = if ($response.Headers.Location) { $response.Headers.Location.OriginalString } else { $null }
        Content  = $response.Content.ReadAsStringAsync().Result
    }
}

# GETs $Page for its anti-forgery token, then POSTs $Fields to $Action (default: the same page).
function Submit-Form($Browser, [string]$Page, [hashtable]$Fields, [string]$Action = $Page) {
    $html = (Get-Page $Browser $Page).Content
    $token = [regex]::Match($html, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
    if (-not $token) {
        $token = [regex]::Match($html, 'RequestVerificationToken&quot;:&quot;([^&]+)&quot;').Groups[1].Value
    }

    $pairs = New-Object 'System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]'
    $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new('__RequestVerificationToken', $token))
    foreach ($key in $Fields.Keys) {
        $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new([string]$key, [string]$Fields[$key]))
    }

    # ::new, not New-Object: New-Object would unroll the list into separate constructor arguments.
    $response = $Browser.PostAsync($Action, [System.Net.Http.FormUrlEncodedContent]::new($pairs)).Result
    return [pscustomobject]@{
        Status   = [int]$response.StatusCode
        Location = if ($response.Headers.Location) { $response.Headers.Location.OriginalString } else { $null }
        Content  = $response.Content.ReadAsStringAsync().Result
    }
}

function Check([string]$Name, [bool]$Ok, [string]$Detail = '') {
    if ($Ok) {
        Write-Host "  ok    $Name" -ForegroundColor Green
    }
    else {
        Write-Host "  FAIL  $Name $Detail" -ForegroundColor Red
        $script:failures++
    }
}

# The newest SMS to +977<national> on /dev/sms, as text.
function Get-LatestSms([string]$National) {
    $inbox = (Get-Page (New-Browser) '/dev/sms').Content
    foreach ($row in [regex]::Matches($inbox, '<tr>(.*?)</tr>', 'Singleline')) {
        # The phone shows as "980-0000001" (the "+" in its tel: link is HTML-encoded, so match the display form).
        if ($row.Groups[1].Value.Contains('>' + $National.Substring(0, 3) + '-' + $National.Substring(3) + '<')) {
            return [System.Net.WebUtility]::HtmlDecode([regex]::Match($row.Groups[1].Value, 'pre-wrap">(.*?)</td>', 'Singleline').Groups[1].Value)
        }
    }
    return ''
}

function New-Phone { '98' + (Get-Random -Minimum 10000000 -Maximum 99999999) }

Write-Host "Smoke test against $BaseUrl"

# 1. Health
$health = Get-Page (New-Browser) '/health'
Check 'health is Healthy' ($health.Status -eq 200 -and $health.Content -match '"status":"Healthy"') $health.Content

# 2. Register an owner
$owner = New-Browser
$ownerPhone = New-Phone
$email = "smoke-$ownerPhone@basera.test"
$password = 'Sm0ke-test!'
$hostel = "Smoke Hostel $ownerPhone"
$registered = Submit-Form $owner '/account/register' @{
    HostelName = $hostel; OwnerName = 'Smoke Owner'; Phone = $ownerPhone; Email = $email; Password = $password
}
Check 'register redirects to /owner' ($registered.Status -eq 302 -and $registered.Location -eq '/owner') "($($registered.Status) $($registered.Location))"
$ownerHome = Get-Page $owner '/owner'
Check 'owner area shows the hostel and the owner' ($ownerHome.Status -eq 200 -and $ownerHome.Content.Contains($hostel) -and $ownerHome.Content.Contains('Smoke Owner'))

# 3. Sign out, sign in with email
$out = Submit-Form $owner '/owner' @{} '/account/logout'
Check 'sign out' ($out.Status -eq 302 -and (Get-Page $owner '/owner').Status -eq 302)
$login = Submit-Form $owner '/account/login' @{ Login = $email; Password = $password }
Check 'password sign-in lands in /owner' ($login.Status -eq 302 -and $login.Location -eq '/owner') "($($login.Status) $($login.Location))"

# 4. OTP sign-in of the seeded resident
$resident = New-Browser
$sent = Submit-Form $resident '/account/otp' @{ Phone = $ResidentPhone }
Check 'OTP code sent to the seeded resident' ($sent.Status -eq 302) "($($sent.Status)) run scripts/dev.ps1 to seed?"
$code = [regex]::Match((Get-LatestSms $ResidentPhone), 'code: (\d{6})').Groups[1].Value
$verified = Submit-Form $resident "/account/otp/verify?phone=$ResidentPhone" @{ Phone = $ResidentPhone; Code = $code } '/account/otp/verify'
Check 'OTP sign-in lands in /resident' ($verified.Status -eq 302 -and $verified.Location -eq '/resident') "($($verified.Status) $($verified.Location))"

# 5. Owner invites a warden; the warden accepts the SMS link
$wardenPhone = New-Phone
$invited = Submit-Form $owner '/owner/staff/invite' @{ Phone = $wardenPhone; Role = 'Warden' }
Check 'invitation sent' ($invited.Status -eq 302 -and $invited.Location -eq '/owner/staff') "($($invited.Status) $($invited.Location))"
$link = [regex]::Match((Get-LatestSms $wardenPhone), '/account/invite/[A-Za-z0-9_-]+').Value
Check 'invitation SMS has a link' ([bool]$link)
$warden = New-Browser
$accepted = Submit-Form $warden $link @{ DisplayName = 'Smoke Warden'; Password = $password }
Check 'warden accepts and lands in /staff' ($accepted.Status -eq 302 -and $accepted.Location -eq '/staff') "($($accepted.Status) $($accepted.Location))"
Check 'owner sees the warden on the staff page' ((Get-Page $owner '/owner/staff').Content.Contains('Smoke Warden'))

if ($script:failures -gt 0) {
    Write-Host "$($script:failures) check(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host 'All smoke checks passed.' -ForegroundColor Green
