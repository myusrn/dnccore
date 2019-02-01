# dnccore


This repo contains a .net core helper library that i bundle into a nuget package for easy consumption and updating.

Using [nuget.org](http://nuget.org/packages/MyUsrn.Dnc.Core/) publicly accessible package feed versus an azure devops, pka visual studio 
team services [vsts], everyone in account only accessible package feed.


[![build status](https://dev.azure.com/myusrn/myprjn/_apis/build/status/myusrn.dnccore?branchName=master)](https://dev.azure.com/myusrn/myprjn/_build/latest?definitionId=1&branchName=master) &nbsp; 
[![nuget status](https://img.shields.io/nuget/v/MyUsrn.Dnc.Core.svg?colorB=brightgreen)](https://www.nuget.org/packages/MyUsrn.Dnc.Core)  

[//]: # ( see https://raw.githubusercontent.com/subor/nng.NETCore/master/README.md for example of nuget | build | tests | codecov badges )

- - -

So far this package includes:
  
  * a RouteExAttribute implementation to enable use of query string parameter, in addition to out of the box [oob] provided request url, based web api versioning support

  * a redis cache based app TokenCache implemenation to facilitate openid connect [oidc] and on-behalf of token caching in confidential web apps using microsoft authentication library [msal] and running across multiple servers

  * a file based based user TokenCache implemenation to facilitate oauth refresh token caching in public mobile/native/spa apps using microsoft authentication library [msal]

### examples of using RouteExAttribute
// GET api/values or api/v1.0/values or api/values?api-version=1.0  
[Route("api/v1.0/values"), RouteEx("api/values", "1.0")]  
public IEnumerable&lt;string&gt; Get() { . . . }  
  
// GET api/v2.0/values or api/values?api-version=2.0  
[Route("api/v2.0/values"), RouteEx("api/values", "2.0")]  
public IEnumerable&lt;string&gt; GetV2() { . . . }
  
### examples of using azure redis cache based app TokenCache 
var userId = context.AuthenticationTicket.Identity.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;  
var app = new ConfidentialClientApplication(confidentialClientId, confidentialClientRedirectUri,
    new ClientCredential(confidentialClientSecret), null, RedisTokenCache.GetUserCache(userid));
<br />

- - - 

#### solution notes 
continuous integration / continuous delivery is accomplished out using azure devops pipeline build of nuget package and release of it into nuget feed

localhost nuget package generation, including symbols and sources for f11 step into debugging into support, is carried out using following command:  
```
dotnet pack -c Release -o d:/temp -p:PackageReleaseNotes="localhost debug and test package build" --include-symbols --include-source ./Core/Core.csproj
```   
and for reviewing package output use following command:  
```
move /y d:/temp/MyUsrn.Dnc.Core.<version>.nupkg d:/temp/MyUsrn.Dnc.Core.<version>[.symbols].nupkg.zip
```

or to enable localhost nuget package reference update every time you build the following project PostBuildEvent setting:  
```
if /i "$(BuildingInsideVisualStudio)" == "true" if /i "$(ConfigurationName)" == "debug" (        
  dotnet pack -o d:/temp -p:PackageReleaseNotes="localhost debug and test package build" --include-symbols --include-source $(ProjectPath)  
)
```  

localhost nuget package publishing is carried out using following command:  
```
nuget setApiKey <nuget.org/symbolsource.org apikey>    
nuget push %temp%\packages\MyUsrn.Dnc.Core.<version>.nupkg [ -Source https://api.nuget.org/v3/index.json ]
```

where presence of symbols.nupkg will cause above to also execute 
```
nuget push %temp%\packages\MyUsrn.Dnc.Core.&lt;version&gt;.symbols.nupkg [ -Source https://nuget.smbsrc.net/ ]
```  
where [nuget.smbsrc.net](https://nuget.smbsrc.net/) is the feed url for [symbolsource.org](http://www.symbolsource.org/) packages  

or localhost nuget package publishing to [azure devops feed](https://docs.microsoft.com/en-us/azure/devops/artifacts/?view=azure-devops) is carried out using following command:  
```
nuget push -Source https://<account>.pkgs.visualstudio.com/DefaultCollection/_packaging/&<feed>/nuget/v3/index.json -ApiKey AzureDevOps d:/temp/MyUsrn.Dnc.Core.<version>.symbols.nupkg
```

for redis cache learning and expermintation see [intro to redis](http://redis.io/topics/data-types-intro) using redis-cli.exe for windows found at 
[MsOpenTech redis for windows](https://github.com/MSOpenTech/redis/) | releases | latest release | downloads | Redis-x64-3.0.500.zip  

http://aspnet.codeplex.com/SourceControl/latest#Samples/WebApi/RoutingConstraintsSample/RoutingConstraints.Server/VersionedRoute.cs
is now a dead link so for current insights see "asp.net core routefactoryattribute [ ihttprouteconstraint ]" -> 
versionedroute attribute implementation https://stackoverflow.com/questions/32892557/versionedroute-attribute-implementation-for-mvc6 | this tutorial ->
https://weblogs.asp.net/jongalloway/looking-at-asp-net-mvc-5-1-and-web-api-2-1-part-2-attribute-routing-with-custom-constraints
