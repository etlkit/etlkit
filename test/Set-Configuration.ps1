#!/usr/bin/env pwsh

param (
    [string]$configEnvironment
)


function Merge-Tokens
{
    [CmdletBinding()]
    [OutputType('string')]
    param (
        [Parameter(ValueFromPipeline)] $template,
        [Hashtable] $tokens
    )
    process {
        return [regex]::Replace(
                $template,
                '\$\{(?<tokenName>[\w_]+)\}',
                {
                    param($match)

                    $tokenName = $match.Groups['tokenName'].Value

                    return $tokens[$tokenName]
                })
    }
}

function ConvertTo-Hashtable
{
    [CmdletBinding()]
    [OutputType('hashtable')]
    param (
        [Parameter(ValueFromPipeline)]
        $InputObject
    )

    process {
        ## Return null if the input is null. This can happen when calling the function
        ## recursively and a property is null
        if ($null -eq $InputObject)
        {
            return $null
        }

        ## Check if the input is an array or collection. If so, we also need to convert
        ## those types into hash tables as well. This function will convert all child
        ## objects into hash tables (if applicable)
        if ($InputObject -is [System.Collections.IEnumerable] -and $InputObject -isnot [string])
        {
            $collection = @(
            foreach ($object in $InputObject)
            {
                ConvertTo-Hashtable -InputObject $object
            }
            )

            ## Return the array but don't enumerate it because the object may be pretty complex
            Write-Output -NoEnumerate $collection
        }
        elseif ($InputObject -is [psobject])
        {
            ## If the object has properties that need enumeration
            ## Convert it to its own hash table and return it
            $hash = @{ }
            foreach ($property in $InputObject.PSObject.Properties)
            {
                $hash[$property.Name] = ConvertTo-Hashtable -InputObject $property.Value
            }
            $hash
        }
        else
        {
            ## If the object isn't an array, collection, or other object, it's already a hash table
            ## So just return it.
            $InputObject
        }
    }
}

function Show-Menu {
    $files = Get-ChildItem (Join-Path $PSScriptRoot ./config/*.json)
    $choiceIndex = 1
    $options = $files | ForEach-Object {
        New-Object System.Management.Automation.Host.ChoiceDescription "&$($choiceIndex) - $($_.Name)"
        $choiceIndex = $choiceIndex + 1
    }
    $chosenIndex = $host.ui.PromptForChoice("Choose configuration...", "Below is the list of environments:", $options, 0)
    return $files[$chosenIndex].FullName
}

$configFiles = @(
    "../TestDatabaseConnectors/default.config.json",
    "../TestConnectionManager/default.config.json",
    "../TestFlatFileConnectors/default.config.json",
    "../TestNonParallel/default.config.json",
    "../TestNonParallel/docker.config.json",
    "../TestTransformations/default.config.json",
    "../TestOtherConnectors/default.config.json",
    "../TestControlFlowTasks/default.config.json",
    "../TestPerformance/default.config.json",
    "../EtlKit.Kafka.Tests/default.config.json"
)

if ($PSBoundParameters.ContainsKey('configEnvironment') -eq $false) {
    $configPath = Show-Menu
}
else {
    $configPath = [System.IO.Path]::Combine('config', "$configEnvironment.json")
}
echo "Applying config $configPath ..."
$config = (Get-Content $configPath | Out-String | ConvertFrom-Json | ConvertTo-Hashtable)

pushd ..
foreach ($file in $configFiles)
{
    $fileName = Join-Path $PSScriptRoot $file
    echo "Applying to $fileName ..."
    Get-Content "$fileName-template" | Merge-Tokens -tokens $config | Out-File $fileName
}
popd
echo "Done!"
