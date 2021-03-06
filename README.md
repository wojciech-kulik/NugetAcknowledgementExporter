[![BuyMeACoffee](https://www.buymeacoffee.com/assets/img/guidelines/download-assets-sm-2.svg)](https://www.buymeacoffee.com/WojciechKulik)

# Nuget Acknowledgement Exporter

![version](https://img.shields.io/badge/version-0.9.1-green) [![NuGet](https://img.shields.io/badge/NuGet-0.9.1-blue.svg)](https://www.nuget.org/packages/NugetAcknowledgementExporter/) [![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/wojciech-kulik/NugetAcknowledgementExporter/blob/master/LICENSE/)

Small tool responsible for extracting licenses from NuGet packages included in .NET projects.

Usage of Third-Party Libraries usually requires to include licenses within an application. This command-line tool can be used for auto-generating the “Acknowledgement” / “Third-Party Libraries” page in your project.

NugetAcknowledgementsExporter finds all CSPROJs in a directory from the argument, extracts all included NuGet packages and downloads their licenses. Based on gained data it builds two files:

- **acknowledgements.txt** – text file containing all acknowledgements for NuGet packages. It could be directly included within your application in a scrollable text field.
- **project_packages.json** – JSON file containing an array of used NuGet packages including downloaded licenses. It could be used to build a more interesting UI for acknowledgements, generated from code based on this file.

## Features

- [x] Detecting `*.csproj` files recursively in project directory
- [x] Extracting NuGet packages from `*.csproj`
- [x] Extracting NuGet packages from `packages.json`
- [x] Grouping packages with the same authors, projectUrl and licenseUrl
- [x] Downloading licenses from `licenseUrl` (included in NuGet `nuspec` file)
- [x] Adding custom licenses (edit file: `licenses/licenses.json`)
- [x] Excluding packages (edit file: `licenses/exclude.json`)
- [x] Including custom packages (edit file: `licenses/include.json`)
- [x] Windows and MacOS support

## Requirements

1. Download latest .NET Core
```
https://dotnet.microsoft.com/download/
```

2. Download latest NuGet commandline tool
```
Windows: https://www.nuget.org/downloads (+ add to PATH)
MacOS: brew install nuget
```

## Usage

1. Download code:
```bash
git clone https://github.com/wojciech-kulik/NugetAcknowledgementExporter.git
```

2. Restore NuGet packages for all projects. Application uses NuGet cache, so it needs to be there.

3. Build & run:
```bash
dotnet run -- <args or --help>
```

## Arguments

```bash
Usage: NugetAcknowledgementExporter <project directory> [args]

Available parameters:
	o|output=		directory where generated files will be saved (by default project directory)
	sj|skipJson		skips generating json file with acknowledgements
	st|skipTxt		skips generating text file with acknowledgements
	h|help			shows all available parameters

To add custom licenses or packages please edit:
- licenses/licenses.json
- licenses/include.json
- licenses/exclude.json

```

## Sample command

```bash
dotnet run -- "/Users/YYY/repositories/my-project/source" -output="/Users/YYY/Desktop"
```

or once it's built, you can navigate to binary and run it directly: 

```bash
MacOS: ./NugetAcknowledgementExporter "/Users/YYY/repositories/my-project/source" -output="/Users/YYY/Desktop"
Windows: NugetAcknowledgementExporter "C:\Users\YYY\repositories\my-project\source" -output="C:\Users\YYY\Desktop"
```

## Sample Output
```
Plugin.Permissions

Authors: James Montemagno
Project URL: https://github.com/jamesmontemagno/PermissionsPlugin
License URL: https://github.com/jamesmontemagno/PermissionsPlugin/blob/master/LICENSE

The MIT License (MIT)

Copyright (c) 2016 James Montemagno

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), (...)

----------------------------

Plugin.StoreReview

Authors: James Montemagno
Project URL: https://github.com/jamesmontemagno/StoreReviewPlugin
License URL: https://github.com/jamesmontemagno/StoreReviewPlugin/blob/master/LICENSE

The MIT License (MIT)

Copyright (c) 2016 James Montemagno

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), (...)

(...)
```

## TODO
- [ ] More testing
- [ ] Code refactoring - split `Program.cs` into classes
- [ ] Download and cache nuspec from https://www.nuget.org/api/v2/package/{packageID} instead of relying on NuGet's cache
- [ ] Custom templates for `acknowledgements.txt`
- [ ] Recognizing popular URLs with well-known licenses (like already done for: opensource.org/licenses/mit and licenses.nuget.org/mit)
- [ ] Automatic run when `dotnet restore` or `nuget restore` called (if possible?)
