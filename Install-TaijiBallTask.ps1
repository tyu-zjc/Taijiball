<#
.SYNOPSIS
    把 TaijiBall.exe 注册成「登录时静默提权启动」的计划任务，这样开机和手动启动都不会再弹 UAC。

.DESCRIPTION
    原理：计划任务以 RunLevel=Highest（最高权限）注册后，任务计划程序会用提权令牌直接拉起进程，
    不经过 UAC 弹窗。用 schtasks /run 触发同一个任务，也同样静默提权。

    本脚本自身需要管理员权限（注册最高权限任务是特权操作），所以会自动请求一次提权——
    这一次是你安装时点，之后开机都不会再问。

.PARAMETER Uninstall
    移除计划任务。

.PARAMETER NoStart
    只注册，不立刻启动太极球。

.PARAMETER DryRun
    只打印将要注册的任务定义，不真正注册（不需要管理员权限）。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Install-TaijiBallTask.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Install-TaijiBallTask.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [switch]$Uninstall,
    [switch]$NoStart,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$TaskName = 'TaijiBall'
$ExePath  = Join-Path $PSScriptRoot 'TaijiBall.exe'

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $pr = New-Object Security.Principal.WindowsPrincipal($id)
    return $pr.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# ---- 非管理员时自动提权重启自己（DryRun 不需要） ----
if (-not $DryRun -and -not (Test-Admin)) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $PSCommandPath))
    if ($Uninstall) { $argList += '-Uninstall' }
    if ($NoStart)   { $argList += '-NoStart' }
    Write-Host '需要管理员权限来注册计划任务，正在请求提权……' -ForegroundColor Yellow
    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -Verb RunAs
    return
}

# ---- 卸载 ----
if ($Uninstall) {
    $existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($existing) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "已移除计划任务「$TaskName」。" -ForegroundColor Green
    }
    else {
        Write-Host "计划任务「$TaskName」本来就不存在。" -ForegroundColor DarkGray
    }
    return
}

# ---- 注册 ----
if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "同目录下找不到 TaijiBall.exe：$ExePath"
}

$user = "$env:USERDOMAIN\$env:USERNAME"

if ($DryRun) {
    Write-Host '== DryRun：以下任务定义不会被注册 ==' -ForegroundColor Cyan
    [pscustomobject]@{
        TaskName   = $TaskName
        Execute    = $ExePath
        WorkingDir = $PSScriptRoot
        Trigger    = "AtLogOn($env:USERNAME)"
        Principal  = $user
        RunLevel   = 'Highest'
        LogonType  = 'Interactive'
    } | Format-List
    return
}

$action    = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory $PSScriptRoot
$trigger   = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Highest
$settings  = New-ScheduledTaskSettingsSet `
                -AllowStartIfOnBatteries `
                -DontStopIfGoingOnBatteries `
                -ExecutionTimeLimit ([TimeSpan]::Zero) `
                -MultipleInstances IgnoreNew

Register-ScheduledTask -TaskName $TaskName `
    -Action $action -Trigger $trigger -Principal $principal -Settings $settings `
    -Description '30x30 太极内存球：登录时静默提权启动，点击不再弹 UAC' `
    -Force | Out-Null

Write-Host "已注册计划任务「$TaskName」：" -ForegroundColor Green
Write-Host "  程序   : $ExePath"
Write-Host "  触发   : $env:USERNAME 登录时（最高权限，无 UAC）"
Write-Host ''
Write-Host '以后想手动启动，双击目录里的「启动太极球.vbs」即可（它调用 schtasks /run，同样不弹 UAC）。' -ForegroundColor DarkGray
Write-Host '若要取消：-Uninstall' -ForegroundColor DarkGray

if (-not $NoStart) {
    Start-ScheduledTask -TaskName $TaskName
    Write-Host '已立即启动太极球。' -ForegroundColor Green
}
