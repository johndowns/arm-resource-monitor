param
(
    [Parameter(Mandatory)]
    $ResourceGroupName,

    [Parameter(Mandatory)]
    $ResourceGroupLocation,

    [Parameter(Mandatory)]
    $SendGridApiKey,

    [Parameter(Mandatory)]
    $EmailFromAddress,

    [Parameter(Mandatory)]
    $EmailFromName,

    [Parameter(Mandatory)]
    $EmailToAddress,

    [Parameter(Mandatory)]
    $EmailToName
)

Write-Host "Creating resource group $ResourceGroupName in location $ResourceGroupLocation."
az group create -n $ResourceGroupName -l $ResourceGroupLocation

Write-Host 'Starting deployment of ARM template.'
$templateFilePath = Join-Path $PSScriptRoot 'arm-resource-monitor.json'
$deploymentOutputsJson = az deployment group create -g $ResourceGroupName --template-file $templateFilePath --parameters sendGridApiKey=$SendGridApiKey emailFromAddress=$EmailFromAddress emailFromName=$EmailFromName emailToAddress=$EmailToAddress emailToName=$EmailToName
$deploymentOutputs = $deploymentOutputsJson | ConvertFrom-Json
$functionIdentityPrincipalId = $deploymentOutputs.properties.outputs.functionIdentityPrincipalId.value
$functionAppName = $deploymentOutputs.properties.outputs.functionAppName.value
$functionAppUrl = $deploymentOutputs.properties.outputs.functionAppUrl.value

Write-Host "Deploying functions app to Azure Functions app $functionAppAName."
$functionAppFolder = Join-Path $PSScriptRoot '..' 'src' 'ArmResourceMonitor'
Write-Host $functionAppFolder
Push-Location $functionAppFolder
func azure functionapp publish $functionAppName --force
Pop-Location

Write-Host 'Deployment complete.'
