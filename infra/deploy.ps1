#!/usr/bin/env pwsh
# Three Blind Mice — Infrastructure Deployment Script
#
# Prerequisites:
#   - Azure CLI installed and logged in (az login)
#   - Bicep CLI (bundled with Azure CLI 2.20+)
#
# Usage:
#   ./infra/deploy.ps1                          # Deploy with defaults
#   ./infra/deploy.ps1 -ResourceGroup my-rg     # Custom resource group
#   ./infra/deploy.ps1 -WhatIf                  # Preview changes only

param(
	[string]$ResourceGroup = "rg-three-blind-mice",
	[string]$Location = "australiaeast",
	[string]$AppName = "three-blind-mice",
	[switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$script_dir = $PSScriptRoot

Write-Host "`n=== Three Blind Mice — Infrastructure ===" -ForegroundColor Cyan

# 1. Ensure resource providers are registered
Write-Host "`nChecking resource providers..." -ForegroundColor Yellow
$providers = @("Microsoft.Web", "Microsoft.SignalRService")
foreach ($provider in $providers) {
	$state = az provider show --namespace $provider --query "registrationState" -o tsv 2>$null
	if ($state -ne "Registered") {
		Write-Host "  Registering $provider..." -ForegroundColor Gray
		az provider register --namespace $provider --wait | Out-Null
	} else {
		Write-Host "  $provider — already registered" -ForegroundColor Green
	}
}

# 2. Create resource group if needed
Write-Host "`nResource group: $ResourceGroup ($Location)" -ForegroundColor Yellow
$rg_exists = az group exists --name $ResourceGroup 2>$null
if ($rg_exists -eq "false") {
	Write-Host "  Creating resource group..." -ForegroundColor Gray
	az group create --name $ResourceGroup --location $Location --output none
	Write-Host "  Created." -ForegroundColor Green
} else {
	Write-Host "  Already exists." -ForegroundColor Green
}

# 3. Deploy Bicep template
Write-Host "`nDeploying infrastructure..." -ForegroundColor Yellow

$deploy_args = @(
	"deployment", "group", "create",
	"--resource-group", $ResourceGroup,
	"--template-file", "$script_dir\main.bicep",
	"--parameters", "app_name=$AppName", "location=$Location",
	"--output", "json"
)

if ($WhatIf) {
	$deploy_args = @(
		"deployment", "group", "what-if",
		"--resource-group", $ResourceGroup,
		"--template-file", "$script_dir\main.bicep",
		"--parameters", "app_name=$AppName", "location=$Location"
	)
	Write-Host "  (What-If mode)" -ForegroundColor Gray
}

$result = az @deploy_args 2>&1
if ($LASTEXITCODE -ne 0) {
	Write-Host "Deployment failed:" -ForegroundColor Red
	Write-Host $result
	exit 1
}

if ($WhatIf) {
	Write-Host $result
	exit 0
}

$deployment = $result | ConvertFrom-Json

# 4. Extract outputs
$swa_hostname = $deployment.properties.outputs.swa_hostname.value
$swa_token = $deployment.properties.outputs.swa_deployment_token.value
$pubsub_hostname = $deployment.properties.outputs.pubsub_hostname.value

Write-Host "`n=== Deployment Complete ===" -ForegroundColor Green
Write-Host "  Static Web App:  https://$swa_hostname" -ForegroundColor White
Write-Host "  Web PubSub:      $pubsub_hostname" -ForegroundColor White
Write-Host "`n  Deployment Token:" -ForegroundColor Yellow
Write-Host "  $swa_token" -ForegroundColor Gray
Write-Host "`nTo deploy the app:" -ForegroundColor Yellow
Write-Host "  npm run build" -ForegroundColor Gray
Write-Host "  npx @azure/static-web-apps-cli deploy ./dist --api-location api \" -ForegroundColor Gray
Write-Host "    --api-language node --api-version 18 \" -ForegroundColor Gray
Write-Host "    --deployment-token `"$swa_token`" --env production" -ForegroundColor Gray
