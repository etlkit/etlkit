## Docker setup

```powershell
.\test\Set-Configuration.ps1
.\test\Run-Containers.ps1
```

### MySql

Login to 
localhost (3306)
User: root
Password: etlkitpassword


### Postgres

Login to
localhost (5432)
User: postgres
Password: etlkitpassword
Database: postgres


### SqlServer

Login to
localhost (1433)
User: sa
Password: YourStrong@Passw0rd


## Odbc setup

For excel: download latest driver
- Odbc driver needs to be 64bit if using 64bit .NET core 
- (Visual Studio 2019 16.4 changed default behvaiour for xunit Tests - they now run with .NET Core 32bit versions)
- Recommended: Driver Access >2016 https://www.microsoft.com/en-us/download/details.aspx?id=54920
```
choco install made-2016 
```
- Old driver: Driver Access >2010 https://www.microsoft.com/en-us/download/details.aspx?id=13255
- Start "ODBC data sources 64-bit" , just add "Microsoft Access driver" as System DNS
 - enter a data source name,e.g. Accesss - no further configuration needed
- Everything else is derived from Connection String

### ODBC on Mac
```
brew install unixodbc
brew install mdbtools
```
