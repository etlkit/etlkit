#!/usr/bin/env pwsh
#requires -PSEdition Core
param (
  [Alias("c")]
  [switch]
  $clear = $false
)

# Validate Mac/Apple Silicon setup for proper x86_64 emulation
$isMac = $false
$isAppleSilicon = $false
$isProperAMD64Emulation = $false

try {
    $osType = uname 2>$null
    $isMac = ($osType -eq "Darwin")
    if ($isMac) {
        $isAppleSilicon = ($(uname -m) -eq "arm64")
        
        if ($isAppleSilicon) {
            # Check Docker architecture
            $dockerArch = docker info --format '{{.Architecture}}' 2>$null
            
            # Check kernel architecture inside container
            $kernelArch = docker run --rm alpine uname -m 2>$null
            
            # Check kernel version for x86_64
            $kernelInfo = docker run --rm alpine cat /proc/version 2>$null
            
            if ($dockerArch -eq 'x86_64' -and $kernelArch -eq 'x86_64' -and $kernelInfo -match 'x86_64') {
                $isProperAMD64Emulation = $true
            }
        } else {
            # Intel Mac - assume proper setup
            $isProperAMD64Emulation = $true
        }
    }
}
catch {
    # Not macOS or unable to detect - assume Windows/Linux
    $isMac = $false
}

if ($isMac -and $isAppleSilicon -and -not $isProperAMD64Emulation) {
    Write-Host "❌ Configuration Issue on macOS Apple Silicon" -ForegroundColor Red
    Write-Host ""
    Write-Host "This system requires proper x86_64 kernel and memory layout for MS SQL Server and other databases." -ForegroundColor Yellow
    Write-Host "Current setup does not provide true x86_64 virtualization." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Required:" -ForegroundColor Cyan
    Write-Host "  • Real x86_64 kernel (not just userspace emulation)" -ForegroundColor White
    Write-Host "  • Native x86_64 memory layout" -ForegroundColor White
    Write-Host "  • Colima with --arch x86_64 --vm-type vz --vz-rosetta" -ForegroundColor White
    Write-Host ""
    Write-Host "Solution:" -ForegroundColor Green
    Write-Host "  Run: ./test/Install-Prerequisites.ps1" -ForegroundColor White
    Write-Host ""
    Write-Host "This will install and configure:" -ForegroundColor Cyan
    Write-Host "  • Lima, Colima, QEMU, and additional guest agents" -ForegroundColor White
    Write-Host "  • Colima with proper x86_64 virtualization" -ForegroundColor White
    Write-Host "  • Docker with real x86_64 kernel support" -ForegroundColor White
    Write-Host ""
    exit 1
}

# Ensure Colima is running and set as Docker context on macOS
if ($isMac) {
    Write-Host "🔧 Configuring Docker context for macOS..." -ForegroundColor Blue
    
    # Check if Colima context exists
    $colimaContextExists = (docker context ls --format "{{.Name}}" | Select-String -Pattern "^colima$") -ne $null
    
    if (-not $colimaContextExists) {
        Write-Host "⚠️  Colima context not found. Please run ./test/Install-Prerequisites.ps1 first." -ForegroundColor Yellow
        exit 1
    }
    
    # Switch to Colima context
    $currentContext = (docker context show).Trim()
    if ($currentContext -ne "colima") {
        Write-Host "🔄 Switching Docker context from '$currentContext' to 'colima'..." -ForegroundColor Yellow
        & docker context use colima
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Failed to switch to Colima context" -ForegroundColor Red
            exit 1
        }
    }
    
    # Check if Colima is running
    $colimaStatus = & colima status 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "🚀 Starting Colima with x86_64 architecture..." -ForegroundColor Blue
        & colima start --arch x86_64 --vm-type vz --vz-rosetta --cpu 4 --memory 8
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Failed to start Colima" -ForegroundColor Red
            exit 1
        }
        Write-Host "✅ Colima started successfully" -ForegroundColor Green
    } else {
        # Check if Colima is running with correct architecture
        $colimaArch = ($colimaStatus | Select-String "arch: (.+)" | ForEach-Object { $_.Matches[0].Groups[1].Value })
        if ($colimaArch -ne "x86_64") {
            Write-Host "⚠️  Colima is running with $colimaArch architecture, need x86_64. Restarting..." -ForegroundColor Yellow
            & colima stop
            & colima delete --force 2>$null
            & colima start --arch x86_64 --vm-type vz --vz-rosetta --cpu 4 --memory 8
            if ($LASTEXITCODE -ne 0) {
                Write-Host "❌ Failed to restart Colima with x86_64" -ForegroundColor Red
                exit 1
            }
            Write-Host "✅ Colima restarted with x86_64 architecture" -ForegroundColor Green
        } else {
            Write-Host "✅ Colima is already running with x86_64 architecture" -ForegroundColor Green
        }
    }
}

