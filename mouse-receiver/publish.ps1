#!/usr/bin/env pwsh
# Three Blind Mice — Mouse Receiver Publish Script
#
# Usage:
#   ./publish.ps1                                    # Build both platforms
#   ./publish.ps1 -Platform win-x64                  # Build Windows only
#   ./publish.ps1 -Platform linux-x64                # Build Linux only
#   ./publish.ps1 -CertPath cert.pfx -CertPassword p # Build + sign Windows

param(
	[ValidateSet('all', 'win-x64', 'linux-x64')]
	[string]$Platform = 'all',
	[string]$CertPath = '',
	[string]$CertPassword = '',
	[string]$OutputDir = './publish'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step($message) {
	Write-Host "`n==> $message" -ForegroundColor Cyan
}

function Get-FileSizeFormatted($path) {
	$size = (Get-Item $path).Length
	if ($size -ge 1MB) { return "{0:N2} MB" -f ($size / 1MB) }
	if ($size -ge 1KB) { return "{0:N2} KB" -f ($size / 1KB) }
	return "$size bytes"
}

function Get-FileHash256($path) {
	return (Get-FileHash -Path $path -Algorithm SHA256).Hash
}

function Find-SignTool {
	# Search Windows SDK paths for signtool.exe
	$sdk_roots = @(
		"${env:ProgramFiles(x86)}\Windows Kits\10\bin"
		"${env:ProgramFiles}\Windows Kits\10\bin"
	)
	foreach ($root in $sdk_roots) {
		if (Test-Path $root) {
			$candidates = Get-ChildItem -Path $root -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
				Where-Object { $_.FullName -match 'x64' } |
				Sort-Object FullName -Descending
			if ($candidates) {
				return $candidates[0].FullName
			}
		}
	}
	return $null
}

function Sign-Binary($exe_path, $cert_path, $cert_password) {
	$signtool = Find-SignTool
	if (-not $signtool) {
		Write-Warning "signtool.exe not found in Windows SDK paths — skipping signing"
		return $false
	}

	Write-Step "Signing $exe_path"
	Write-Host "  Using: $signtool"

	$sign_args = @(
		"sign"
		"/f", $cert_path
		"/p", $cert_password
		"/fd", "SHA256"
		"/tr", "http://timestamp.digicert.com"
		"/td", "SHA256"
		$exe_path
	)
	& $signtool @sign_args
	if ($LASTEXITCODE -ne 0) {
		throw "signtool failed with exit code $LASTEXITCODE"
	}
	return $true
}

function Publish-Platform($rid, $project, $out_dir) {
	Write-Step "Publishing $project for $rid"
	dotnet publish "$project" -c Release -r $rid --self-contained false /p:PublishSingleFile=true -o $out_dir
	if ($LASTEXITCODE -ne 0) {
		throw "dotnet publish failed for $rid with exit code $LASTEXITCODE"
	}
}

# --- Main ---

$script_dir = $PSScriptRoot
Push-Location $script_dir

try {
	$build_win = $Platform -eq 'all' -or $Platform -eq 'win-x64'
	$build_linux = $Platform -eq 'all' -or $Platform -eq 'linux-x64'
	$should_sign = $CertPath -ne '' -and $build_win
	$summary = @()

	# Clean output directory
	Write-Step "Cleaning output directory: $OutputDir"
	if (Test-Path $OutputDir) {
		Remove-Item -Path $OutputDir -Recurse -Force
	}

	# Publish Windows
	if ($build_win) {
		$win_out = "$OutputDir/win-x64"
		Publish-Platform "win-x64" "ThreeBlindMice.Windows/ThreeBlindMice.Windows.csproj" $win_out

		$win_exe = Join-Path $win_out "ThreeBlindMice.Windows.exe"
		if (-not (Test-Path $win_exe)) {
			throw "Expected output not found: $win_exe"
		}

		# Sign if certificate was provided
		if ($should_sign) {
			Sign-Binary $win_exe $CertPath $CertPassword
		}

		$summary += [PSCustomObject]@{
			Platform = "win-x64"
			Path     = $win_exe
			Size     = Get-FileSizeFormatted $win_exe
			SHA256   = Get-FileHash256 $win_exe
		}
	}

	# Publish Linux
	if ($build_linux) {
		$linux_out = "$OutputDir/linux-x64"
		Publish-Platform "linux-x64" "ThreeBlindMice.Linux/ThreeBlindMice.Linux.csproj" $linux_out

		$linux_bin = Join-Path $linux_out "ThreeBlindMice.Linux"
		if (-not (Test-Path $linux_bin)) {
			throw "Expected output not found: $linux_bin"
		}

		$summary += [PSCustomObject]@{
			Platform = "linux-x64"
			Path     = $linux_bin
			Size     = Get-FileSizeFormatted $linux_bin
			SHA256   = Get-FileHash256 $linux_bin
		}
	}

	# Summary
	Write-Host "`n"
	Write-Host ("=" * 60) -ForegroundColor Green
	Write-Host "  Publish Summary" -ForegroundColor Green
	Write-Host ("=" * 60) -ForegroundColor Green

	foreach ($entry in $summary) {
		Write-Host "`n  Platform : $($entry.Platform)"
		Write-Host "  Path     : $($entry.Path)"
		Write-Host "  Size     : $($entry.Size)"
		Write-Host "  SHA256   : $($entry.SHA256)"
	}

	if ($should_sign) {
		Write-Host "`n  Signing  : Applied (SHA256 + timestamp)" -ForegroundColor Yellow
	}

	Write-Host "`n$("=" * 60)`n" -ForegroundColor Green
}
finally {
	Pop-Location
}
