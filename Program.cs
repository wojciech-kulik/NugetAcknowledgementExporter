using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NDesk.Options;

namespace NugetAcknowledgementExporter
{
    class Program
    {
        static string ProjectDirectory = "";
        static string OutputDirectory = null;
        static bool GenerateJson = true;
        static bool GenerateTxt = true;
        static bool ShowHelp = false;

        static async Task Main(string[] args)
        {
            // CHECK ARGUMENTS
            if (!ParseArguments(args) || ShowHelp) return;

            if (string.IsNullOrWhiteSpace(ProjectDirectory) || !Directory.Exists(ProjectDirectory))
            {
                Console.WriteLine("Please specify project directory. For more information use --help.");
                return;
            }

            // FIND ALL CSPROJ FILES
            Console.WriteLine($"\n⌛️ Searching for CSPROJ files in {ProjectDirectory}...");
            var projects = FindAllCSProjs(ProjectDirectory);
            Console.WriteLine($"✅ Found {projects.Count} projects\n");
            if (projects.Count == 0)
            {
                Console.WriteLine("❌ Could not find any project file.");
                return;
            }

            // PARSE NUGET PACKAGES
            Console.WriteLine("⌛️ Searching for NuGet packages...");
            var includedPackages = projects
                .SelectMany(path => GetIncludedPackages(path))
                .Distinct(new NugetPackageEqualityComparer())
                .OrderBy(x => x.Name)
                .ToList();
            if (includedPackages.Count == 0)
            {
                Console.WriteLine("❌ Could not find any NuGet packages.");
                return;
            }
            Console.WriteLine($"✅ Detected {includedPackages.Count} nuget packages\n");

            // EXCLUDE PACKAGES
            Console.WriteLine("⌛️ Excluding packages...");
            await FilterOut(includedPackages);

            // DOWNLOAD LICENSES AND EXTRACT NUGET PACKAGE DETAILS
            Console.WriteLine("⌛️ Downloading licenses...");
            try
            {
                await FillPackagesDetails(includedPackages);
                Console.WriteLine("✅ Finished downloading licenses\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Please check if `nuget` command line tool is installed");
            }

            // INCLUDE PACKAGES FROM "include.json" file
            Console.WriteLine("⌛️ Including custom packages...");
            await IncludeCustomPackages(includedPackages);
            includedPackages = includedPackages
                .Distinct(new NugetPackageEqualityComparer())
                .OrderBy(x => x.Name)
                .ToList();

            // EXPORT JSON FILE
            if (GenerateJson)
            {
                Console.WriteLine("⌛️ Exporting project_packages.json...");
                ExportToJson(includedPackages);
                Console.WriteLine("✅ Exported project_packages.json\n");
            }

            // EXPORT TXT FILE
            if (GenerateTxt)
            {
                Console.WriteLine("⌛️ Exporting acknowledgements.txt...");
                ExportAcknowledgements(includedPackages);
                Console.WriteLine("✅ Exported acknowledgements.txt\n");
            }

            Console.WriteLine($"🎉🎉 Finished exporting acknowledgements for {includedPackages.Count} packages");
            Console.WriteLine($"🎉🎉 Output directory: {OutputDirectory}");
        }