# Create/update .testcontainers.properties with Colima context
$testcontainersPropsPath = [System.IO.Path]::Combine($PSScriptRoot, "..", ".testcontainers.properties")
$targetDockerContext = if ($isMac) { "colima" } else { (docker context show).Trim() }

$testcontainersContent = @"
# Testcontainers configuration for ETLBox
# This file is automatically generated/updated by Run-Containers.ps1
# Generated on: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

# Docker context configuration
docker.context=$targetDockerContext

# Reuse containers for better performance during testing
testcontainers.reuse.enable=true

# Container startup timeout (default: 60s)
testcontainers.container.startup.timeout=120

"@

try {
    Set-Content -Path $testcontainersPropsPath -Value $testcontainersContent -Encoding UTF8
    Write-Host "✅ Created/updated .testcontainers.properties with docker.context=$targetDockerContext" -ForegroundColor Green
}
catch {
    Write-Host "⚠️  Warning: Could not create .testcontainers.properties file: $($_.Exception.Message)" -ForegroundColor Yellow
}

if ($clear) {
  # Stop running containers
  $containerNames = @("localmssql", "localmysql", "localpostgres", "localclickhouse", "localkafka")
  $runningContainers = docker ps -q -f "name=$($containerNames -join '|')"

  if ($runningContainers) {
    docker stop $runningContainers
    if ($LASTEXITCODE -ne 0) {
      throw "Failed to stop containers with exit code: $LASTEXITCODE"
    }
  }

  # Remove existing containers with the same names
  $existingContainers = docker ps -a -q -f "name=$($containerNames -join '|')"
  if ($existingContainers) {
    docker rm $existingContainers
    if ($LASTEXITCODE -ne 0) {
      throw "Failed to remove containers with exit code: $LASTEXITCODE"
    }
  }
}

# Verify Docker is accessible before proceeding
try {
  $null = docker ps 2>&1
  if ($LASTEXITCODE -ne 0) {
    throw "Docker is not running or not accessible (exit code: $LASTEXITCODE)"
  }
} catch {
  Write-Host "Docker is not running or not accessible. Please start Docker Desktop and try again." -ForegroundColor Red
  throw "Docker is not accessible: $($_.Exception.Message)"
}

# Start SQL Server
# - Apple Silicon without x86_64 emulation: use Azure SQL Edge (native ARM64 support)
# - All other platforms (Windows, Intel Mac, Apple Silicon with emulation): use full SQL Server
if ($isMac -and $isAppleSilicon -and -not $isProperAMD64Emulation) {
  & docker run -d --cap-add SYS_PTRACE -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" -e "MSSQL_PID=Developer" -p 1433:1433 --name localmssql mcr.microsoft.com/azure-sql-edge
} else {
  & docker run -d --cap-add SYS_PTRACE -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" -e "MSSQL_PID=Developer" -p 1433:1433 --name localmssql mcr.microsoft.com/mssql/server:2022-latest
}
if ($LASTEXITCODE -ne 0) {
  throw "Failed to start SQL Server container. Exit code: $LASTEXITCODE"
}

& docker run -d -e "MYSQL_ROOT_HOST=%" -e "MYSQL_ROOT_PASSWORD=etlboxpassword" -p 3306:3306 --name localmysql mysql/mysql-server
if ($LASTEXITCODE -ne 0) {
  throw "Failed to start MySQL container. Exit code: $LASTEXITCODE"
}

& docker run -d -e "POSTGRES_PASSWORD=etlboxpassword" -e "LANG=en_US.utf8" -p 5432:5432 --name localpostgres postgres
if ($LASTEXITCODE -ne 0) {
  throw "Failed to start PostgreSQL container. Exit code: $LASTEXITCODE"
}

& docker run -d --name localclickhouse -p 8123:8123 -p 9000:9000 -e "CLICKHOUSE_USER=clickhouse" -e "CLICKHOUSE_PASSWORD=Qwe123456" -e "CLICKHOUSE_MAX_CONNECTIONS=100" clickhouse/clickhouse-server
if ($LASTEXITCODE -ne 0) {
  throw "Failed to start ClickHouse container. Exit code: $LASTEXITCODE"
}

& docker run -d --name localkafka -p 9092:9092 -p 9093:9093 -e "KAFKA_NODE_ID=1" -e "KAFKA_PROCESS_ROLES=broker,controller" -e "KAFKA_CONTROLLER_QUORUM_VOTERS=1@localhost:9093" -e "KAFKA_LISTENERS=PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093" -e "KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092" -e "KAFKA_CONTROLLER_LISTENER_NAMES=CONTROLLER" -e "KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR=1" -e "KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR=1" -e "KAFKA_TRANSACTION_STATE_LOG_MIN_ISR=1" -e "KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS=0" -e "CLUSTER_ID=MkU3OEVBNTcwNTJENDM2Qk" apache/kafka:latest
if ($LASTEXITCODE -ne 0) {
  throw "Failed to start Kafka container. Exit code: $LASTEXITCODE"
}
