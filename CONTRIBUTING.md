# For contributors
Here is a short list of things contributors might want to do:

## CSharp samples

#### Building the CSharp samples
To build all the CSharp samples without opening Visual Studio, you can use this simple script:
```
for /R %f in (*.sln) do (nuget restore "%f" & msbuild /m "%f")
```

If you want to write the output of the build to a file you can tack on ` >> mybuild.tmp` to the end of the command.

#### Update the Azure.Batch NuGet project reference
The Azure Batch samples should be kept up to date with the latest Azure.Batch NuGet package.

##### Using nuget.exe
To update the NuGet reference the easiest way is to use the nuget command line:
`for /R %f in (*.sln) do (nuget restore "%f" & nuget update %f -Id Azure.Batch)`

Note that this will miss updating the shared projects, so you'll have to do those by hand (either with the command line or with visual studio).  If you do those as well, note that you'll have to fix up the `HintPath` to point to `$(SolutionDirectory)` again.

##### Using a text replace
You can also just do a textual replace.  This can be done as long as the dependencies have not changed between the old and new versions of the Azure.Batch NuGet package.

The following commands can be used to perform the textual replace:
`rep.exe -find:"Azure.Batch.3.0.0" -replace:"Azure.Batch.3.1.0" -r *.csproj`

`rep.exe -find:"Include=\"Microsoft.Azure.Batch, Version=3.0.0.0" -replace:"Include=\"Microsoft.Azure.Batch, Version=3.1.0.0" -r *.csproj`

`rep.exe -find:"package id=\"Azure.Batch\" version=\"3.0.0\"" -replace:"package id=\"Azure.Batch\" version=\"3.1.0\"" -r packages.config`

 