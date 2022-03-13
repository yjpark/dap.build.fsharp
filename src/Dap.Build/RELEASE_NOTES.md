## 0.8.1
* Change target to net6.0

## 0.8.0
* Update with Dotnet 6.0 FAKE 5.22.0 Paket 6.2.1

## 0.7.5
* Update to FAKE 5.20.3

## 0.7.4
* Update to FAKE 5.19.1

## 0.7.3
* Remove dotnet build from fable serve and bundle dependencies which is not working with latest fable and Thoth.Json

## 0.7.2
* Remove NoBuild param in dotent pack which got NETSDK1085 error when building with dotnet core 3.0

## 0.7.1
* Change target framework back to netstandard2.0 since FAKE is with netcoreapp2.1

## 0.7.0
* Update target framework to netstandard2,1 build with DotNet Core 3.0

## 0.6.14
* Update to FAKE 5.16.1

## 0.6.13
* Update to FAKE 5.16.0

## 0.6.12
* Update to FAKE 5.13.3

## 0.6.11
* Update Fable targets to Fable.Core 3.0

## 0.6.10
* Can build xamarin ios android and mac projects

## 0.6.9
* Update dependencies with Fake 5.12.1

## 0.6.8
* Remove Recover target which might cause inconsistent state and can be replaced by fetch or inject

## 0.6.7
* Support mixed debug and release configuration

## 0.6.6
* Support both nuget.org and proget feeds

## 0.6.5
* Change default config for Fable

## 0.6.4
* Update with Fake 5.7.2

## 0.6.3
* Generate .sha512 file when inject
* Save original version before inject
* Create Recover target to remove the injected version
* Create Fetch target to fetch nupkg from feed
* Create sub tragets per project
* Add Run and WatchRun targets
* Add Fable targets

## 0.6.2
* Create targets for each projects

## 0.6.1
* Dap.Build.DotNet for common targets

## 0.6.0
* Functions for standard build for nuget packages