        static bool ParseArguments(string[] args)
        {
            var options = new OptionSet {
                { "o|output=", "directory where generated files will be saved (by default project directory)", x => OutputDirectory = x },
                { "sj|skipJson",  "skips generating json file with acknowledgements", v => GenerateJson = v == null },
                { "st|skipTxt",  "skips generating text file with acknowledgements", v => GenerateTxt = v == null },
                { "h|help",  "\tshows all available parameters", v => ShowHelp = v != null },
            };

            try
            {
                ProjectDirectory = options.Parse(args)?.FirstOrDefault();
                OutputDirectory ??= ProjectDirectory;

                if (ShowHelp)
                {
                    Console.WriteLine("\nNugetAcknowledgementExporter exports all used NuGet packages to JSON file and TXT file which can be included within an application. It also downloads associated licenses.");
                    Console.WriteLine("\nUsage: NugetAcknowledgementExporter <project directory> [args]");
                    Console.WriteLine("\nAvailable parameters:");
                    options.ToList().ForEach(x => Console.WriteLine($"\t{x.Prototype}\t\t{x.Description}"));
                    Console.WriteLine("\nTo add custom licenses or packages please edit:\n- licenses/licenses.json\n- licenses/include.json\n- licenses/exclude.json");
                }

                return true;
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `NugetAcknowledgementExporter --help' for more information.");
                return false;
            }
        }

        static string GetNugetCachePath()
        {
            var nugetLists = "nuget locals all -list".Bash();
            var globalPackages = nugetLists.Split('\n').FirstOrDefault(x => x.StartsWith("global-packages: "));

            if (string.IsNullOrWhiteSpace(globalPackages))
            {
                throw new InvalidOperationException("❌ Could not get NuGet cache directory (error: 1)");
            }

            var path = globalPackages.Substring("global-packages: ".Length).Trim();
            if (!Directory.Exists(path))
            {
                throw new InvalidOperationException("❌ Could not get NuGet cache directory (error: 2)");
            }

            return path;
        }

        static List<string> FindAllCSProjs(string basePath)
        {
            var result = new List<string>();

            result.AddRange(Directory.EnumerateFiles(basePath).Where(x => Path.GetExtension(x).ToLower() == ".csproj"));

            foreach (var directory in Directory.GetDirectories(basePath))
            {
                result.AddRange(FindAllCSProjs(directory));
            }

            return result;
        }

        static List<NugetPackage> GetIncludedPackages(string csproj)
        {
            var regex = new Regex("PackageReference Include=\"([^\"]+)\" Version=\"([^\"]+)\" \\/>");
            var regex2 = new Regex("PackageReference Include=\"([^\"]+)\">[\\s]+<Version>([^\\<]+)<\\/Version>");
            var file = File.ReadAllText(csproj);

            var packages = regex.Matches(file).Select(x => new NugetPackage
            {
                Name = x.Groups[1].Value,
                Version = x.Groups[2].Value
            }).ToList();

            packages.AddRange(regex2.Matches(file).Select(x => new NugetPackage
            {
                Name = x.Groups[1].Value,
                Version = x.Groups[2].Value
            }));

            packages.AddRange(GetPackagesFromConfig(csproj));

            return packages;
        }

        static List<NugetPackage> GetPackagesFromConfig(string csproj)
        {
            var configFile = Path.Combine(Path.GetDirectoryName(csproj), "packages.config");
            if (!File.Exists(configFile))
            {
                return new List<NugetPackage>();
            }

            var regex = new Regex("package id=\"([^\"]+)\" version=\"([^\"]+)\"");
            var file = File.ReadAllText(configFile);

            var packages = regex.Matches(file).Select(x => new NugetPackage
            {
                Name = x.Groups[1].Value,
                Version = x.Groups[2].Value
            }).ToList();

            return packages;
        }

        static async Task<List<Filter>> GetFilters()
        {
            try
            {
                var content = await File.ReadAllTextAsync("licenses/exclude.json");
                var filters = JsonConvert.DeserializeObject<List<Filter>>(content);
                return filters;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Could not get filters from licenses/exclude.json");
                Console.WriteLine(ex.Message);
                return new List<Filter>();
            }
        }

        static async Task FilterOut(List<NugetPackage> packages)
        {
            var counter = packages.Count;
            var filters = await GetFilters();

            packages.RemoveAll(package => filters.Any(filter => filter.Matches(package)));
            Console.WriteLine($"✅ Excluded {counter - packages.Count} nuget packages\n");
        }

        static async Task FillPackagesDetails(List<NugetPackage> packages)
        {
            var nugetCache = GetNugetCachePath();

            foreach (var package in packages)
            {
                var fileName = $"{package.Name.ToLower()}.nuspec";
                var specFilePath = Path.Combine(nugetCache, package.Name.ToLower(), package.Version, fileName);

                try
                {
                    var spec = new XmlDocument();
                    spec.Load(specFilePath);
                    var metadata = spec.DocumentElement["metadata"];

                    package.Authors = metadata["authors"].InnerText;
                    package.ProjectUrl = metadata["projectUrl"].InnerText;
                    package.LicenseUrl = metadata["licenseUrl"].InnerText;

                    await ResolveUrls(package);
                    var licenses = await GetLicenses();
                    await FillLicense(package, licenses);

                    if (string.IsNullOrWhiteSpace(package.License))
                    {
                        await DownloadLicense(package);
                    }
                }
                catch (XmlException)
                {
                    Console.WriteLine($"❌ Could not parse NuSpec for {package.Name} (path: {specFilePath})");
                }
                catch
                {
                    Console.WriteLine($"❌ Could not get license for {package.Name} (url: {package.LicenseUrl})");
                }
            }
        }

        static async Task<List<License>> GetLicenses()
        {
            try
            {
                var licenses = await File.ReadAllTextAsync("licenses/licenses.json");
                var list = JsonConvert.DeserializeObject<List<License>>(licenses);
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Could not get licenses from licenses/licenses.json");
                Console.WriteLine(ex.Message);
                return new List<License>();
            }
        }

        static async Task FillLicense(NugetPackage package, List<License> licenses)
        {
            var license = licenses.FirstOrDefault(x => x.PackageName != null && x.PackageName.ToLowerInvariant() == package.Name.ToLowerInvariant());
            license ??= licenses.FirstOrDefault(x => x.LicenseUrl != null && x.LicenseUrl.ToLowerInvariant() == package.LicenseUrl.ToLowerInvariant());
            if (license == null) return;

            var path = license.File;
            if (!Path.IsPathFullyQualified(path))
            {
                path = Path.Combine("licenses", path);
            }

            if (!File.Exists(path))
            {
                Console.WriteLine($"❌ License file: {path} does not exists");
                return;
            }

            package.License = await File.ReadAllTextAsync(path);
        }

        static async Task ResolveUrls(NugetPackage package)
        {
            using var httpClient = new HttpClient();

            var headRequest = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, package.LicenseUrl));
            package.LicenseUrl = headRequest.RequestMessage.RequestUri.AbsoluteUri;

            try
            {
                headRequest = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, package.ProjectUrl));
                package.ProjectUrl = headRequest.RequestMessage.RequestUri.AbsoluteUri;
            }
            catch { }
        }

        static async Task DownloadLicense(NugetPackage package)
        {
            using var httpClient = new HttpClient();
            var lowerLicenseUrl = package.LicenseUrl.ToLowerInvariant();

            if (lowerLicenseUrl.Contains("licenses.nuget.org/mit") || lowerLicenseUrl.Contains("opensource.org/licenses/mit"))
            {
                package.License = MITLicense(package.Authors);
            }
            else if (lowerLicenseUrl.Contains("apache.org/licenses/license-2.0"))
            {
                package.License = ApacheLicense(package.Authors);
            }
            else if (lowerLicenseUrl.Contains("github.com") ||
                     lowerLicenseUrl.EndsWith(".txt") ||
                     lowerLicenseUrl.EndsWith(".md") ||
                     lowerLicenseUrl.Contains("raw.githubusercontent.com"))
            {
                package.License = (await httpClient.GetStringAsync(package.LicenseUrl.Replace("/blob/", "/raw/")))?.Trim();
            }

            if (string.IsNullOrWhiteSpace(package.License))
            {
                Console.WriteLine($"❌ Could not download license for {package.Name} (url: {package.LicenseUrl})");
            }
        }

        static async Task IncludeCustomPackages(List<NugetPackage> packages)
        {
            try
            {
                var content = await File.ReadAllTextAsync("licenses/include.json");
                var include = JsonConvert.DeserializeObject<List<NugetPackage>>(content);
                packages.AddRange(include);

                if (include.Count == 0)
                {
                    Console.WriteLine("✅ No custom packages\n");
                }
                else
                {
                    Console.WriteLine($"✅ Included {include.Count} custom packages\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Could not include custom packages from `include.json` file");
                Console.WriteLine(ex.Message);
            }
        }

        static List<NugetPackage> GroupPackages(List<NugetPackage> packages)
        {
            var grouped = packages
                .GroupBy(x => string.Join("|", x.LicenseUrl, x.ProjectUrl.TrimEnd('/', ' '), x.Authors))
                .Select(x => new NugetPackage
                {
                    Name = string.Join("\n", x.Select(x => x.Name)),
                    Authors = x.First().Authors,
                    LicenseUrl = x.First().LicenseUrl,
                    ProjectUrl = x.First().ProjectUrl,
                    License = x.First().License
                })
                .OrderBy(x => x.Name)
                .ToList();

            Console.WriteLine($"✅ Grouping finished -> {grouped.Count} packages");

            return grouped;
        }

        static void ExportToJson(List<NugetPackage> packages)
        {
            var json = JsonConvert.SerializeObject(packages, Newtonsoft.Json.Formatting.Indented);
            var path = Path.Combine(OutputDirectory, "project_packages.json");
            File.WriteAllText(path, json);
        }

        static void ExportAcknowledgements(List<NugetPackage> packages)
        {
            var result = "";
            var groupedPackages = GroupPackages(packages);

            foreach (var package in groupedPackages)
            {
                if (result != "")
                {
                    result += "\n\n".PadRight(30, '-') + "\n\n";
                }

                result += $"{package.Name}";
                result += $"\n\nAuthors: {package.Authors}";
                result += $"\nProject URL: {package.ProjectUrl}";
                result += $"\nLicense URL: {package.LicenseUrl}\n\n";
                result += package.License;
            }

            while (result.Contains("\n\n\n"))
            {
                result = result.Replace("\n\n\n", "\n\n");
            }

            var path = Path.Combine(OutputDirectory, "acknowledgements.txt");
            File.WriteAllText(path, result);
        }

        static string MITLicense(string authors)
        {
            return @$"Copyright (c) {authors}

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
""Software""), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/ or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT.IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";
        }

        static string ApacheLicense(string authors)
        {
            return $@"Copyright {authors}

Licensed under the Apache License, Version 2.0 (the ""License"");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an ""AS IS"" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.";
        }
    }
}
