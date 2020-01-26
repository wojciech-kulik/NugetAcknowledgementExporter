# NugetAcknowledgementExporter

This tool is responsible for extracting licenses from NuGet packages included in .NET projects.  

It can be used for auto-generating "Acknowledgement" page in your project.

## Usage

1. Download code:
```bash
git clone https://github.com/wojciech-kulik/NugetAcknowledgementExporter.git
```

2. Build & run:
```bash
dotnet run -- <args or --help>
```

# Arguments

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
dotnet run -- "/Users/YYY/repositories/my-project/source" -output="/Users/YYY/Desktop/"
```

or once it's built, you can navigate to binary and run it directly: 

```bash
./NugetAcknowledgementExporter "/Users/YYY/repositories/my-project/source" -output="/Users/YYY/Desktop/"
```

## Output
`acknowledgements.txt` - containing all packages and licenses ready to be included within your project. Example:
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

`project_packages.json` - containing all packages in JSON file, could be used for building custom "Acknowledgements" page within your project. It contains the following fields: `Name`, `Authors`, `Version`, `License`, `LicenseUrl`, `ProjectUrl`.

# Features
- [x] Detecting `*.csproj` files recursively in project directory
- [x] Extracting NuGet packages from `*.csproj`
- [x] Extracting NuGet packages from `packages.json`
- [x] Downloading licenses from `licenseUrl` (included in NuGet `nuspec` file)
- [x] Adding custom licenses (edit file: `licenses/licenses.json`)
- [x] Excluding packages (edit file: `licenses/exclude.json`)
- [x] Including custom packages (edit file: `licenses/include.json`)

# TODO
- [ ] More testing
- [ ] ?