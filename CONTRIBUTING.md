# Contributing to Azure Batch samples

Thank you for your interest in contributing to Azure batch samples!

## Ways to contribute

You can contribute to [Azure batch samples](https://github.com/Azure/azure-batch-samples/) in a few different ways:

- Submit issues through [issue tracker](https://github.com/Azure/azure-batch-samples/issues) on GitHub. We are actively monitoring the issues and improving our samples.
- If you wish to make code changes to samples, or contribute something new, please follow the [GitHub Forks / Pull requests model](https://help.github.com/articles/fork-a-repo/): Fork the sample repo, make the change and propose it back by submitting a pull request.


## CSharp samples

#### Building the CSharp samples
To build all the CSharp samples without opening Visual Studio, you can use this simple script:
```
for /R %f in (*.sln) do (dotnet build "%f")
```

If you want to write the output of the build to a file you can tack on ` >> mybuild.tmp` to the end of the command.

#### Update the Azure.Batch NuGet project reference
The Azure Batch samples should be kept up to date with the latest Azure.Batch NuGet package.

##### Using a text replace
The following commands can be used to perform the textual replace: `rep.exe -find:"<PackageReference Include=\"Microsoft.Azure.Batch\" Version=\"10.0.0\" />" -replace:"<PackageReference Include=\"Microsoft.Azure.Batch\" Version=\"12.0.0\" />" -r *.csproj`
